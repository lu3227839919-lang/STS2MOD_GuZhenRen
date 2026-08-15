using System.Runtime.CompilerServices;
using System.Threading;

using GuZhenRen.Cards.HeLian;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

using STS2RitsuLib;

namespace GuZhenRen.Cards.LiDao;

/// <summary>力道虚影的生成、容量、凝影与同一攻击集中结算。</summary>
public static class LiDaoPhantomSystem
{
    private static readonly AsyncLocal<bool> OtherManifestedState = new();

    internal static bool OtherManifestedForCurrentAttack =>
        OtherManifestedState.Value;

    /// <summary>
    /// 本回合（按回合号）已发生虚影显化的记录，供伴生牌查询
    /// "本回合已有其他虚影显化过" 等条件。战斗内临时状态，不参与存档。
    /// </summary>
    private sealed class TurnManifestation
    {
        internal int Turn;
        internal bool Any;
    }

    private static readonly ConditionalWeakTable<Player, TurnManifestation>
        TurnManifestations = new();

    /// <summary>
    /// 本回合是否已发生过虚影显化（用于牛角顶九转等"虚影联动"判定）。
    /// 仅返回本回合（TurnNumber）内的记录，回合切换后自动视为未显化。
    /// </summary>
    internal static bool HasManifestedThisTurn(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        int turn = owner.PlayerCombatState?.TurnNumber ?? 1;
        return TurnManifestations.TryGetValue(owner, out TurnManifestation? m) &&
            m.Turn == turn &&
            m.Any;
    }

    /// <summary>
    /// 当前常驻虚影包含的兽力种类数（基础虚影计 1 种，百兽虚影按组成兽力计）。
    /// 用于伴生牌"至少 2/3/4 种常驻虚影"的条件。
    /// </summary>
    public static int GetPermanentPhantomKinds(Player owner) =>
        GetPermanentPhantoms(owner)
            .SelectMany(phantom => phantom.LastManifestedKinds)
            .Distinct()
            .Count();

    /// <summary>
    /// 绞摔七转起"剩余段数追击其他敌人"的目标选择：当前 HP 最低的存活敌人。
    /// 与鳄鱼虚影的追击判定一致（确定性排序）。
    /// </summary>
    public static Creature? FindPursuitTarget(CardModel source) =>
        source.CombatState == null
            ? null
            : GuZhenRenDeterminism
                .OrderCreatures(source.CombatState.HittableEnemies)
                .Where(enemy => enemy.IsAlive)
                .OrderBy(enemy => enemy.CurrentHp)
                .ThenBy(enemy => enemy.CombatId)
                .FirstOrDefault();

    public static IReadOnlyList<AbstractLiDaoXuYing> GetPermanentPhantoms(
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        return PileType.Hand.GetPile(owner).Cards
            .OfType<AbstractLiDaoXuYing>()
            .Where(phantom => !phantom.IsFullForcePhantom)
            .OrderBy(GetResolutionOrder)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();
    }

    public static int GetForceBase(Player owner) => Math.Min(
        6,
        GetPermanentPhantoms(owner).Sum(phantom =>
            phantom is BaiShouXuYing ? 2 : 1
        )
    );

    internal static async Task ActivateBeastGuAsync<TPhantom>(
        PlayerChoiceContext choiceContext,
        AbstractLiDaoGuCard sourceGu
    ) where TPhantom : AbstractLiDaoXuYing
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(sourceGu);

        TPhantom? existing = GetPermanentPhantoms(sourceGu.Owner)
            .OfType<TPhantom>()
            .FirstOrDefault();

        if (existing != null)
        {
            if (sourceGu.GuRank > existing.GuRank)
            {
                existing.InitializeGuRankFromSource(sourceGu.GuRank);
            }
            existing.Condense();
            LiDaoPowerSystem.NotifyCondensed(sourceGu.Owner.Creature);
            await EnsureControllerAsync(choiceContext, sourceGu);
            return;
        }

        await EnsureCapacityAsync(choiceContext, sourceGu);

        TPhantom phantom = GuGeneratedCardFactory.Create<TPhantom>(
            sourceGu.Owner,
            sourceGu.GuRank,
            upgraded: false
        );
        await GuGeneratedCardFactory.AddToHandOrDiscard(
            phantom,
            sourceGu.Owner
        );
        await EnsureControllerAsync(choiceContext, sourceGu);
    }

    internal static async Task ActivateBaiShouGuAsync(
        PlayerChoiceContext choiceContext,
        BaiShouLiGu sourceGu,
        IReadOnlyList<LiDaoBeastKind> composition
    )
    {
        BaiShouXuYing? existing = GetPermanentPhantoms(sourceGu.Owner)
            .OfType<BaiShouXuYing>()
            .FirstOrDefault();

        if (existing != null)
        {
            if (sourceGu.GuRank > existing.GuRank)
            {
                existing.InitializeGuRankFromSource(sourceGu.GuRank);
            }
            existing.ConfigureComposition(composition);
            existing.Condense();
            LiDaoPowerSystem.NotifyCondensed(sourceGu.Owner.Creature);
            await EnsureControllerAsync(choiceContext, sourceGu);
            return;
        }

        await EnsureCapacityAsync(choiceContext, sourceGu);
        BaiShouXuYing phantom =
            GuGeneratedCardFactory.Create<BaiShouXuYing>(
                sourceGu.Owner,
                sourceGu.GuRank,
                upgraded: false
            );
        phantom.ConfigureComposition(composition);
        await GuGeneratedCardFactory.AddToHandOrDiscard(
            phantom,
            sourceGu.Owner
        );
        await EnsureControllerAsync(choiceContext, sourceGu);
    }

    internal static async Task ActivateFullForceGuAsync(
        PlayerChoiceContext choiceContext,
        QuanLiYiFuGu sourceGu
    )
    {
        QuanLiXuYing? existing = PileType.Hand
            .GetPile(sourceGu.Owner)
            .Cards
            .OfType<QuanLiXuYing>()
            .FirstOrDefault();

        if (existing != null)
        {
            if (sourceGu.GuRank > existing.GuRank)
            {
                existing.InitializeGuRankFromSource(sourceGu.GuRank);
            }
        }
        else
        {
            QuanLiXuYing phantom =
                GuGeneratedCardFactory.Create<QuanLiXuYing>(
                    sourceGu.Owner,
                    sourceGu.GuRank,
                    upgraded: false
                );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                phantom,
                sourceGu.Owner
            );
        }

        await EnsureControllerAsync(choiceContext, sourceGu);
    }

    internal static async Task ResolveAttackAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries ||
            cardPlay.Card.Type != CardType.Attack ||
            cardPlay.Card.Keywords.Contains(GuZhenRenKeywords.XuYing) ||
            cardPlay.Card.Tags.Contains(GuZhenRenTags.XuYingCopy))
        {
            return;
        }

        Player owner = cardPlay.Player;
        IReadOnlyList<AbstractLiDaoXuYing> permanent =
            GetPermanentPhantoms(owner);
        QuanLiXuYing? fullForce = PileType.Hand.GetPile(owner).Cards
            .OfType<QuanLiXuYing>()
            .FirstOrDefault();

        OtherManifestedState.Value = false;
        try
        {
            if (fullForce != null)
            {
                await ResolveFullForceAsync(
                    choiceContext,
                    cardPlay,
                    fullForce,
                    permanent
                );
                return;
            }

            await ResolveNaturalAsync(
                choiceContext,
                cardPlay,
                permanent
            );
        }
        finally
        {
            OtherManifestedState.Value = false;
        }
    }

    private static async Task ResolveFullForceAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        QuanLiXuYing fullForce,
        IReadOnlyList<AbstractLiDaoXuYing> permanent
    )
    {
        List<AbstractLiDaoXuYing> selected = permanent.ToList();
        int maximum = QuanLiYiFuGu.ForcedPhantomLimitAtRank(
            fullForce.GuRank,
            selected.Count
        );

        if (QuanLiYiFuGu.UsesRandomSubsetAtRank(fullForce.GuRank))
        {
            selected = SelectRandomSubset(
                cardPlay.Player,
                selected,
                maximum,
                "li_dao/full_force"
            );
        }

        HashSet<LiDaoBeastKind> manifested = [];
        decimal multiplier =
            QuanLiYiFuGu.EffectPercentAtRank(fullForce.GuRank) / 100m;

        foreach (AbstractLiDaoXuYing phantom in selected)
        {
            OtherManifestedState.Value = manifested.Count > 0;
            bool triggered = await phantom.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: true,
                effectMultiplier: multiplier
            );
            if (!triggered)
            {
                continue;
            }

            await RecordManifestation(
                cardPlay.Player,
                phantom,
                manifested
            );
        }

        float chanceGain =
            QuanLiYiFuGu.PermanentChanceGainAtRank(fullForce.GuRank);
        if (chanceGain > 0f)
        {
            foreach (AbstractLiDaoXuYing phantom in permanent)
            {
                float capped = Math.Min(0.8f, phantom.BaseChance + chanceGain);
                phantom.IncreaseBaseChance(capped - phantom.BaseChance);
            }
        }

        int recoveryAcceleration =
            QuanLiYiFuGu.RecoveryAccelerationAtRank(fullForce.GuRank);
        if (recoveryAcceleration > 0)
        {
            int turn = cardPlay.Player.PlayerCombatState?.TurnNumber ?? 1;
            foreach (CardModel gu in GuCardPileSystem.RecoveryPileType
                         .GetPile(cardPlay.Player)
                         .Cards
                         .Where(card => card is ILiDaoBeastGuCard))
            {
                GuCardUsageRules.AccelerateRecoveryBy(
                    gu,
                    recoveryAcceleration,
                    turn
                );
            }
        }

        await CardPileCmd.RemoveFromCombat(
            fullForce,
            skipVisuals: false
        );
    }

    private static async Task ResolveNaturalAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        IReadOnlyList<AbstractLiDaoXuYing> permanent
    )
    {
        List<AbstractLiDaoXuYing> candidates = permanent.ToList();
        List<AbstractLiDaoXuYing> failures = [];
        HashSet<AbstractLiDaoXuYing> manifestedCards = [];
        HashSet<LiDaoBeastKind> manifestedKinds = [];
        bool naturalManifested = false;

        KuLiPower? kuLi = cardPlay.Player.Creature.GetPower<KuLiPower>();
        int turn = cardPlay.Player.PlayerCombatState?.TurnNumber ?? 1;

        if (candidates.Count > 0 &&
            kuLi?.TryClaimDesperationBurst(turn) == true)
        {
            AbstractLiDaoXuYing forced = SelectRandomSubset(
                cardPlay.Player,
                candidates,
                1,
                "li_dao/desperation"
            )[0];
            candidates.Remove(forced);
            bool triggered = await forced.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: true,
                effectMultiplier: 1m
            );
            if (triggered)
            {
                manifestedCards.Add(forced);
                await RecordManifestation(
                    cardPlay.Player,
                    forced,
                    manifestedKinds
                );
            }
        }

        foreach (AbstractLiDaoXuYing phantom in candidates)
        {
            OtherManifestedState.Value = manifestedCards.Count > 0;
            bool triggered = await phantom.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: false,
                effectMultiplier: 1m
            );
            if (!triggered)
            {
                failures.Add(phantom);
                continue;
            }

            manifestedCards.Add(phantom);
            naturalManifested = true;
            await RecordManifestation(
                cardPlay.Player,
                phantom,
                manifestedKinds
            );
        }

        if (manifestedCards.Count == 0 &&
            failures.Count > 0 &&
            kuLi?.CanRetryAllFailed == true)
        {
            AbstractLiDaoXuYing retry = SelectRandomSubset(
                cardPlay.Player,
                failures,
                1,
                "li_dao/retry"
            )[0];
            bool triggered = await retry.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: false,
                effectMultiplier: 1m
            );
            if (triggered)
            {
                manifestedCards.Add(retry);
                naturalManifested = true;
                await RecordManifestation(
                    cardPlay.Player,
                    retry,
                    manifestedKinds
                );
            }
        }

        if (naturalManifested &&
            kuLi?.CanCreateDoubleShadow == true)
        {
            AbstractLiDaoXuYing[] remaining = permanent
                .Where(phantom => !manifestedCards.Contains(phantom))
                .ToArray();
            if (remaining.Length > 0 &&
                RollPercent(cardPlay.Player, 30, "li_dao/double_shadow"))
            {
                AbstractLiDaoXuYing extra = SelectRandomSubset(
                    cardPlay.Player,
                    remaining.ToList(),
                    1,
                    "li_dao/double_shadow_pick"
                )[0];
                OtherManifestedState.Value = true;
                if (await extra.TriggerFromControllerAsync(
                        choiceContext,
                        cardPlay,
                        forced: true,
                        effectMultiplier: 1m
                    ))
                {
                    await RecordManifestation(
                        cardPlay.Player,
                        extra,
                        manifestedKinds
                    );
                }
            }
        }
    }

    private static async Task RecordManifestation(
        Player owner,
        AbstractLiDaoXuYing phantom,
        ISet<LiDaoBeastKind> manifestedKinds
    )
    {
        foreach (LiDaoBeastKind kind in phantom.LastManifestedKinds)
        {
            manifestedKinds.Add(kind);
            await LiDaoPowerSystem.NotifyManifested(owner.Creature, kind);
        }

        TurnManifestation turnState = TurnManifestations.GetValue(
            owner,
            static _ => new TurnManifestation()
        );
        turnState.Turn = owner.PlayerCombatState?.TurnNumber ?? 1;
        turnState.Any = true;
    }

    private static async Task EnsureCapacityAsync(
        PlayerChoiceContext choiceContext,
        AbstractGuZhenRenCard source
    )
    {
        IReadOnlyList<AbstractLiDaoXuYing> existing =
            GetPermanentPhantoms(source.Owner);
        if (existing.Sum(card => card.PhantomSlotCost) < 4)
        {
            return;
        }

        LocString prompt = new(
            "cards",
            "LU_GU_ZHEN_REN_CARD_LI_DAO_REPLACE_PHANTOM.selectionScreenPrompt"
        );
        CardSelectorPrefs prefs = new(prompt, 1)
        {
            Cancelable = false,
        };

        CardModel? selected = (
            await CardSelectCmd.FromHand(
                choiceContext,
                source.Owner,
                prefs,
                card => card is AbstractLiDaoXuYing phantom &&
                    !phantom.IsFullForcePhantom,
                source
            )
        ).FirstOrDefault();

        selected ??= existing.FirstOrDefault();
        if (selected != null)
        {
            await CardPileCmd.RemoveFromCombat(
                selected,
                skipVisuals: false
            );
        }
    }

    private static async Task EnsureControllerAsync(
        PlayerChoiceContext choiceContext,
        CardModel source
    )
    {
        if (source.Owner.Creature.GetPower<LiDaoBattlePower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<LiDaoBattlePower>(
            choiceContext,
            source.Owner.Creature,
            1,
            source.Owner.Creature,
            source
        );
    }

    private static List<AbstractLiDaoXuYing> SelectRandomSubset(
        Player owner,
        List<AbstractLiDaoXuYing> candidates,
        int count,
        string stream
    )
    {
        Rng rng = RitsuLibFramework.GetModPlayerRng(
            owner,
            Entry.ModId,
            stream
        );
        List<AbstractLiDaoXuYing> pool = candidates.ToList();
        List<AbstractLiDaoXuYing> selected = [];

        while (selected.Count < count && pool.Count > 0)
        {
            int index = rng.NextInt(pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return selected;
    }

    private static bool RollPercent(
        Player owner,
        int percent,
        string stream
    ) => RitsuLibFramework
        .GetModPlayerRng(owner, Entry.ModId, stream)
        .NextInt(100) < percent;

    private static int GetResolutionOrder(AbstractLiDaoXuYing phantom) =>
        phantom switch
        {
            BaiZhiXuYing => 0,
            FeiXiongXuYing => 1,
            EXuYing => 2,
            BaiShouXuYing => 3,
            ShiGuiXuYing => 4,
            QingNiuXuYing => 5,
            _ => 6,
        };
}
