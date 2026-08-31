using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class LiuGuangGu
    : AbstractGuWormCard,
      ITiaoGuCard,
      IRefractionRelevantCard
{
    public override int MinimumAvailableGuRank => 4;

    public override int MaxGuRank => 6;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public LiuGuangGu()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        SetGuRank(4);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int maximumTunes = GuRank >= 6 ? 2 : 1;
        bool cancelable = GuRank >= 6;
        for (int tune = 0; tune < maximumTunes; tune++)
        {
            bool completed = await TuneOnceAsync(
                choiceContext,
                cancelable
            );
            if (!completed && cancelable)
            {
                break;
            }
        }

        // 高转效果不依赖本次是否存在合法调蛊目标。
        if (GuRank >= 5)
        {
            GuangDaoPowerSystem.ForceNextGuangDaoRefraction(Owner);
        }
    }

    private async Task<bool> TuneOnceAsync(
        PlayerChoiceContext choiceContext,
        bool cancelable
    )
    {
        CardModel[] candidates = GuCardPileSystem.PileType
            .GetPile(Owner)
            .Cards
            .Where(card =>
                !ReferenceEquals(card, this) &&
                TiaoGuSystem.Service.CanTuneGu(card, Owner)
            )
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        CardModel? selected =
            (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    candidates,
                    Owner,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1)
                    {
                        Cancelable = cancelable,
                        RequireManualConfirmation = true,
                        PretendCardsCanBePlayed = true,
                    }
                )
            ).FirstOrDefault();

        if (selected == null)
        {
            return false;
        }

        await TiaoGuSystem.Service.TuneGuAsync(selected, Owner);
        return true;
    }
}
