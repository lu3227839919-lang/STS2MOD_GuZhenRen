// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“回溯蛊”。
// 主要类型：HuiSuGu。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：战斗衍生牌必须由当前 CombatState 创建，确保网络卡号和牌堆归属有效。
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
public sealed class HuiSuGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int RecoveryDelayTurns => GuRank <= 7 ? 3 : 4;

    public HuiSuGu() : base(CardRarity.Rare)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        int inspect = GuRank switch
        {
            5 => 2,
            6 or 7 => 3,
            8 => 4,
            _ => 5,
        };
        int delta = GuRank switch
        {
            <= 6 => 1,
            <= 8 => 0,
            _ => -1,
        };

        description.Add("InspectCount", inspect);
        description.Add("CostHigher", delta > 0 ? 1 : 0);
        description.Add("CostSame", delta == 0 ? 1 : 0);
        description.Add("CostLower", delta < 0 ? 1 : 0);
        description.Add("XiYingGain", GuRank >= 9 ? 2 : 1);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int inspect = GuRank switch
        {
            5 => 2,
            6 or 7 => 3,
            8 => 4,
            _ => 5,
        };

        if (CombatState == null)
        {
            return;
        }

        CardModel[] candidates = CombatManager.Instance.History
            .CardPlaysFinished
            .OfType<CardPlayFinishedEntry>()
            .Where(entry =>
                entry.HappenedThisTurn(CombatState) &&
                entry.CardPlay.Player == Owner)
            .Select(entry => entry.CardPlay.Card)
            .Where(IsLegalBacktrackTarget)
            .Reverse()
            .DistinctBy(card => card.Id)
            .Take(inspect)
            .ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        CardModel source;
        if (candidates.Length == 1)
        {
            source = candidates[0];
        }
        else
        {
            IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1)
                {
                    Cancelable = false,
                    PretendCardsCanBePlayed = true,
                }
            );
            source = selected.FirstOrDefault() ?? candidates[0];
        }

        CardModel copy = CombatState.CreateCard(
            source.CanonicalInstance,
            Owner
        );
        for (int index = 0; index < source.CurrentUpgradeLevel; index++)
        {
            CardCmd.Upgrade(copy);
        }

        int delta = GuRank switch
        {
            <= 6 => 1,
            <= 8 => 0,
            _ => -1,
        };
        int baseCost = source.EnergyCost.GetWithModifiers(CostModifiers.None);
        copy.EnergyCost.SetCustomBaseCost(Math.Max(0, baseCost + delta));
        copy.AddKeyword(CardKeyword.Exhaust);
        copy.AddKeyword(GuZhenRenKeywords.XiYing);
        ZhouDaoCardState.MarkXiYing(copy, GuRank >= 9 ? 2 : 1);

        if (Owner.Creature.GetPower<XiYingWatcherPower>() == null)
        {
            await PowerCmd.Apply<XiYingWatcherPower>(
                choiceContext,
                Owner.Creature,
                1,
                Owner.Creature,
                this,
                silent: true
            );
        }

        await GuGeneratedCardFactory.AddToHandOrDiscard(copy, Owner);
    }

    private static bool IsLegalBacktrackTarget(CardModel card) =>
        card.DeckVersion != null &&
        card is not IGuWormCard &&
        !ZhouDaoCardState.IsXiYing(card) &&
        card.Type is CardType.Attack or CardType.Skill &&
        !card.EnergyCost.CostsX;
}
