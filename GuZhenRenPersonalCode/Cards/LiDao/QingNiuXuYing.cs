using System.Runtime.CompilerServices;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class QingNiuXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.QingNiu;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new BlockVar(2m, ValueProp.Move),
        new DynamicVar("HitBlockBonus", 0m),
    ];

    public QingNiuXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "HitBlockTotal",
            DynamicVars.Block.IntValue + DynamicVars["HitBlockBonus"].IntValue
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.QingNiu,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(QingNiuLaoLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            QingNiuLaoLiGu.DamageAtRank(GuRank);
        DynamicVars.Block.BaseValue = QingNiuLaoLiGu.BlockAtRank(GuRank);
        DynamicVars["HitBlockBonus"].BaseValue =
            QingNiuLaoLiGu.HitBlockBonusAtRank(GuRank);
    }
}
