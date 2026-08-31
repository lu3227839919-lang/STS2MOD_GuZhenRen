using System.Globalization;

using GuZhenRen.Aperture;
using GuZhenRen.Relics;
using GuZhenRen.Tribulations.Core;

using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Testing;

/// <summary>
/// 蛊真人模组的统一测试控制台入口。
///
/// gzr status
/// gzr card &lt;card-id&gt; [rank]
/// gzr xp [amount]
/// gzr rank &lt;target-rank&gt;
/// gzr tribulation [id]
/// </summary>
public sealed class GuZhenRenConsoleCmd : AbstractConsoleCmd
{
    private static readonly string[] Subcommands =
    [
        "status",
        "card",
        "xp",
        "rank",
        "tribulation",
    ];

    public override string CmdName => "gzr";

    public override string Args =>
        "<status|card|xp|rank|tribulation> [arguments]";

    public override string Description =>
        "Gu Zhen Ren test tools: status, card grant, cultivation, rank up, and forced tribulation.";

    public override bool IsNetworked => true;

    public override CmdResult Process(
        Player? issuingPlayer,
        string[] args
    )
    {
        if (!RunManager.Instance.IsInProgress || issuingPlayer == null)
        {
            return new CmdResult(
                success: false,
                "当前没有正在进行的游戏。"
            );
        }

        if (args.Length == 0)
        {
            return Help();
        }

        string subcommand = args[0].ToLowerInvariant();
        return subcommand switch
        {
            "status" => ShowStatus(issuingPlayer),
            "card" => GrantCard(issuingPlayer, args[1..]),
            "xp" => GrantCultivation(issuingPlayer, args[1..]),
            "rank" => AdvanceRank(issuingPlayer, args[1..]),
            "tribulation" or "trib" =>
                ForceTribulation(issuingPlayer, args[1..]),
            _ => new CmdResult(
                success: false,
                $"未知的 gzr 子命令：{args[0]}\n" +
                HelpText
            ),
        };
    }

    public override CompletionResult GetArgumentCompletions(
        Player? player,
        string[] args
    )
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(
                Subcommands,
                [],
                args.FirstOrDefault() ?? string.Empty,
                CompletionType.Subcommand
            );
        }

        string subcommand = args[0].ToLowerInvariant();
        string partial = args[^1];
        string[] completed = args[..^1];

        if (subcommand == "card" && args.Length == 2)
        {
            return CompleteArgument(
                ModelDb.AllCards.Select(card => card.Id.Entry),
                completed,
                partial
            );
        }

        if (subcommand == "card" && args.Length == 3)
        {
            return CompleteArgument(
                Enumerable.Range(1, 9).Select(rank => rank.ToString()),
                completed,
                partial
            );
        }

        if (subcommand == "rank" && args.Length == 2)
        {
            return CompleteArgument(
                Enumerable.Range(1, 9).Select(rank => rank.ToString()),
                completed,
                partial
            );
        }

        if (subcommand == "xp" && args.Length == 2)
        {
            return CompleteArgument(
                ["1", "2", "3", "5", "10"],
                completed,
                partial
            );
        }

        if ((subcommand == "tribulation" || subcommand == "trib") &&
            args.Length == 2)
        {
            IEnumerable<string> candidates =
                new[] { "random" }.Concat(
                    TribulationSystem.Registry.Definitions
                        .Select(definition => definition.Id)
                        .OrderBy(id => id, StringComparer.Ordinal)
                );

            return CompleteArgument(
                candidates,
                completed,
                partial
            );
        }

        return new CompletionResult
        {
            Type = CompletionType.Argument,
            ArgumentContext = CmdName,
        };
    }

    private static string HelpText =>
        "gzr status\n" +
        "gzr card <card-id> [rank]\n" +
        "gzr xp [amount]\n" +
        "gzr rank <target-rank>\n" +
        "gzr tribulation [id|random]";

    private static CmdResult Help() =>
        new(success: true, HelpText);

    private static CmdResult ShowStatus(Player player)
    {
        if (!HasAperture(player))
        {
            return new CmdResult(
                success: false,
                "当前玩家没有空窍遗物。"
            );
        }

        ApertureRunData data = ApertureSystem.GetState(player);
        int required = ApertureProgression.GetRequiredXp(data.Rank);
        string progress = data.IsCultivationComplete
            ? "修行圆满"
            : $"修为 {data.Xp}/{required}";
        string tribulation =
            data.ActiveTribulationFloor == player.RunState.TotalFloor &&
            !string.IsNullOrWhiteSpace(data.ActiveTribulationId)
                ? data.ActiveTribulationId
                : "无";

        return new CmdResult(
            success: true,
            $"空窍：{data.Rank} 转，{progress}\n" +
            $"当前灾劫：{tribulation}"
        );
    }

    private static CmdResult GrantCard(
        Player player,
        string[] args
    )
    {
        if (args.Length == 0)
        {
            return new CmdResult(
                success: false,
                "用法：gzr card <card-id> [rank]"
            );
        }

        // 复用原生 card 命令；GuCardConsoleCommandPatch 会继续处理
        // 蛊牌转数、战斗牌堆落点和永久牌组规则。
        return new CardConsoleCmd().Process(player, args);
    }

    private static CmdResult GrantCultivation(
        Player player,
        string[] args
    )
    {
        if (!HasAperture(player))
        {
            return new CmdResult(
                success: false,
                "当前玩家没有空窍遗物。"
            );
        }

        int amount = 1;
        if (args.Length > 0 &&
            (!int.TryParse(
                args[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out amount
            ) || amount is < 1 or > 9999))
        {
            return new CmdResult(
                success: false,
                "修为数量必须是 1 至 9999 的整数。"
            );
        }

        Task task = GuZhenRenTestSupport.GrantCultivationAsync(
            player,
            amount
        );
        return new CmdResult(
            task,
            success: true,
            $"正在给予 {amount} 点空窍修为。"
        );
    }

    private static CmdResult AdvanceRank(
        Player player,
        string[] args
    )
    {
        if (!HasAperture(player))
        {
            return new CmdResult(
                success: false,
                "当前玩家没有空窍遗物。"
            );
        }

        if (args.Length == 0 ||
            !int.TryParse(
                args[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int targetRank
            ) ||
            targetRank < ApertureProgression.MinimumRank ||
            targetRank > ApertureProgression.MaximumImplementedRank)
        {
            return new CmdResult(
                success: false,
                "目标转数必须是 1 至 9 的整数。"
            );
        }

        int currentRank = ApertureSystem.GetState(player).Rank;
        if (targetRank < currentRank)
        {
            return new CmdResult(
                success: false,
                $"不能用测试命令把空窍从 {currentRank} 转降到 " +
                $"{targetRank} 转。"
            );
        }

        Task task = GuZhenRenTestSupport.AdvanceToRankAsync(
            player,
            targetRank
        );
        return new CmdResult(
            task,
            success: true,
            $"正在把空窍提升到 {targetRank} 转。"
        );
    }

    private static CmdResult ForceTribulation(
        Player player,
        string[] args
    )
    {
        if (!HasAperture(player))
        {
            return new CmdResult(
                success: false,
                "当前玩家没有空窍遗物。"
            );
        }

        if (player.Creature.CombatState == null)
        {
            return new CmdResult(
                success: false,
                "灾劫只能在战斗中触发。"
            );
        }

        string? id = args.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(id) &&
            !id.Equals("random", StringComparison.OrdinalIgnoreCase) &&
            !TribulationSystem.Registry.Definitions.Any(definition =>
                definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            return new CmdResult(
                success: false,
                $"未知灾劫：{id}"
            );
        }

        Task task = GuZhenRenTestSupport.ForceTribulationAsync(
            player,
            id
        );
        return new CmdResult(
            task,
            success: true,
            string.IsNullOrWhiteSpace(id) ||
            id.Equals("random", StringComparison.OrdinalIgnoreCase)
                ? "正在强制触发一个灾劫。"
                : $"正在强制触发灾劫：{id}"
        );
    }

    private static bool HasAperture(Player player) =>
        player.Relics.OfType<KongQiaoRelic>().Any();
}
