using System.Globalization;
using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 让原生 card 控制台指令生成的卡牌自动遵循牌堆规则。
///
/// 支持：
/// card &lt;卡牌ID&gt; &lt;转数&gt;
/// card &lt;卡牌ID&gt; rank=&lt;转数&gt;
///
/// 位置参数即使被原生指令接受，最终也会被本补丁按规则覆盖：
/// 战斗中蛊牌进入恢复堆、普通牌进入手牌；战斗外进入永久牌组。
/// </summary>
internal static class GuCardConsoleCommandPatch
{
    private const string HarmonyId =
        Entry.ModId + ".CardConsoleCommand";

    private static readonly HashSet<string> KnownCardTypeNames =
        typeof(AbstractGuZhenRenCard)
            .Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(AbstractGuZhenRenCard)
                    .IsAssignableFrom(type)
            )
            .Select(type => NormalizeCardArgument(type.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    private sealed class CardCommandState
    {
        public required HashSet<CardModel> ExistingCards { get; init; }

        public int? RequestedRank { get; init; }
    }

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? process = AccessTools.Method(
            typeof(CardConsoleCmd),
            nameof(CardConsoleCmd.Process),
            [typeof(Player), typeof(string[])]
        );

        if (process == null)
        {
            Entry.Logger.Info(
                "未找到 CardConsoleCmd.Process，已跳过卡牌控制台自动落点补丁。"
            );
            _initialized = true;
            return;
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            process,
            prefix: new HarmonyMethod(
                typeof(GuCardConsoleCommandPatch),
                nameof(ProcessPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(GuCardConsoleCommandPatch),
                nameof(ProcessPostfix)
            )
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void ProcessPrefix(
        Player issuingPlayer,
        ref string[] args,
        out CardCommandState __state
    )
    {
        int? requestedRank = null;
        bool outOfCombat =
            issuingPlayer.PlayerCombatState == null;
        bool allowPositionalRank =
            args.Length > 0 &&
            LooksLikeGuZhenRenCardArgument(args[0]);

        List<string> forwardedArgs = [];
        if (args.Length > 0)
        {
            forwardedArgs.Add(args[0]);
        }

        for (int index = 1; index < args.Length; index++)
        {
            string token = args[index];

            if (TryParseRankToken(
                    token,
                    allowPositionalRank,
                    out int rank
                ))
            {
                requestedRank = rank;
                continue;
            }

            // 原生 card 指令默认把牌放进 Hand，而战斗外不存在
            // Hand。战斗外剔除用户可能传入的牌堆参数，并在调用
            // 原方法前强制补上 Deck，避免原方法先于 Postfix 抛错。
            if (outOfCombat && IsNativePileArgument(token))
            {
                continue;
            }

            forwardedArgs.Add(token);
        }

        if (outOfCombat && args.Length > 0)
        {
            forwardedArgs.Add("Deck");
        }

        args = forwardedArgs.ToArray();

        __state = new CardCommandState
        {
            ExistingCards = SnapshotOwnedCards(issuingPlayer),
            RequestedRank = requestedRank,
        };
    }

    private static void ProcessPostfix(
        Player issuingPlayer,
        CardCommandState __state
    )
    {
        CardModel[] grantedCards = SnapshotOwnedCards(issuingPlayer)
            .Where(card =>
                !__state.ExistingCards.Contains(card)
            )
            .ToArray();

        foreach (CardModel grantedCard in grantedCards)
        {
            if (__state.RequestedRank.HasValue &&
                grantedCard is AbstractGuZhenRenCard rankedCard)
            {
                int requested = __state.RequestedRank.Value;
                int normalized = Math.Clamp(
                    requested,
                    AbstractGuZhenRenCard.MinimumGuRank,
                    Math.Max(
                        AbstractGuZhenRenCard.MinimumGuRank,
                        rankedCard.MaxGuRank
                    )
                );

                rankedCard.InitializeGuRankFromSource(normalized);

                if (requested != normalized)
                {
                    Entry.Logger.Info(
                        $"控制台给予 {grantedCard.Id} 时请求 {requested} 转，" +
                        $"已按卡牌上限修正为 {normalized} 转。"
                    );
                }
            }

            var destination =
                GuCardPileSystem.PlaceGrantedCardByRule(
                    grantedCard,
                    issuingPlayer
                );

            string rankText = grantedCard is AbstractGuZhenRenCard guCard
                ? $"转数 {guCard.GuRank}，"
                : string.Empty;

            Entry.Logger.Info(
                $"控制台给予 {grantedCard.Id}：" +
                rankText +
                $"自动目标牌堆 {destination}。"
            );
        }
    }

    private static HashSet<CardModel> SnapshotOwnedCards(
        Player player
    )
    {
        HashSet<CardModel> result = new(player.Deck.Cards);

        if (player.PlayerCombatState == null)
        {
            return result;
        }

        foreach (CardPile pile in player.PlayerCombatState.AllPiles)
        {
            result.UnionWith(pile.Cards);
        }

        return result;
    }

    private static bool IsNativePileArgument(string token)
    {
        return token.Equals("None", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Draw", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Hand", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Discard", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Exhaust", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Play", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("Deck", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRankToken(
        string token,
        bool allowPositionalRank,
        out int rank
    )
    {
        const string prefix = "rank=";
        string value = token;

        if (token.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            value = token[prefix.Length..];
        }
        else if (!allowPositionalRank)
        {
            rank = 0;
            return false;
        }

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out rank
        );
    }

    private static bool LooksLikeGuZhenRenCardArgument(
        string argument
    )
    {
        string normalized = NormalizeCardArgument(argument);
        if (normalized.Contains(
                NormalizeCardArgument(Entry.ModId),
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return true;
        }

        return KnownCardTypeNames.Any(typeName =>
            normalized.EndsWith(
                typeName,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private static string NormalizeCardArgument(string value)
    {
        return string.Concat(
            value.Where(char.IsLetterOrDigit)
        ).ToUpperInvariant();
    }
}
