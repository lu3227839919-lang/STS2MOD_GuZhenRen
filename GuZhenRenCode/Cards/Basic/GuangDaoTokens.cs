using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

public abstract class AbstractGuangDaoToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    protected AbstractGuangDaoToken(int baseCost, CardType type)
        : base(
            baseCost,
            type,
            CardRarity.Token,
            TargetType.Self
        )
    {
        SetDao(Dao.GuangDao);
    }

    protected static async Task ApplyFocus(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int amount
    )
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            source.Owner.Creature,
            amount,
            source.Owner.Creature,
            source
        );
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class WeiGuang : AbstractGuangDaoToken
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/XiaoGuangGu.png"
    );

    public WeiGuang() : base(0, CardType.Skill)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ApplyFocus(choiceContext, this, IsUpgraded ? 5 : 3);

    protected override void OnUpgrade()
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JuGuang : AbstractGuangDaoToken
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/XiaoGuangGu.png"
    );

    public JuGuang() : base(0, CardType.Skill)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await ApplyFocus(
            choiceContext,
            this,
            IsUpgraded ? 8 : 6
        );

        if (GuRank >= 8 && cardPlay.PlayIndex == 0)
        {
            YuHui afterglow = GuGeneratedCardFactory.Create<YuHui>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                afterglow,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YuHui : AbstractGuangDaoToken
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/XiaoGuangGu.png"
    );

    public YuHui() : base(0, CardType.Skill)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ApplyFocus(choiceContext, this, 3);

    protected override void OnUpgrade()
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JiGuang : AbstractGuangDaoToken
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/XiaoGuangGu.png"
    );

    public JiGuang() : base(1, CardType.Skill)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            this,
            IsUpgraded ? 3 : 2
        );
        await ApplyFocus(
            choiceContext,
            this,
            IsUpgraded ? 10 : 8
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
