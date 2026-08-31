using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(XueQiGu),
    typeof(YueGuangGu),
    MinimumMaterialRank = 2
)]
public sealed class XueYueGu : AbstractHeLianGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 5;

    public override int RecoveryDelayTurns => GuRank >= 5 ? 2 : 3;

    protected override bool IsPlayable =>
        base.IsPlayable && (IsCanonical || HasEligibleHost());

    public XueYueGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        SetGuRank(MinimumAvailableGuRank);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
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
                        XueDaoParasiteSystem.ParasiteKind.BloodMoon
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
                XueDaoParasiteSystem.ParasiteKind.BloodMoon,
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
            materials.All(card => card is not YueGuangGu) ||
            materials.OfType<IGuRankProvider>().Any(card => card.GuRank < 2))
        {
            throw new InvalidOperationException(
                "血月蛊需要二转血气蛊与二转月光蛊。"
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
                XueDaoParasiteSystem.ParasiteKind.BloodMoon
            )
        ) == true;
}
