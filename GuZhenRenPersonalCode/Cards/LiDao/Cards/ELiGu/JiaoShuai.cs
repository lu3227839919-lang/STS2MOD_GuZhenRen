// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“绞摔”。
// 主要类型：JiaoShuai。
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
public sealed class JiaoShuai : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(ELiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
        new DynamicVar("SecondHitBonus", 0m),
    ];

    public JiaoShuai() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 4,
        3 or 4 => 5,
        _ => 6,
    };

    private static int SecondHitBonusAtRank(int rank) =>
        rank >= 4 ? 2 : 0;

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank) + _upDamage;
        DynamicVars["Hits"].BaseValue = 2m;
        DynamicVars["SecondHitBonus"].BaseValue =
            SecondHitBonusAtRank(GuRank);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("SecondHitRange", GuRank >= 4 ? 1 : 0);
        description.Add(
            "SecondHitBonus",
            SecondHitBonusAtRank(GuRank)
        );
        description.Add(
            "SecondHitDamage",
            DynamicVars.Damage.IntValue + SecondHitBonusAtRank(GuRank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        for (int hit = 0; hit < 2 && target.IsAlive; hit++)
        {
            decimal damage = DynamicVars.Damage.BaseValue;
            if (hit == 1)
            {
                damage += SecondHitBonusAtRank(GuRank);
            }

            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 1m;
        RefreshRankValues();
    }
}
