using GuZhenRen.Cards.LiDao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 伤力回天：苦力蛊 + 自力更生蛊。
///
/// 先按当前伤势打出一记重击，随后进入“回天”状态。
/// 回天状态记录之后每张牌完整结算造成的实际生命伤害总和，
/// 只保留其中的最高值；同一张牌第一段中已经并入的苦力加伤也会计入。
///
/// 回复仅触发一次：
/// 1. 首次濒死时，以记录的最高值回复生命并阻止此次死亡；
/// 2. 若直到战斗结束都没有触发，则战斗结束时回复该最高值。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(KuLiGu), typeof(ZiLiGengShengGu))]
[ShaZhaoRecipe(typeof(ZiLiGengShengGu), typeof(KuLiGu))]
public sealed class ShangLiHuiTian : AbstractShaZhaoCard
{
    private const string LightBonusVar = "LightBonus";
    private const string HeavyBonusVar = "HeavyBonus";
    private const string CriticalBonusVar = "CriticalBonus";
    private const string RecoveryPercentVar = "RecoveryPercent";

    public override int MinimumAvailableGuRank => 3;
    public override int MaxGuRank => 7;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar(LightBonusVar, 8m),
        new DynamicVar(HeavyBonusVar, 16m),
        new DynamicVar(CriticalBonusVar, 28m),
        new DynamicVar(RecoveryPercentVar, 60m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [
            CardKeyword.Retain,
            CardKeyword.Exhaust,
            GuZhenRenKeywords.ShangShi,
        ];

    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Instant;

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public ShangLiHuiTian()
        : base(
            baseCost: 1,
            type: CardType.Attack,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.LiDao);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("LightBonus", LightBonusAtRank(GuRank));
        description.Add("HeavyBonus", HeavyBonusAtRank(GuRank));
        description.Add("CriticalBonus", CriticalBonusAtRank(GuRank));
        description.Add("RecoveryPercent", RecoveryPercentAtRank(GuRank));

        int currentDamage = BaseDamageAtRank(GuRank);
        if (CombatState != null)
        {
            currentDamage += GetInjuryTier(Owner.Creature) switch
            {
                1 => LightBonusAtRank(GuRank),
                2 => HeavyBonusAtRank(GuRank),
                >= 3 => CriticalBonusAtRank(GuRank),
                _ => 0,
            };
        }
        description.Add("CurrentDamage", currentDamage);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            Creature? target = cardPlay.Target;
            if (target == null || !target.IsHittable)
            {
                return;
            }

            int injuryTier = GetInjuryTier(Owner.Creature);
            int bonusDamage = injuryTier switch
            {
                1 => LightBonusAtRank(GuRank),
                2 => HeavyBonusAtRank(GuRank),
                >= 3 => CriticalBonusAtRank(GuRank),
                _ => 0,
            };

            int damage = BaseDamageAtRank(GuRank) + bonusDamage;
            if (damage > 0)
            {
                await DamageCmd.Attack(damage)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }

            // 回天状态在本牌伤害结算后才开始，因此不会把伤力回天
            // 自己的这次伤害计入“后续用牌伤害最大值”。
            await ApplyHuiTianStateAsync(choiceContext);
        }
        finally
        {
            await AdvanceLifecycleAsync(choiceContext);
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    protected override void OnShaZhaoStateLoaded()
    {
        base.OnShaZhaoStateLoaded();
        RefreshRankValues();
    }

    private async Task ApplyHuiTianStateAsync(
        PlayerChoiceContext choiceContext
    )
    {
        ShangLiHuiTianPower? existing =
            Owner.Creature.GetPower<ShangLiHuiTianPower>();

        if (existing != null)
        {
            existing.Arm(GuRank);
            return;
        }

        ShangLiHuiTianPower power =
            (ShangLiHuiTianPower)
                ModelDb.Power<ShangLiHuiTianPower>().ToMutable();
        power.Arm(GuRank);

        await PowerCmd.Apply(
            choiceContext,
            power,
            Owner.Creature,
            1,
            Owner.Creature,
            this
        );
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = BaseDamageAtRank(GuRank);
        DynamicVars[LightBonusVar].BaseValue =
            LightBonusAtRank(GuRank);
        DynamicVars[HeavyBonusVar].BaseValue =
            HeavyBonusAtRank(GuRank);
        DynamicVars[CriticalBonusVar].BaseValue =
            CriticalBonusAtRank(GuRank);
        DynamicVars[RecoveryPercentVar].BaseValue =
            RecoveryPercentAtRank(GuRank);
    }

    private static int BaseDamageAtRank(int rank) => rank switch
    {
        <= 3 => 8,
        4 => 9,
        5 => 10,
        6 => 11,
        _ => 12,
    };

    private static int LightBonusAtRank(int rank) => rank switch
    {
        <= 3 => 6,
        4 => 7,
        5 => 8,
        6 => 9,
        _ => 10,
    };

    private static int HeavyBonusAtRank(int rank) => rank switch
    {
        <= 3 => 12,
        4 => 14,
        5 => 16,
        6 => 18,
        _ => 20,
    };

    private static int CriticalBonusAtRank(int rank) => rank switch
    {
        <= 3 => 20,
        4 => 24,
        5 => 28,
        6 => 32,
        _ => 36,
    };


    private static int RecoveryPercentAtRank(int rank) => rank switch
    {
        <= 3 => 40,
        4 => 50,
        5 => 60,
        6 => 75,
        _ => 90,
    };

    private static int GetInjuryTier(Creature creature)
    {
        int maxHp = Math.Max(1, creature.MaxHp);
        int weightedHp = creature.CurrentHp * 100;

        if (weightedHp > maxHp * 75)
        {
            return 0;
        }
        if (weightedHp > maxHp * 50)
        {
            return 1;
        }
        if (weightedHp > maxHp * 25)
        {
            return 2;
        }
        return 3;
    }
}
