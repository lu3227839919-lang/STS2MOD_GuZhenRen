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

        // Replay 只重复攻击段。格挡属于本次出牌的首段收益，
        // 避免八、九转同时把攻防两侧都按 Replay 倍增。
        if (cardPlay.PlayIndex == 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                DynamicVars.Block,
                cardPlay
            );
        }
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
        (int damage, int block) = GuRank switch
        {
            1 => (8, 5),
            2 => (9, 7),
            3 => (10, 9),
            4 => (11, 11),
            5 => (12, 13),
            6 => (14, 15),
            7 => (16, 17),
            8 => (18, 19),
            _ => (20, 21),
        };

        DynamicVars.Damage.BaseValue = damage;
        DynamicVars.Block.BaseValue = block;

        if (IsMutable)
        {
            BaseReplayCount = GuRank >= 8 ? 1 : 0;

            int rankDiscount = GuRank >= 7 ? 1 : 0;
            int rankAdjustedCost = Math.Max(
                0,
                2 - (IsUpgraded ? 1 : 0) - rankDiscount
            );
            EnergyCost.SetThisCombat(rankAdjustedCost);
        }
    }
}
