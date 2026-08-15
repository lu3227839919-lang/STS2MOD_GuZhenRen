using System.Runtime.CompilerServices;

using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QuanLiYiFuGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upVigor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<VigorPower>(3m),
    ];

    public YunLi() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.YunLiBlock(GuRank) + _upBlock;
        DynamicVars[typeof(VigorPower).Name].BaseValue =
            LiDaoCompanionRankTable.YunLiVigor(GuRank) + _upVigor;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add(
            "PhantomVigorBasicRange",
            rank is >= 6 and <= 7 ? 1 : 0
        );
        description.Add("PhantomVigorTwoRange", rank >= 8 ? 1 : 0);
        description.Add(
            "PhantomVigorBonus",
            LiDaoCompanionRankTable.YunLiPhantomVigorBonus(
                rank,
                PermanentPhantomKinds
            )
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        decimal block = DynamicVars.Block.BaseValue;
        decimal vigor = DynamicVars[typeof(VigorPower).Name].BaseValue;

        int rank = GuRank;
        vigor += LiDaoCompanionRankTable.YunLiPhantomVigorBonus(
            rank,
            PermanentPhantomKinds
        );

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            vigor,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upVigor += 2m;
        RefreshRankValues();
    }
}
