using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Cards.Interfaces;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.TuDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 1)]
public sealed class YuPiGu
    : AbstractGuWormCard,
      ICarouselCard,
      IGuRecoveryEffectSource
{
    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".yu_pi.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".yu_pi.pending_generation",
            static () => false
        );

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<CardTag> AdditionalCanonicalTags =>
        GuRank >= 5
            ? [GuZhenRenTags.GuangDao]
            : [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(8m, ValueProp.Move)];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public YuPiGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Common,
            target: TargetType.Self
        )
    {
        SetDao(Dao.TuDao);
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
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        // 五转开始以玉面反光支持光道，但不提供永久敏捷。
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
            YuMo membrane = CreatePrimaryToken<YuMo>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                membrane,
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

        AbstractGuZhenRenCard primary = GuRank switch
        {
            >= 9 => CreatePrimaryToken<LiuLiYuYi>(),
            >= 6 => CreatePrimaryToken<YuGuangYi>(),
            _ => CreatePrimaryToken<YuMo>(),
        };

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            primary,
            Owner
        );

        if (GuRank >= 8)
        {
            ZheGuang reflected = CreatePrimaryToken<ZheGuang>();
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                reflected,
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

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    public IReadOnlyList<CardModel> GetCarouselCards()
    {
        if (GuRank < 3)
        {
            return [];
        }

        CardModel previewModel = GuRank switch
        {
            >= 9 => ModelDb.Card<LiuLiYuYi>().ToMutable(),
            >= 6 => ModelDb.Card<YuGuangYi>().ToMutable(),
            _ => ModelDb.Card<YuMo>().ToMutable(),
        };

        if (previewModel is not AbstractGuZhenRenCard preview)
        {
            return [];
        }

        preview.InitializeGuRankFromSource(GuRank);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(preview, CardPreviewStyle.None);
        }

        return [preview];
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
            1 => 8,
            2 => 10,
            3 => 11,
            4 => 13,
            5 => 15,
            6 => 18,
            7 => 21,
            8 => 24,
            _ => 28,
        };
    }
}
