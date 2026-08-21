// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“牛角顶”。
// 主要类型：NiuJiaoDing。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
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
