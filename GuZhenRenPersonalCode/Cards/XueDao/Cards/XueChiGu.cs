// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“血池蛊”。
// 主要类型：XueChiGu。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：升转、复制或读档后通过 OnGuRankChanged 重算品阶相关数值。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueChiGu : AbstractGuWormCard
{
    private const string OverflowBlockVar = "OverflowBlock";

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<XueYuanPower>(2m),
        new DynamicVar(OverflowBlockVar, 3m),
    ];

    public XueChiGu()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int requested = DynamicVars[typeof(XueYuanPower).Name].IntValue;
        int gained = await XueDaoPowerSystem.GainXueYuan(
            choiceContext,
            this,
            requested
        );
        int overflow = Math.Max(0, requested - gained);

        if (overflow > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                overflow * DynamicVars[OverflowBlockVar].BaseValue,
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars[typeof(XueYuanPower).Name].BaseValue = GuRank switch
        {
            <= 2 => 2,
            <= 4 => 3,
            <= 6 => 4,
            <= 8 => 5,
            _ => 6,
        };
        DynamicVars[OverflowBlockVar].BaseValue = GuRank switch
        {
            <= 3 => 3,
            <= 6 => 4,
            <= 8 => 5,
            _ => 6,
        };
    }
}
