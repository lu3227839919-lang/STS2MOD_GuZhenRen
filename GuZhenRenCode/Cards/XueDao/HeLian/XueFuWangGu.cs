using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(XueLuGu),
    typeof(DaoChiXueFuGu),
    MinimumMaterialRank = 4
)]
public sealed class XueFuWangGu : AbstractHeLianGuCard
{
    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 7 ? 4 : 3;

    public XueFuWangGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int consumed = await XueDaoCardSystem.ConsumeOldestRemains(
            choiceContext,
            Owner,
            2
        );

        if (consumed > 0)
        {
            (_, int overflow) = await XueDaoPowerSystem.GainXueLuOrOverflow(
                choiceContext,
                this,
                consumed
            );

            if (overflow > 0)
            {
                await CreatureCmd.Heal(
                    Owner.Creature,
                    overflow * 3
                );
            }
        }

        XueFuWang generated =
            GuGeneratedCardFactory.Create<XueFuWang>(
                Owner,
                GuRank,
                upgraded: false
            );
        generated.ConfigureConsumedRemains(consumed);

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            generated,
            Owner
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
    [
        GuCardReferenceFactory.Create<XueFuWang>(this, false),
    ];

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
}
