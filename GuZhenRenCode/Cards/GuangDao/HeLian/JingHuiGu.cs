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
    typeof(JingGuangGu),
    typeof(DingGuangGu),
    MinimumMaterialRank = 3
)]
public sealed class JingHuiGu
    : AbstractHeLianGuCard,
      IGuRecoveryEffectSource
{
    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".jing_hui.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".jing_hui.pending_generation",
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
        new BlockVar(11m, ValueProp.Move),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public JingHuiGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Rare,
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

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        if (cardPlay.PlayIndex == 0)
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );

            if (GuRank >= 5)
            {
                await PowerCmd.Apply<JingHuiReservePower>(
                    choiceContext,
                    Owner.Creature,
                    1,
                    Owner.Creature,
                    this
                );
            }

            if (GuRank >= 8)
            {
                await PowerCmd.Apply<JingHuiBreakPower>(
                    choiceContext,
                    Owner.Creature,
                    1,
                    Owner.Creature,
                    this
                );
            }
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
            JingHui token = CreatePrimaryToken<JingHui>();
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
            ? CreatePrimaryToken<ZhouTianJingHui>()
            : GuRank >= 6
                ? CreatePrimaryToken<NingJingHui>()
                : CreatePrimaryToken<JingHui>();

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            primary,
            Owner
        );
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
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 3 => 11,
            4 => 13,
            5 => 15,
            6 => 18,
            7 => 20,
            8 => 23,
            _ => 27,
        };
        DynamicVars[typeof(ZhaoPoPower).Name].BaseValue = GuRank switch
        {
            <= 4 => 1,
            <= 6 => 2,
            <= 8 => 3,
            _ => 4,
        };
    }
}
