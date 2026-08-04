using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class DingGuangGu
    : AbstractGuWormCard,
      IGuRecoveryEffectSource
{
    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".ding_guang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".ding_guang.pending_generation",
            static () => false
        );

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ZhaoPoPower>(1m),
        new DynamicVar("ExposeBonus", 2m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public DingGuangGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Uncommon,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
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

        if (cardPlay.PlayIndex == 0)
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );

            if (GuRank >= 2)
            {
                await PowerCmd.Apply<DingGuangChargePower>(
                    choiceContext,
                    Owner.Creature,
                    DynamicVars["ExposeBonus"].IntValue,
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
            DingGuangFu token = CreateToken<DingGuangFu>();
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
        AbstractGuZhenRenCard token = GuRank >= 9
            ? CreateToken<RiYun>()
            : CreateToken<DingGuangFu>();

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            token,
            Owner
        );
    }

    public Task OnRecoveredAsync()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        return Task.CompletedTask;
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    public override IReadOnlyList<CardModel> GetCarouselCards()
    {
        if (GuRank < 3)
        {
            return [];
        }

        bool upgraded = IsUpgraded || GuRank >= 6;
        if (GuRank >= 9)
        {
            return
            [
                GuCardReferenceFactory.Create<RiYun>(
                    this,
                    upgraded
                ),
            ];
        }

        List<CardModel> cards =
        [
            GuCardReferenceFactory.Create<DingGuangFu>(
                this,
                upgraded
            ),
        ];

        if (GuRank >= 7)
        {
            cards.Add(
                GuCardReferenceFactory.Create<GuangBiao>(this)
            );
        }

        return cards;
    }

    private T CreateToken<T>() where T : AbstractGuZhenRenCard
    {
        return GuGeneratedCardFactory.Create<T>(
            Owner,
            GuRank,
            upgraded: IsUpgraded || GuRank >= 6
        );
    }

    private void RefreshRankValues()
    {
        DynamicVars[typeof(ZhaoPoPower).Name].BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 5 => 2,
            <= 7 => 3,
            8 => 4,
            _ => 5,
        };
        DynamicVars["ExposeBonus"].BaseValue = GuRank >= 6 ? 4 : 2;
    }
}
