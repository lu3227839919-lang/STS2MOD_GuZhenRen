using System.Threading;

using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
        int maximum = fullForce.GuRank switch
        {
            <= 1 => Math.Min(1, selected.Count),
            2 => Math.Min(2, selected.Count),
            _ => selected.Count,
        };

        if (fullForce.GuRank <= 2)
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
            LiDaoRankTable.FullForcePercent(fullForce.GuRank) / 100m;

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

        if (fullForce.GuRank >= 8)
        {
            float chanceGain = fullForce.GuRank >= 9 ? 0.05f : 0.03f;
            foreach (AbstractLiDaoXuYing phantom in permanent)
            {
                float capped = Math.Min(0.8f, phantom.BaseChance + chanceGain);
                phantom.IncreaseBaseChance(capped - phantom.BaseChance);
            }
        }

        if (fullForce.GuRank >= 9)
        {
            int turn = cardPlay.Player.PlayerCombatState?.TurnNumber ?? 1;
            foreach (CardModel gu in GuCardPileSystem.RecoveryPileType
                         .GetPile(cardPlay.Player)
                         .Cards
                         .Where(card => card is ILiDaoBeastGuCard))
            {
                GuCardUsageRules.AccelerateRecoveryBy(gu, 1, turn);
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
            "GU_ZHEN_REN_CARD_LI_DAO_REPLACE_PHANTOM.selectionScreenPrompt"
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
