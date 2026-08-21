// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“血颅蛊”。
// 主要类型：XueLuGu。
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
public sealed class XueLuGu : AbstractGuWormCard
{
    private const string MaxAbsorbVar = "MaxAbsorb";
    private const int OverflowHealPerRemains = 3;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 7 ? 4 : 3;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar(MaxAbsorbVar, 1m),
        new HealVar(OverflowHealPerRemains),
    ];

    public XueLuGu()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        int consumed = await XueDaoCardSystem.ConsumeOldestRemains(
            choiceContext,
            Owner,
            DynamicVars[MaxAbsorbVar].IntValue
        );

        if (consumed <= 0)
        {
            return;
        }

        (_, int overflow) = await XueDaoPowerSystem.GainXueLuOrOverflow(
            choiceContext,
            this,
            consumed
        );

        if (overflow > 0)
        {
            await CreatureCmd.Heal(
                Owner.Creature,
                overflow * OverflowHealPerRemains
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
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 1 => 6,
            2 => 7,
            3 => 9,
            4 => 11,
            5 => 13,
            6 => 15,
            7 => 18,
            8 => 21,
            _ => 24,
        };
        DynamicVars[MaxAbsorbVar].BaseValue = GetMaximumAbsorb();
    }

    private int GetMaximumAbsorb() =>
        GuRank switch
        {
            <= 3 => 1,
            <= 6 => 2,
            _ => 3,
        };
}
