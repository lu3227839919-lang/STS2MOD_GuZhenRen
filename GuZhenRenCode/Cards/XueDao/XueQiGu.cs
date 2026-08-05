using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 血气蛊：一至九转的持续寄生蛊。按转数支付血元，不足部分以
/// 生命代付；高转血气会跟随宿主跨牌堆并触发多次。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueQiGu : AbstractGuWormCard
{
    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        >= 9 => 4,
        >= 5 => 3,
        _ => 2,
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
        [
            GuZhenRenKeywords.XueYuan,
            GuZhenRenKeywords.XueJi,
            GuZhenRenKeywords.XueQi,
            GuZhenRenKeywords.PoTai,
            GuZhenRenKeywords.FuHua,
            GuZhenRenKeywords.YiHai,
            GuZhenRenKeywords.LiuXue,
        ]).Distinct();

    protected override bool IsPlayable =>
        base.IsPlayable &&
        (IsCanonical || (HasEligibleHost() && CanPayCost()));

    public XueQiGu()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        int[] rates = XueDaoParasiteSystem.GetBloodQiTriggerPercentages(GuRank);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
        description.Add("BloodCost", XueDaoParasiteSystem.GetBloodQiCost(GuRank));
        description.Add("ParasiteValue", XueDaoParasiteSystem.GetBloodQiBaseValue(GuRank) + (IsUpgraded ? 2 : 0));
        description.Add("ParasiteBleed", XueDaoParasiteSystem.GetBloodQiBleed(GuRank));
        description.Add("TriggerCount", rates.Length);
        description.Add("Rate1", rates[0]);
        description.Add("Rate2", rates.Length >= 2 ? rates[1] : 0);
        description.Add("Rate3", rates.Length >= 3 ? rates[2] : 0);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? host = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1) { Cancelable = false },
            card => XueDaoParasiteSystem.CanAttach(card, XueDaoParasiteSystem.ParasiteKind.BloodQi),
            this
        )).FirstOrDefault();

        if (host == null)
        {
            return;
        }

        int cost = XueDaoParasiteSystem.GetBloodQiCost(GuRank);
        int availableBlood = Math.Min(cost, XueDaoPowerSystem.GetXueYuan(Owner.Creature));
        int missing = cost - availableBlood;

        if (missing > 0 && Owner.Creature.CurrentHp <= missing * 2)
        {
            return;
        }

        if (availableBlood > 0 &&
            !await XueDaoPowerSystem.TrySpendXueYuan(choiceContext, this, availableBlood))
        {
            return;
        }

        if (missing > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner.Creature,
                missing * 2,
                ValueProp.Unblockable | ValueProp.Unpowered,
                this,
                cardPlay
            );
        }

        await XueDaoParasiteSystem.AttachAsync(
            choiceContext,
            host,
            XueDaoParasiteSystem.ParasiteKind.BloodQi,
            GuRank,
            IsUpgraded,
            this
        );
    }

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(card, XueDaoParasiteSystem.ParasiteKind.BloodQi)
        ) == true;

    private bool CanPayCost()
    {
        int missing = Math.Max(
            0,
            XueDaoParasiteSystem.GetBloodQiCost(GuRank) -
            XueDaoPowerSystem.GetXueYuan(Owner.Creature)
        );
        return Owner.Creature.CurrentHp > missing * 2;
    }
}
