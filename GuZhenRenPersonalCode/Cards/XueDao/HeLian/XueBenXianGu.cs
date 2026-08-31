using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(XueJingGu),
    typeof(XueRouGu),
    MinimumMaterialRank = 5
)]
public sealed class XueBenXianGu : AbstractHeLianGuCard
{
    public override int MinimumAvailableGuRank => 6;

    public override int MaxGuRank => 6;

    public override int YuanQiCost => 1;

    public override int RecoveryDelayTurns => 3;

    protected override bool IsPlayable =>
        base.IsPlayable && (IsCanonical || HasEligibleHost());

    public XueBenXianGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        SetGuRank(6);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        XueDaoParasiteSystem.AddOrdinaryDescriptionArgs(description, 6);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        CardModel? host = (
                await CardSelectCmd.FromHand(
                    choiceContext,
                    Owner,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1)
                    {
                        Cancelable = false,
                    },
                    card => XueDaoParasiteSystem.CanAttach(
                        card,
                        XueDaoParasiteSystem.ParasiteKind.Ordinary
                    ),
                    this
                )
            )
            .FirstOrDefault();

        if (host != null)
        {
            await XueDaoParasiteSystem.AttachAsync(
                choiceContext,
                host,
                XueDaoParasiteSystem.ParasiteKind.Ordinary,
                6,
                this
            );
        }
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count != 2 ||
            materials.All(card => card is not XueJingGu) ||
            materials.All(card => card is not XueRouGu) ||
            materials.OfType<IGuRankProvider>().Any(card => card.GuRank < 5))
        {
            throw new InvalidOperationException(
                "血本仙蛊需要五转血精蛊与五转血肉蛊。"
            );
        }

        return 6;
    }

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(
                card,
                XueDaoParasiteSystem.ParasiteKind.Ordinary
            )
        ) == true;
}
