using System.Runtime.CompilerServices;

using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

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
        RefreshRankValues();
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

    /// <summary>
    /// 按当前转数刷新 DynamicVars 数值（转数基础值 + 升级增量）。
    /// 子类覆写时在构造完成后与蛊升转时调用。
    /// </summary>
    protected virtual void RefreshRankValues()
    {
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

    /// <summary>本回合是否已打出过攻击牌（不含当前正在结算的这张）。</summary>
    protected bool PlayedAttackEarlierThisTurn()
    {
        CombatManager? combat = CombatManager.Instance;
        return combat != null &&
            combat.History.CardPlaysFinished.Any(
                entry => entry is CardPlayFinishedEntry finished &&
                    finished.HappenedThisTurn(CombatState) &&
                    finished.CardPlay.Player == Owner &&
                    finished.CardPlay.Card.Type == CardType.Attack
            );
    }

    /// <summary>当前常驻虚影包含的兽力种类数（基础虚影计 1 种，百兽按组成计）。</summary>
    protected int PermanentPhantomKinds =>
        Owner != null
            ? LiDaoPhantomSystem.GetPermanentPhantomKinds(Owner)
            : 0;
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenJianChong : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiZhiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move)];

    public ChenJianChong() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues() =>
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.ChenJianChongDamage(GuRank) + _upDamage;

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("FirstAttackRange", rank is >= 3 and <= 6 ? 1 : 0);
        description.Add(
            "FirstAttackBonus",
            LiDaoCompanionRankTable.ChenJianChongFirstAttackBonus(rank)
        );
        description.Add("NoBlockRange", rank >= 7 ? 1 : 0);
        description.Add(
            "NoBlockBonus",
            LiDaoCompanionRankTable.ChenJianChongNoBlockBonus(rank)
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        int rank = GuRank;
        decimal damage = DynamicVars.Damage.BaseValue;

        if (!PlayedAttackEarlierThisTurn() &&
            rank is >= 3 and <= 6)
        {
            damage += LiDaoCompanionRankTable
                .ChenJianChongFirstAttackBonus(rank);
        }
        if (target.Block <= 0 && rank >= 7)
        {
            damage += LiDaoCompanionRankTable
                .ChenJianChongNoBlockBonus(rank);
        }

        return DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FeiXiongZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(FeiXiongZhiLiGu);

    private decimal _upDamage;
    private decimal _upBlockBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongZhuang() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.FeiXiongZhuangDamage(GuRank) + _upDamage;
        DynamicVars["BlockBonus"].BaseValue =
            LiDaoCompanionRankTable.FeiXiongZhuangBlockBonus(GuRank) +
            _upBlockBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("BlockRange", rank <= 5 ? 1 : 0);
        description.Add(
            "BlockBonus",
            LiDaoCompanionRankTable.FeiXiongZhuangBlockBonus(rank)
        );
        description.Add("DivineRange", rank >= 6 ? 1 : 0);
        description.Add(
            "DivineMight",
            LiDaoCompanionRankTable.FeiXiongZhuangDivineMight(rank)
        );
        description.Add("QuakeRange", rank >= 8 ? 1 : 0);
        description.Add(
            "Quake",
            LiDaoCompanionRankTable.FeiXiongZhuangQuake(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        int rank = GuRank;
        decimal damage = DynamicVars.Damage.BaseValue;

        if (target.Block > 0)
        {
            if (rank <= 5)
            {
                damage += DynamicVars["BlockBonus"].BaseValue;
            }
            else
            {
                await DamageCmd.Attack(damage)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_heavy_blunt")
                    .Execute(choiceContext);

                int divine = LiDaoCompanionRankTable
                    .FeiXiongZhuangDivineMight(rank);
                if (divine > 0 && target.IsAlive)
                {
                    await CreatureCmd.Damage(
                        choiceContext,
                        target,
                        divine,
                        ValueProp.Unblockable | ValueProp.Unpowered,
                        Owner.Creature,
                        this,
                        cardPlay
                    );
                }

                await ApplyQuakeAsync(choiceContext, rank, target);
                return;
            }
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);

        await ApplyQuakeAsync(choiceContext, rank, target);
    }

    private async Task ApplyQuakeAsync(
        PlayerChoiceContext choiceContext,
        int rank,
        Creature target
    )
    {
        if (rank < 8 || CombatState == null)
        {
            return;
        }

        int quake = LiDaoCompanionRankTable.FeiXiongZhuangQuake(rank);
        foreach (Creature enemy in GuZhenRenDeterminism
                     .OrderCreatures(CombatState.HittableEnemies)
                     .Where(enemy => enemy.IsAlive &&
                         !ReferenceEquals(enemy, target)))
        {
            await CreatureCmd.Damage(
                choiceContext,
                enemy,
                quake,
                ValueProp.Unpowered,
                Owner.Creature,
                this,
                cardPlay: null
            );
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        _upBlockBonus += 2m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JiaoShuai : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ELiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public JiaoShuai() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.JiaoShuaiDamage(GuRank) + _upDamage;
        DynamicVars["Hits"].BaseValue =
            LiDaoCompanionRankTable.JiaoShuaiHits(GuRank);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("LastHitRange", rank is >= 3 and <= 4 ? 1 : 0);
        description.Add(
            "LastHitBonus",
            LiDaoCompanionRankTable.JiaoShuaiLastHitBonus(rank)
        );
        description.Add("PursuitRange", rank >= 7 ? 1 : 0);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int hits = DynamicVars["Hits"].IntValue;
        decimal baseDamage = DynamicVars.Damage.BaseValue;
        int lastHitBonus = LiDaoCompanionRankTable.JiaoShuaiLastHitBonus(rank);
        bool pursues = LiDaoCompanionRankTable.JiaoShuaiPursues(rank);

        Creature? current = cardPlay.Target;
        for (int hit = 0; hit < hits; hit++)
        {
            if (current == null || current.IsDead)
            {
                if (!pursues)
                {
                    break;
                }
                current = LiDaoPhantomSystem.FindPursuitTarget(this);
                if (current == null)
                {
                    break;
                }
            }

            decimal damage = baseDamage;
            if (lastHitBonus > 0 && hit == hits - 1)
            {
                damage += lastHitBonus;
            }

            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(current)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 1m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NiuJiaoDing : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QingNiuLaoLiGu);
    public override bool GainsBlock => true;

    private decimal _upDamage;
    private decimal _upBlock;

    /// <summary>本场首次打出记录（战斗实例级别，随战斗克隆重置）。</summary>
    private sealed class FirstTimeState
    {
        internal bool Played;
    }

    private static readonly ConditionalWeakTable<CardModel, FirstTimeState>
        FirstTimeStates = new();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new BlockVar(3m, ValueProp.Move),
    ];

    public NiuJiaoDing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.NiuJiaoDingDamage(GuRank) + _upDamage;
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.NiuJiaoDingBlock(GuRank) + _upBlock;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("FirstTimeRange", rank >= 8 ? 1 : 0);
        description.Add(
            "FirstTimeBonus",
            LiDaoCompanionRankTable.NiuJiaoDingFirstTimeBonus(rank)
        );
        description.Add("PhantomLinkRange", rank >= 9 ? 1 : 0);
        description.Add(
            "PhantomLinkBonus",
            LiDaoCompanionRankTable.NiuJiaoDingPhantomLinkBonus(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        decimal block = DynamicVars.Block.BaseValue;
        if (rank >= 8 && TryClaimFirstTime())
        {
            block += LiDaoCompanionRankTable
                .NiuJiaoDingFirstTimeBonus(rank);
        }
        if (rank >= 9 &&
            LiDaoPhantomSystem.HasManifestedThisTurn(Owner))
        {
            block += LiDaoCompanionRankTable
                .NiuJiaoDingPhantomLinkBonus(rank);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    private bool TryClaimFirstTime()
    {
        FirstTimeState state = FirstTimeStates.GetValue(
            this,
            static _ => new FirstTimeState()
        );
        if (state.Played)
        {
            return false;
        }
        state.Played = true;
        return true;
    }

    protected override void OnUpgrade()
    {
        _upDamage += 2m;
        _upBlock += 2m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ShiGuiLiGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upAttackBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("AttackBonus", 2m),
    ];

    public ChenZhuang() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.ChenZhuangBlock(GuRank) + _upBlock;
        DynamicVars["AttackBonus"].BaseValue =
            LiDaoCompanionRankTable.ChenZhuangAttackBonus(GuRank) +
            _upAttackBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("NoBlockRange", rank is >= 5 and <= 8 ? 1 : 0);
        description.Add(
            "NoBlockBonus",
            LiDaoCompanionRankTable.ChenZhuangNoBlockBonus(rank)
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        decimal block = DynamicVars.Block.BaseValue;
        decimal attackBonus = PlayedAttackEarlierThisTurn()
            ? DynamicVars["AttackBonus"].BaseValue
            : 0m;

        if (Owner.Creature.Block <= 0)
        {
            if (rank <= 8)
            {
                block += LiDaoCompanionRankTable
                    .ChenZhuangNoBlockBonus(rank);
            }
            else
            {
                block = Math.Round(
                    block * 1.5m,
                    MidpointRounding.AwayFromZero
                );
            }
        }

        block += attackBonus;

        return CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upAttackBonus += 1m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class KuLian : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(KuLiGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("HpLoss", 2m),
    ];

    public KuLian() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues() =>
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.KuLianBlock(GuRank) + _upBlock;

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("HardshipRange", rank >= 5 ? 1 : 0);
        description.Add(
            "HardshipBonus",
            LiDaoCompanionRankTable.KuLianHardshipBonus(rank)
        );
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

        decimal block = DynamicVars.Block.BaseValue;
        int rank = GuRank;
        if (rank >= 5)
        {
            int hardship =
                Owner.Creature.GetPower<KuLiPower>()?.Hardship ?? 0;
            block += hardship *
                LiDaoCompanionRankTable.KuLianHardshipBonus(rank);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 4m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class TiaoXiYunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ZiLiGengShengGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upPhantomBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("PhantomBonus", 3m),
    ];

    public TiaoXiYunLi() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.TiaoXiYunLiBlock(GuRank) + _upBlock;
        DynamicVars["PhantomBonus"].BaseValue =
            LiDaoCompanionRankTable.TiaoXiYunLiPhantomBonus(GuRank) +
            _upPhantomBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("HealBasicRange", rank is >= 6 and <= 7 ? 1 : 0);
        description.Add("HealTwoRange", rank == 8 ? 1 : 0);
        if (rank is >= 6 and <= 8)
        {
            description.Add(
                "Heal",
                LiDaoCompanionRankTable.TiaoXiYunLiHeal(rank, 3)
            );
        }
        if (rank >= 9)
        {
            description.Add("Heal1", 1);
            description.Add("Heal2", 2);
            description.Add("Heal3", 3);
        }
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int kinds = PermanentPhantomKinds;

        decimal block = DynamicVars.Block.BaseValue;
        if (kinds > 0)
        {
            block += DynamicVars["PhantomBonus"].BaseValue;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );

        if (LiDaoCompanionRankTable.TiaoXiYunLiCanHeal(rank))
        {
            int heal = LiDaoCompanionRankTable.TiaoXiYunLiHeal(rank, kinds);
            if (heal > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, heal);
            }
        }
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upPhantomBonus += 1m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QuanLiYiFuGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upVigor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<VigorPower>(3m),
    ];

    public YunLi() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.YunLiBlock(GuRank) + _upBlock;
        DynamicVars[typeof(VigorPower).Name].BaseValue =
            LiDaoCompanionRankTable.YunLiVigor(GuRank) + _upVigor;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add(
            "PhantomVigorBasicRange",
            rank is >= 6 and <= 7 ? 1 : 0
        );
        description.Add("PhantomVigorTwoRange", rank >= 8 ? 1 : 0);
        description.Add(
            "PhantomVigorBonus",
            LiDaoCompanionRankTable.YunLiPhantomVigorBonus(
                rank,
                PermanentPhantomKinds
            )
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        decimal block = DynamicVars.Block.BaseValue;
        decimal vigor = DynamicVars[typeof(VigorPower).Name].BaseValue;

        int rank = GuRank;
        vigor += LiDaoCompanionRankTable.YunLiPhantomVigorBonus(
            rank,
            PermanentPhantomKinds
        );

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            vigor,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upVigor += 2m;
        RefreshRankValues();
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class BaiShouJiaShi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiShouLiGu);
    public override bool GainsBlock => true;

    private decimal _upDamage;
    private decimal _upBlock;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new BlockVar(4m, ValueProp.Move),
    ];

    public BaiShouJiaShi() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.BaiShouJiaShiDamage(GuRank) + _upDamage;
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.BaiShouJiaShiBlock(GuRank) + _upBlock;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("ExtraDamageRange", rank >= 5 ? 1 : 0);
        description.Add(
            "ExtraDamage",
            LiDaoCompanionRankTable.BaiShouJiaShiExtraDamage(rank)
        );
        description.Add("BlockFourRange", rank >= 8 ? 1 : 0);
        description.Add(
            "BlockFour",
            LiDaoCompanionRankTable.BaiShouJiaShiBlockFour(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int kinds = PermanentPhantomKinds;

        decimal damage = DynamicVars.Damage.BaseValue;
        if (kinds >= 3)
        {
            damage += LiDaoCompanionRankTable
                .BaiShouJiaShiExtraDamage(rank);
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (kinds >= 2)
        {
            decimal block = DynamicVars.Block.BaseValue;
            if (kinds >= 4)
            {
                block += LiDaoCompanionRankTable
                    .BaiShouJiaShiBlockFour(rank);
            }
            await CreatureCmd.GainBlock(
                Owner.Creature,
                block,
                ValueProp.Move,
                cardPlay
            );
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        _upBlock += 2m;
        RefreshRankValues();
    }
}
