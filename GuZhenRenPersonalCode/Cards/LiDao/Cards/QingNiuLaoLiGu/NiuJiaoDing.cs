using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NiuJiaoDing : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(QingNiuLaoLiGu);
    public override bool GainsBlock => true;

    private decimal _upDamage;
    private decimal _upBlock;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new BlockVar(3m, ValueProp.Move),
    ];

    public NiuJiaoDing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 6,
        3 => 7,
        4 => 8,
        _ => 9,
    };

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        3 => 4,
        4 => 5,
        _ => 6,
    };

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank) + _upDamage;
        DynamicVars.Block.BaseValue = BlockAtRank(GuRank) + _upBlock;
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block.BaseValue,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upDamage += 2m;
        _upBlock += 2m;
        RefreshRankValues();
    }
}
