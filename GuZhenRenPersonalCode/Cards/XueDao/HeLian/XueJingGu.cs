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
    typeof(XueQiGu),
    typeof(XueRouGu),
    MinimumMaterialRank = 2
)]
public sealed class XueJingGu : AbstractHeLianGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 5;

    public override int YuanQiCost => 1;

    public override int RecoveryDelayTurns => 2;

    protected override bool IsPlayable =>
        base.IsPlayable && (IsCanonical || HasEligibleHost());

    public XueJingGu()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        SetGuRank(MinimumAvailableGuRank);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        XueDaoParasiteSystem.AddOrdinaryDescriptionArgs(description, GuRank);
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
                GuRank,
                this
            );
        }
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count != 2 ||
            materials.All(card => card is not XueQiGu) ||
            materials.All(card => card is not XueRouGu) ||
            materials.OfType<IGuRankProvider>().Any(card => card.GuRank < 2))
        {
            throw new InvalidOperationException(
                "血精蛊需要二转血气蛊与二转血肉蛊。"
            );
        }

        return Math.Clamp(
            materials.OfType<IGuRankProvider>()
                .Select(card => card.GuRank)
                .Min() + 1,
            MinimumAvailableGuRank,
            MaxGuRank
        );
    }

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(
                card,
                XueDaoParasiteSystem.ParasiteKind.Ordinary
            )
        ) == true;
}
