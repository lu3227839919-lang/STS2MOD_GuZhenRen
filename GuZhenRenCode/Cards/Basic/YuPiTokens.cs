using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.TuDao;

public abstract class AbstractYuPiToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override bool GainsBlock => true;

    protected override IEnumerable<CardTag> AdditionalCanonicalTags =>
        GuRank >= 6
            ? [GuZhenRenTags.GuangDao]
            : [];

    protected AbstractYuPiToken(int cost)
        : base(
            cost,
            CardType.Skill,
            CardRarity.Token,
            TargetType.Self
        )
    {
        SetDao(Dao.TuDao);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YuMo : AbstractYuPiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(8m, ValueProp.Move)];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YuPiGu.png"
    );

    public YuMo() : base(1)
    {
        RefreshRankValues();
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => CreatureCmd.GainBlock(
        Owner.Creature,
        DynamicVars.Block,
        cardPlay
    );

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 3 => 8,
            4 => 10,
            _ => 12,
        };
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YuGuangYi : AbstractYuPiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(12m, ValueProp.Move)];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YuPiGu.png"
    );

    public YuGuangYi() : base(1)
    {
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        if (GuRank >= 8)
        {
            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                1
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 6 => 12,
            7 => 15,
            8 => 18,
            _ => 20,
        };
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ZheGuang : AbstractYuPiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move)];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YuPiGu.png"
    );

    public ZheGuang() : base(0)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
        await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            this,
            1
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class LiuLiYuYi : AbstractYuPiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(22m, ValueProp.Move)];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, CardKeyword.Retain];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YuPiGu.png"
    );

    public LiuLiYuYi() : base(2)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
        await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            this,
            2
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
