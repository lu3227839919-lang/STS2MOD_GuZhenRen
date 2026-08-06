using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

public abstract class AbstractXueDaoToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    protected AbstractXueDaoToken(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target
    ) : base(baseCost, type, rarity, target)
    {
        SetDao(Dao.XueDao);
    }
}

/// <summary>
/// 遗骸：寄生牌完成合法击杀或流血致死后获得。主动使用恢复2点血元后
/// 立即消耗消失；未被使用的遗骸在战斗结束后保留到永久牌堆（最多4张），
/// 也可被血颅蛊、刀翅血蝠蛊及相关杀招主动消耗。
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YiHai : AbstractXueDaoToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<XueYuanPower>(2m)];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public YiHai()
        : base(
            0,
            CardType.Status,
            CardRarity.Status,
            TargetType.Self
        )
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await XueDaoPowerSystem.GainXueYuanFromCardEffect(
            choiceContext,
            this,
            DynamicVars[typeof(XueYuanPower).Name].IntValue
        );

        // 遗骸使用后立即消失（进入消耗堆），不可在永久牌堆中复用；
        // 未使用的遗骸作为非消耗牌在战斗结束后保留至永久牌堆。
        await CardExhaustCompat.ExhaustAsync(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
    }
}

public abstract class AbstractBloodBatToken : AbstractXueDaoToken
{
    private const string HitsVar = "Hits";
    private const string BleedVar = "Bleed";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar(HitsVar, 2m),
        new PowerVar<LiuXuePower>(1m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Ethereal, CardKeyword.Exhaust];

    protected abstract int ExtraBaseHits { get; }

    protected abstract bool TransfersOnKill { get; }

    protected AbstractBloodBatToken(int baseCost)
        : base(
            baseCost,
            CardType.Attack,
            CardRarity.Token,
            TargetType.AnyEnemy
        )
    {
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? currentTarget = cardPlay.Target;
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            return;
        }

        bool initialTargetHadBleed =
            XueDaoPowerSystem.GetLiuXue(
                currentTarget,
                Owner.Creature
            ) is { Amount: > 0 };

        int hits = DynamicVars[HitsVar].IntValue +
            ExtraBaseHits +
            (initialTargetHadBleed ? 1 : 0);
        decimal damagePerHit = DynamicVars.Damage.BaseValue +
            XueDaoPowerSystem.GetXueLu(Owner.Creature);

        HashSet<Creature> touched = new(ReferenceEqualityComparer.Instance);

        for (int hit = 0; hit < hits; hit++)
        {
            if (currentTarget.IsDead)
            {
                if (!TransfersOnKill)
                {
                    break;
                }

                currentTarget = SelectNextTarget();
                if (currentTarget == null)
                {
                    break;
                }
            }

            touched.Add(currentTarget);

            await DamageCmd
                .Attack(damagePerHit)
                .FromCard(this, cardPlay)
                .Targeting(currentTarget)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (cardPlay.PlayIndex != 0)
        {
            return;
        }

        int bleed = DynamicVars[typeof(LiuXuePower).Name].IntValue;
        foreach (Creature target in GuZhenRenDeterminism
                     .OrderCreatures(touched)
                     .Where(c => c.IsAlive))
        {
            await XueDaoPowerSystem.ApplyLiuXue(
                choiceContext,
                this,
                target,
                bleed
            );
        }
    }

    private Creature? SelectNextTarget()
    {
        if (CombatState == null)
        {
            return null;
        }

        return GuZhenRenDeterminism
            .OrderCreatures(CombatState.HittableEnemies)
            .Where(enemy => enemy.IsAlive)
            .OrderBy(enemy => enemy.CurrentHp)
            .ThenBy(enemy => enemy.CombatId)
            .FirstOrDefault();
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 3,
            2 => 4,
            3 => 4,
            4 => 5,
            5 => 6,
            6 => 6,
            7 => 7,
            8 => 8,
            _ => 9,
        };
        DynamicVars[HitsVar].BaseValue = GuRank switch
        {
            <= 2 => 2,
            <= 5 => 3,
            _ => 4,
        };
        DynamicVars[typeof(LiuXuePower).Name].BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 6 => 2,
            <= 8 => 3,
            _ => 4,
        };
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class DaoChiXueFu : AbstractBloodBatToken
{
    protected override int ExtraBaseHits => 0;

    protected override bool TransfersOnKill => false;

    public DaoChiXueFu() : base(1)
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class DaoChiXueFuQun : AbstractBloodBatToken
{
    protected override int ExtraBaseHits => 2;

    protected override bool TransfersOnKill => true;

    public DaoChiXueFuQun() : base(2)
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XueFuWang : AbstractBloodBatToken
{
    private const string ConsumedRemainsVar = "ConsumedRemains";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Concat(
            [new DynamicVar(ConsumedRemainsVar, 0m)]
        );

    protected override int ExtraBaseHits =>
        DynamicVars[ConsumedRemainsVar].IntValue * 2;

    protected override bool TransfersOnKill =>
        DynamicVars[ConsumedRemainsVar].IntValue >= 2;

    public XueFuWang() : base(2)
    {
    }

    internal void ConfigureConsumedRemains(int amount)
    {
        DynamicVars[ConsumedRemainsVar].BaseValue =
            Math.Clamp(amount, 0, 2);
    }
}
