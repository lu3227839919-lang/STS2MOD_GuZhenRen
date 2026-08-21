// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“三更蛊”。
// 主要类型：SanGengGu。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ZhouDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class SanGengGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int RecoveryDelayTurns => GuRank <= 6 ? 3 : 4;

    public SanGengGu() : base(CardRarity.Rare)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        (int energy, int draw, int years, int bonusEvents, int hpLoss) =
            GuRank switch
            {
                5 => (1, 1, 1, 2, 4),
                6 => (1, 1, 2, 2, 5),
                7 => (1, 2, 2, 3, 6),
                8 => (2, 2, 2, 3, 7),
                _ => (2, 2, 3, 3, 8),
            };

        description.Add("EnergyGain", energy);
        description.Add("DrawCount", draw);
        description.Add("NianHuaGain", years);
        description.Add("BonusEvents", bonusEvents);
        description.Add("HpLoss", hpLoss);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        (int energy, int draw, int years, int bonusEvents, int hpLoss) =
            GuRank switch
            {
                5 => (1, 1, 1, 2, 4),
                6 => (1, 1, 2, 2, 5),
                7 => (1, 2, 2, 3, 6),
                8 => (2, 2, 2, 3, 7),
                _ => (2, 2, 3, 3, 8),
            };

        await PlayerCmd.GainEnergy(energy, Owner);
        await CardPileCmd.Draw(choiceContext, draw, Owner);
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            Owner,
            years,
            this,
            allowSanGengBonus: false
        );
        await PowerCmd.Apply<SanGengPower>(
            choiceContext,
            Owner.Creature,
            bonusEvents,
            Owner.Creature,
            this
        );
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            this,
            cardPlay
        );
    }
}
