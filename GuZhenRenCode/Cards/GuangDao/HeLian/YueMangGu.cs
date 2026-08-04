using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(YueGuangGu),
    typeof(XiaoGuangGu),
    typeof(XiaoGuangGu),
    MinimumMaterialRank = 3
)]
public sealed class YueMangGu
    : AbstractHeLianGuCard,
      IGuRecoveryEffectSource
{
    private const int GuangHuiCost = 2;

    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".yue_mang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".yue_mang.pending_generation",
            static () => false
        );

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new RepeatVar("Hits", 2),
        new DynamicVar("EmpoweredHits", 1m),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public YueMangGu()
        : base(
            baseCost: 0,
            type: CardType.Attack,
            rarity: CardRarity.Uncommon,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        SetGuRank(3);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("GuangHuiCost", GuangHuiCost);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        bool empowered = GuRank >= 5 &&
            await GuangDaoPowerSystem.TrySpendGuangHui(
                choiceContext,
                this,
                cardPlay,
                GuangHuiCost
            );

        int hitCount = DynamicVars["Hits"].IntValue +
            (empowered
                ? DynamicVars["EmpoweredHits"].IntValue
                : 0);

        for (int hit = 0; hit < hitCount; hit++)
        {
            await DamageCmd
                .Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (target.IsDead)
            {
                break;
            }
        }

        if (empowered && cardPlay.PlayIndex == 0 && !target.IsDead)
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );
        }
    }

    public void ResetRecoveryEffectState()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
    }

    public async Task OnEnteredRecoveryAsync()
    {
        if (RecoveryHandledState[this])
        {
            return;
        }

        RecoveryHandledState[this] = true;
        if (GuRank < 3)
        {
            return;
        }

        if (GuRank == 3)
        {
            YueMang token = CreatePrimaryToken<YueMang>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                token,
                Owner
            );
            return;
        }

        PendingGenerationState[this] = true;
    }

    public async Task OnRecoveryTurnStartAsync(int turnNumber)
    {
        if (!PendingGenerationState[this])
        {
            return;
        }

        PendingGenerationState[this] = false;
        AbstractGuZhenRenCard primary = GuRank >= 9
            ? CreatePrimaryToken<TianYueMang>()
            : GuRank >= 6
                ? CreatePrimaryToken<NingYueMang>()
                : CreatePrimaryToken<YueMang>();

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            primary,
            Owner
        );

        if (GuRank == 8)
        {
            NingYueMang second = CreatePrimaryToken<NingYueMang>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                second,
                Owner
            );
        }
    }

    public Task OnRecoveredAsync()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        return Task.CompletedTask;
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        return materials
            .OfType<IGuRankProvider>()
            .Select(provider => provider.GuRank)
            .DefaultIfEmpty(3)
            .Min();
    }

    protected override void OnHeLianCompleted(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.All(card => card.IsUpgraded) && !IsUpgraded)
        {
            CardCmd.Upgrade(this);
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private T CreatePrimaryToken<T>()
        where T : AbstractGuZhenRenCard
    {
        return GuGeneratedCardFactory.Create<T>(
            Owner,
            GuRank,
            upgraded: IsUpgraded
        );
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 3 => 4,
            4 => 5,
            5 => 5,
            6 => 6,
            7 => 7,
            8 => 7,
            _ => 8,
        };
        DynamicVars["Hits"].BaseValue = GuRank switch
        {
            <= 4 => 2,
            <= 7 => 3,
            _ => 4,
        };
        DynamicVars["EmpoweredHits"].BaseValue = GuRank >= 9
            ? 2
            : 1;
    }
}
