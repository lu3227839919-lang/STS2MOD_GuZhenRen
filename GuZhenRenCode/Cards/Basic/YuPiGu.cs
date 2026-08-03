using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Cards.Interfaces;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.TuDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class YuPiGu
    : AbstractGuWormCard,
      ICarouselCard
{
    public override int MaxUses => IsUpgraded ? 2 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DexterityPower>(1m),
        new BlockVar(0m, ValueProp.Move),
    ];

    public override bool GainsBlock =>
        DynamicVars.Block.BaseValue > 0;

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/YuPiGu.png"
        );

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

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        XuanYuZhang generated =
            (XuanYuZhang)combatState.CreateCard(
                ModelDb.Card<XuanYuZhang>(),
                Owner
            );

        if (IsUpgraded)
        {
            CardCmd.Upgrade(generated);
        }

        generated.InitializeGuRankFromSource(
            Math.Min(9, GuRank + 1)
        );
        await GuCardPileSystem.AddGeneratedCardToHand(
            generated,
            Owner
        );

        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Dexterity.IntValue,
            Owner.Creature,
            this
        );

        if (DynamicVars.Block.BaseValue > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                DynamicVars.Block,
                cardPlay
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    public IReadOnlyList<CardModel> GetCarouselCards()
    {
        XuanYuZhang preview =
            (XuanYuZhang)ModelDb.Card<XuanYuZhang>().ToMutable();

        if (IsUpgraded)
        {
            CardCmd.Upgrade(preview, CardPreviewStyle.None);
        }

        preview.InitializeGuRankFromSource(
            Math.Min(9, GuRank + 1)
        );

        return [preview];
    }

    private void RefreshRankValues()
    {
        DynamicVars.Dexterity.BaseValue =
            1 + Math.Max(0, (GuRank - 1) / 2);
        DynamicVars.Block.BaseValue = GuRank >= 6
            ? 8 + (GuRank - 6) * 2
            : 0;
    }
}
