// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“飞熊撞”。
// 主要类型：FeiXiongZhuang。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FeiXiongZhuang : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(FeiXiongZhiLiGu);

    private decimal _upDamage;
    private decimal _upBlockBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongZhuang() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 9,
        3 => 10,
        4 => 12,
        _ => 13,
    };

    private static int BlockBonusAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        3 => 4,
        4 => 5,
        _ => 6,
    };

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank) + _upDamage;
        DynamicVars["BlockBonus"].BaseValue =
            BlockBonusAtRank(GuRank) + _upBlockBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "BlockedDamage",
            DynamicVars.Damage.IntValue + DynamicVars["BlockBonus"].IntValue
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        decimal damage = DynamicVars.Damage.BaseValue;
        if (target.Block > 0)
        {
            damage += DynamicVars["BlockBonus"].BaseValue;
        }

        return DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        _upBlockBonus += 2m;
        RefreshRankValues();
    }
}
