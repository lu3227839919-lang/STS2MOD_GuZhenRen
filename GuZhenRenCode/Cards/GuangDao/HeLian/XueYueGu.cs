using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 血月蛊：月光蛊与血气蛊合练而成。植入会跨三次触发推进的月相寄生。
/// </summary>
[HeLianRecipe(typeof(YueGuangGu), typeof(XueQiGu), MinimumMaterialRank = 3)]
public sealed class XueYueGu : AbstractHeLianGuCard
{
    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        >= 9 => 4,
        >= 7 => 4,
        _ => 3,
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
        [
            GuZhenRenKeywords.XueYuan,
            GuZhenRenKeywords.XueJi,
            GuZhenRenKeywords.YueXiang,
            GuZhenRenKeywords.CanYue,
            GuZhenRenKeywords.YingYue,
            GuZhenRenKeywords.ManYue,
            GuZhenRenKeywords.ZongEDu,
            GuZhenRenKeywords.PoTai,
            GuZhenRenKeywords.FuHua,
            GuZhenRenKeywords.LiuXue,
            GuZhenRenKeywords.XueYin,
        ]).Distinct();

    protected override bool IsPlayable =>
        base.IsPlayable &&
        (IsCanonical ||
         (XueDaoPowerSystem.GetXueYuan(Owner.Creature) >= 2 && HasEligibleHost()));

    public XueYueGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        var crescent = XueDaoParasiteSystem.GetBloodMoonPhaseValues(GuRank, 0);
        var waxing = XueDaoParasiteSystem.GetBloodMoonPhaseValues(GuRank, 1);
        var full = XueDaoParasiteSystem.GetBloodMoonPhaseValues(GuRank, 2);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
        AddPhase(description, "Crescent", crescent);
        AddPhase(description, "Waxing", waxing);
        AddPhase(description, "Full", full);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? host = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1) { Cancelable = false },
            card => XueDaoParasiteSystem.CanAttach(card, XueDaoParasiteSystem.ParasiteKind.BloodMoon),
            this
        )).FirstOrDefault();

        if (host == null ||
            !await XueDaoPowerSystem.TrySpendXueYuan(choiceContext, this, 2))
        {
            return;
        }

        await XueDaoParasiteSystem.AttachAsync(
            choiceContext,
            host,
            XueDaoParasiteSystem.ParasiteKind.BloodMoon,
            GuRank,
            IsUpgraded,
            this
        );
    }

    protected override int CalculateHeLianResultRank(IReadOnlyList<CardModel> materials) =>
        Math.Min(
            MaxGuRank,
            materials.OfType<IGuRankProvider>().Select(provider => provider.GuRank).DefaultIfEmpty(3).Min() + 1
        );

    protected override void OnHeLianCompleted(IReadOnlyList<CardModel> materials)
    {
        if (materials.All(card => card.IsUpgraded) && !IsUpgraded)
        {
            CardCmd.Upgrade(this);
        }
    }

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(card, XueDaoParasiteSystem.ParasiteKind.BloodMoon)
        ) == true;

    private void AddPhase(
        LocString description,
        string prefix,
        XueDaoParasiteSystem.BloodMoonPhaseValues values
    )
    {
        description.Add(prefix + "Base", values.BaseDamage + (IsUpgraded ? 3 : 0));
        description.Add(prefix + "Scale", values.EnergyScale);
        description.Add(prefix + "Bleed", values.TotalBleed);
        description.Add(prefix + "Marks", values.TotalMarks);
    }
}
