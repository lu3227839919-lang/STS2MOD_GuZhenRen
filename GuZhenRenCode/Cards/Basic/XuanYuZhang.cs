using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.TuDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XuanYuZhang
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new BlockVar(5m, ValueProp.Move),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override bool GainsBlock => true;

    public override bool CanBeGeneratedInCombat => false;

    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/XuanYuZhang.png"
        );

    public XuanYuZhang()
        : base(
            baseCost: 2,
            type: CardType.Attack,
            rarity: CardRarity.Token,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.TuDao);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        RefreshRankValues();
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        int preImmortalIncrease = Math.Max(
            0,
            Math.Min(GuRank, 5) - 1
        );
        DynamicVars.Damage.BaseValue = 8 + preImmortalIncrease;
        DynamicVars.Block.BaseValue = 5 + preImmortalIncrease * 2;
        // 规范卡牌模型在 ModelDb 启动期间不可写。奖励牌、
        // 生成牌和读档卡均会在可变实例上重新调用本方法。
        if (IsMutable)
        {
            BaseReplayCount = GuRank >= 6 ? 1 : 0;
        }

        if (GuRank >= 6)
        {
            int rankAdjustedCost = Math.Max(
                0,
                2 - (IsUpgraded ? 1 : 0) - 1
            );
            EnergyCost.SetThisCombat(rankAdjustedCost);
        }
    }
}
