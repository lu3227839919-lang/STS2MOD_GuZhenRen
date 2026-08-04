using System.Runtime.CompilerServices;

using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class XiaoGuangGu
    : AbstractGuWormCard,
      IGuRecoveryEffectSource
{
    private sealed class TurnGainState
    {
        public ICombatState? CombatState;
        public int TurnNumber;
        public bool Claimed;
    }

    private static readonly ConditionalWeakTable<
        Player,
        TurnGainState
    > TurnGainStates = new();

    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".xiao_guang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".xiao_guang.pending_generation",
            static () => false
        );

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 2 => 1,
        <= 7 => 2,
        _ => 3,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("GuangHui", 1m),
        new DynamicVar("FocusBonus", 1m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public XiaoGuangGu()
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
        if (cardPlay.PlayIndex != 0)
        {
            return;
        }

        if (GuRank >= 6)
        {
            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                DynamicVars["GuangHui"].IntValue
            );
            return;
        }

        bool firstThisTurn = TryClaimFirstGainThisTurn(Owner);
        if (firstThisTurn)
        {
            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                1
            );
        }

        if (GuRank >= 2 || !firstThisTurn)
        {
            await PowerCmd.Apply<JuGuangPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["FocusBonus"].IntValue,
                Owner.Creature,
                this
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
            WeiGuang card = CreateRecoveryCard<WeiGuang>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                card,
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

        AbstractGuZhenRenCard generated = GuRank switch
        {
            >= 9 => CreateRecoveryCard<JiGuang>(),
            >= 6 => CreateRecoveryCard<JuGuang>(),
            _ => CreateRecoveryCard<WeiGuang>(),
        };

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            generated,
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
        return GuRank switch
        {
            < 3 => [],
            <= 5 =>
            [
                GuCardReferenceFactory.Create<WeiGuang>(
                    this,
                    IsUpgraded
                ),
            ],
            <= 7 =>
            [
                GuCardReferenceFactory.Create<JuGuang>(
                    this,
                    IsUpgraded
                ),
            ],
            8 =>
            [
                GuCardReferenceFactory.Create<JuGuang>(
                    this,
                    IsUpgraded
                ),
                GuCardReferenceFactory.Create<YuHui>(this),
            ],
            _ =>
            [
                GuCardReferenceFactory.Create<JiGuang>(
                    this,
                    IsUpgraded
                ),
            ],
        };
    }

    private T CreateRecoveryCard<T>()
        where T : AbstractGuZhenRenCard
    {
        return GuGeneratedCardFactory.Create<T>(
            Owner,
            GuRank,
            upgraded: IsUpgraded
        );
    }

    private static bool TryClaimFirstGainThisTurn(Player owner)
    {
        ICombatState? combatState = owner.Creature.CombatState;
        int turnNumber = owner.PlayerCombatState?.TurnNumber ?? 0;
        TurnGainState state = TurnGainStates.GetValue(
            owner,
            static _ => new TurnGainState()
        );

        if (!ReferenceEquals(state.CombatState, combatState) ||
            state.TurnNumber != turnNumber)
        {
            state.CombatState = combatState;
            state.TurnNumber = turnNumber;
            state.Claimed = false;
        }

        if (state.Claimed)
        {
            return false;
        }

        state.Claimed = true;
        return true;
    }

    private void RefreshRankValues()
    {
        DynamicVars["GuangHui"].BaseValue = GuRank >= 6 ? 2 : 1;
        DynamicVars["FocusBonus"].BaseValue = GuRank switch
        {
            <= 1 => 1,
            <= 5 => 2,
            <= 7 => 4,
            _ => 6,
        };
    }
}
