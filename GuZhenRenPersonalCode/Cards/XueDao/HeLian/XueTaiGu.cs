using GuZhenRen.Cards.XueDao;
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
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(XueQiGu),
    typeof(XueYueGu),
    MinimumMaterialRank = 4
)]
public sealed class XueTaiGu : AbstractHeLianGuCard
{
    public override int MaxUses => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
        [
            GuZhenRenKeywords.XueTai,
            GuZhenRenKeywords.TaiDong,
            GuZhenRenKeywords.TunJi,
            GuZhenRenKeywords.FuHua,
        ]).Distinct();

    public override int RecoveryDelayTurns => GuRank >= 7 ? 4 : 3;

    protected override bool IsPlayable =>
        base.IsPlayable &&
        (IsCanonical ||
         (HasEligibleHost() && CanPayBloodFetusCost()));

    public XueTaiGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
    }


    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        CardModel? host = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1)
            {
                Cancelable = false
            },
            card => XueDaoParasiteSystem.CanAttach(
                card,
                XueDaoParasiteSystem.ParasiteKind.BloodFetus
            ),
            this
        )).FirstOrDefault();

        if (host == null)
        {
            return;
        }

        int availableBlood = Math.Min(
            2,
            XueDaoPowerSystem.GetXueYuan(Owner.Creature)
        );
        int missing = 2 - availableBlood;

        if (missing > 0 &&
            Owner.Creature.CurrentHp <= missing * 2)
        {
            return;
        }

        if (availableBlood > 0 &&
            !await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                availableBlood
            ))
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
            XueDaoParasiteSystem.ParasiteKind.BloodFetus,
            GuRank,
            false,
            this
        );
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    ) => Math.Min(
        MaxGuRank,
        materials
            .OfType<IGuRankProvider>()
            .Select(provider => provider.GuRank)
            .DefaultIfEmpty(1)
            .Min() + 1
    );

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(
                card,
                XueDaoParasiteSystem.ParasiteKind.BloodFetus
            )
        ) == true;

    private bool CanPayBloodFetusCost()
    {
        int missing = Math.Max(
            0,
            2 - XueDaoPowerSystem.GetXueYuan(Owner.Creature)
        );
        return Owner.Creature.CurrentHp > missing * 2;
    }
}
