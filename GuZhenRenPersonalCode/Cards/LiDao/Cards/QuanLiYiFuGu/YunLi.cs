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

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 5, 2 => 6, 3 => 6, 4 => 7, 5 => 8,
        6 => 9, 7 => 10, 8 => 11, _ => 12,
    };

    private static int VigorAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        <= 4 => 4,
        <= 6 => 5,
        <= 8 => 6,
        _ => 7,
    };

    private static int PhantomVigorBonusAtRank(int rank, int phantomKinds) =>
        rank switch
        {
            6 or 7 => phantomKinds >= 1 ? 1 : 0,
            >= 8 => phantomKinds >= 2 ? 2 : 0,
            _ => 0,
        };

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank) + _upBlock;
        DynamicVars[typeof(VigorPower).Name].BaseValue =
            VigorAtRank(GuRank) + _upVigor;
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
            PhantomVigorBonusAtRank(rank, PermanentPhantomKinds)
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
        vigor += PhantomVigorBonusAtRank(rank, PermanentPhantomKinds);

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
