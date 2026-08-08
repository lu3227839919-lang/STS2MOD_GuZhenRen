using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoCompanionCard :
    AbstractGuZhenRenCard,
    ILiDaoCompanionCard,
    ICardRewardExcluded
{
    public abstract Type TrainedGuType { get; }

    public override bool CanBeGeneratedInCombat => false;

    protected AbstractLiDaoCompanionCard(
        CardType type,
        TargetType target
    ) : base(1, type, CardRarity.Common, target)
    {
        SetDao(Dao.LiDao);
    }

    /// <summary>
    /// 伴生牌与力道蛊一一对应，卡面转数跟随对应蛊。
    /// 首次加入牌组（EnsureForGuAsync）与蛊升转时都会触发转数变化钩子，
    /// 因此在此统一从 owner 牌组中同类型蛊同步当前转数。
    /// canonical 实例（图鉴/卡池）没有 owner，保持默认一转。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        LiDaoCompanionSystem.SyncRankFromGuToCompanions(this);
    }

    /// <summary>
    /// 伴生牌卡面头部显示与蛊虫卡一致的中文转数（如“三转”）。
    /// 基类已注入 Rank / Rank1-9Exact；这里补上文言风格的中文数字参数。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RankCN", ToChineseNumber(GuRank));
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (ReferenceEquals(cardPlay.Card, this))
        {
            await LiDaoTrainingSystem.TrainFromCompanionAsync(
                cardPlay,
                TrainedGuType
            );
        }
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenJianChong : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiZhiGu);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move)];

    public ChenJianChong() : base(CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => DamageCmd.Attack(DynamicVars.Damage.BaseValue)
        .FromCard(this, cardPlay)
        .Targeting(cardPlay.Target!)
        .WithHitFx("vfx/vfx_attack_blunt")
        .Execute(choiceContext);

    protected override void OnUpgrade() =>
        DynamicVars.Damage.UpgradeValueBy(3m);
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FeiXiongZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(FeiXiongZhiLiGu);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongZhuang() : base(CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        decimal damage = DynamicVars.Damage.BaseValue +
            (target.Block > 0
                ? DynamicVars["BlockBonus"].BaseValue
                : 0m);

        return DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["BlockBonus"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JiaoShuai : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ELiGu);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public JiaoShuai() : base(CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => DamageCmd.Attack(DynamicVars.Damage.BaseValue)
        .FromCard(this, cardPlay)
        .Targeting(cardPlay.Target!)
        .WithHitCount(DynamicVars["Hits"].IntValue)
        .WithHitFx("vfx/vfx_attack_blunt")
        .Execute(choiceContext);

    protected override void OnUpgrade() =>
        DynamicVars.Damage.UpgradeValueBy(1m);
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NiuJiaoDing : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QingNiuLaoLiGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new BlockVar(3m, ValueProp.Move),
    ];

    public NiuJiaoDing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
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
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ShiGuiLiGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("AttackBonus", 2m),
    ];

    public ChenZhuang() : base(CardType.Skill, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        bool attacked = CombatManager.Instance.History.CardPlaysFinished.Any(
            entry => entry is CardPlayFinishedEntry finished &&
                finished.HappenedThisTurn(CombatState) &&
                finished.CardPlay.Player == Owner &&
                finished.CardPlay.Card.Type == CardType.Attack
        );

        decimal block = DynamicVars.Block.BaseValue +
            (attacked ? DynamicVars["AttackBonus"].BaseValue : 0m);

        return CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["AttackBonus"].UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class KuLian : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(KuLiGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("HpLoss", 2m),
    ];

    public KuLian() : base(CardType.Skill, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int hpLoss = Math.Min(
            DynamicVars["HpLoss"].IntValue,
            Math.Max(0, Owner.Creature.CurrentHp - 1)
        );

        if (hpLoss > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner.Creature,
                hpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null,
                cardSource: this,
                cardPlay: cardPlay
            );
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
    }

    protected override void OnUpgrade() =>
        DynamicVars.Block.UpgradeValueBy(4m);
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class TiaoXiYunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ZiLiGengShengGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("PhantomBonus", 3m),
    ];

    public TiaoXiYunLi() : base(CardType.Skill, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        decimal block = DynamicVars.Block.BaseValue +
            (LiDaoPhantomSystem.GetPermanentPhantoms(Owner).Count > 0
                ? DynamicVars["PhantomBonus"].BaseValue
                : 0m);

        return CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["PhantomBonus"].UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QuanLiYiFuGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<VigorPower>(3m),
    ];

    public YunLi() : base(CardType.Skill, TargetType.Self)
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
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[typeof(VigorPower).Name].BaseValue,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars[typeof(VigorPower).Name].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class BaiShouJiaShi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiShouLiGu);
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new BlockVar(4m, ValueProp.Move),
    ];

    public BaiShouJiaShi() : base(CardType.Attack, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (LiDaoPhantomSystem.GetPermanentPhantoms(Owner).Count >= 2)
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
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
