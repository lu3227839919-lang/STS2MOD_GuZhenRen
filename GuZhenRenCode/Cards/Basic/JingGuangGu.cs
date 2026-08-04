using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class JingGuangGu
    : AbstractGuWormCard,
      IGuRecoveryEffectSource
{
    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".jing_guang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".jing_guang.pending_generation",
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
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("AttackSequenceBonus", 2m),
    ];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YuPiGu.png"
    );

    public JingGuangGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Common,
            target: TargetType.Self
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
        decimal block = DynamicVars.Block.BaseValue;
        if (GuRank >= 2 &&
            Owner.Creature.GetPower<ZheGuangPower>()?
                .PreviousCardWas(CardType.Attack) == true)
        {
            block += DynamicVars["AttackSequenceBonus"].BaseValue;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );

        if (GuRank >= 5 && cardPlay.PlayIndex == 0)
        {
            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                1
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
            GuangJing token = CreateToken<GuangJing>();
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
            ? CreateToken<MingJing>()
            : CreateToken<GuangJing>();

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
        DynamicVars.Block.BaseValue = GuRank switch
        {
            1 => 8,
            2 => 9,
            3 => 10,
            4 => 12,
            5 => 14,
            6 => 17,
            7 => 19,
            8 => 22,
            _ => 26,
        };
    }
}
