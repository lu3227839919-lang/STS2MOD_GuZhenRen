using GuZhenRen.Characters;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class DaoChiXueFuGu : AbstractGuWormCard
{
    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    public DaoChiXueFuGu()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (XueDaoCardSystem.CountRemains(Owner) < 2)
        {
            DaoChiXueFu normal =
                GuGeneratedCardFactory.Create<DaoChiXueFu>(
                    Owner,
                    GuRank,
                    upgraded: false
                );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                normal,
                Owner
            );
            return;
        }

        // 选择界面使用未登记进 CombatState 的预览模型，避免未选中的
        // 正式战斗牌残留为不可见孤儿牌。
        DaoChiXueFu normalPreview =
            GuCardReferenceFactory.Create<DaoChiXueFu>(
                this,
                false
            );
        DaoChiXueFuQun swarmPreview =
            GuCardReferenceFactory.Create<DaoChiXueFuQun>(
                this,
                false
            );

        // 这些预览不会登记进 CombatState，但战斗中的简单选卡界面会从
        // 候选牌 Owner 初始化战斗牌堆，因此必须保留当前玩家归属。
        normalPreview.Owner = Owner;
        swarmPreview.Owner = Owner;

        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            [normalPreview, swarmPreview],
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1)
            {
                Cancelable = false,
                PretendCardsCanBePlayed = true,
            }
        )).FirstOrDefault();

        bool useSwarm = selected is DaoChiXueFuQun;
        if (useSwarm)
        {
            int consumed = await XueDaoCardSystem.ConsumeOldestRemains(
                choiceContext,
                Owner,
                2
            );

            // 多人状态极端不同步或其他效果抢先消耗遗骸时，安全降级为
            // 基础血蝠，避免无成本获得血蝠群。
            if (consumed != 2)
            {
                Entry.Logger.Warn(
                    "[刀翅血蝠蛊] 选择血蝠群后遗骸不足，已降级为基础血蝠。"
                );
                useSwarm = false;
            }
        }

        AbstractGuZhenRenCard generated = useSwarm
            ? GuGeneratedCardFactory.Create<DaoChiXueFuQun>(
                Owner,
                GuRank,
                upgraded: false
            )
            : GuGeneratedCardFactory.Create<DaoChiXueFu>(
                Owner,
                GuRank,
                upgraded: false
            );

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            generated,
            Owner
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
    [
        GuCardReferenceFactory.Create<DaoChiXueFu>(this, false),
        GuCardReferenceFactory.Create<DaoChiXueFuQun>(this, false),
    ];
}
