// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“天月芒”。
// 主要类型：TianYueMang。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class TianYueMang : AbstractYueMangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar("Hits", 4),
        new DynamicVar("DoubleRefractionBonus", 12m),
        new PowerVar<ZhaoPoPower>(2m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(global::GuZhenRen.Cards.GuZhenRenKeywords.ZheGuangCore)
            .Distinct();

    public TianYueMang() : base(2)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        bool doubleRefraction = Owner.Creature.GetPower<ZheGuangPower>()?
            .GuangHuiGainedThisTurn >= 2;
        await AttackMany(
            choiceContext,
            cardPlay,
            target,
            DynamicVars["Hits"].IntValue,
            DynamicVars.Damage.BaseValue
        );

        if (doubleRefraction && !target.IsDead)
        {
            await DamageCmd
                .Attack(DynamicVars["DoubleRefractionBonus"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
