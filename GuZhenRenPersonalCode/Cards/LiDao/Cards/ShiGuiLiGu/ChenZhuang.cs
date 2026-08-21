// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“沉桩”。
// 主要类型：ChenZhuang。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenZhuang : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(ShiGuiLiGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upAttackBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("AttackBonus", 0m),
        new DynamicVar("NoBlockBonus", 0m),
    ];

    public ChenZhuang() : base(CardType.Skill, TargetType.Self) =>
        RefreshRankValues();

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 2 => 8,
        3 => 10,
        4 => 11,
        _ => 12,
    };

    private static int AttackBonusAtRank(int rank) => rank switch
    {
        4 => 3,
        >= 5 => 4,
        _ => 0,
    };

    private static int NoBlockBonusAtRank(int rank) => rank >= 5 ? 2 : 0;

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue = BlockAtRank(GuRank) + _upBlock;
        DynamicVars["AttackBonus"].BaseValue =
            AttackBonusAtRank(GuRank) + _upAttackBonus;
        DynamicVars["NoBlockBonus"].BaseValue =
            NoBlockBonusAtRank(GuRank);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("AttackBonusRange", GuRank >= 4 ? 1 : 0);
        description.Add("NoBlockRange", GuRank >= 5 ? 1 : 0);
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        decimal block = DynamicVars.Block.BaseValue;
        if (GuRank >= 4 && PlayedAttackEarlierThisTurn())
        {
            block += DynamicVars["AttackBonus"].BaseValue;
        }
        if (GuRank >= 5 && Owner.Creature.Block <= 0)
        {
            block += DynamicVars["NoBlockBonus"].BaseValue;
        }

        return CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upAttackBonus += 1m;
        RefreshRankValues();
    }
}
