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
public sealed class FeiXiongXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.FeiXiong;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "BlockedDamage",
            DynamicVars.Damage.IntValue + DynamicVars["BlockBonus"].IntValue
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.FeiXiong,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(FeiXiongZhiLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            FeiXiongZhiLiGu.DamageAtRank(GuRank);
        DynamicVars["BlockBonus"].BaseValue =
            FeiXiongZhiLiGu.BlockedTargetBonusAtRank(GuRank);
    }
}
