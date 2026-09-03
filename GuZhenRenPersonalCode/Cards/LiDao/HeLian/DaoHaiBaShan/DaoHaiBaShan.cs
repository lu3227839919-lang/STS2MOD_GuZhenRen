using GuZhenRen.Cards.LiDao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 倒海拔山：力道合练仙蛊，由六转拔山蛊与六转挽澜蛊合练而成，
/// 成蛊即为七转，最高可升至九转。
///
/// 催动顺序固定为：撼山削格挡 → 多段群体攻击 → 破势判定与易伤 →
/// 回能 → 回澜（八转起破势≥2追加群体伤害；九转全体破势再追加一次）。
/// 原本 0 格挡的敌人不计入破势、回能与回澜条件。
/// </summary>
[RegisterCard(
    typeof(GuZhenRenGuCardPool),
    Inherit = true
)]
[HeLianRecipe(
    typeof(BaShan),
    typeof(WanLan),
    MinimumMaterialRank = 6
)]
public sealed class DaoHaiBaShan : AbstractHeLianGuCard
{
    public override int MinimumAvailableGuRank => 7;

    public override int MaxGuRank => 9;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 8 => 4,
        _ => 5,
    };

    public DaoHaiBaShan()
        : base(
            0,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        SetGuRank(7);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Damage", DamageAtRank(GuRank));
        description.Add("BlockBreak", BlockBreakAtRank(GuRank));
        description.Add("BreakVulnerable", BreakVulnerableAtRank(GuRank));
        description.Add("EnergyCap", EnergyCapAtRank(GuRank));
        description.Add("ReboundDamage", ReboundDamageAtRank(GuRank));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ICombatState combatState = CombatState!;
        int blockBreak = BlockBreakAtRank(GuRank);
        int damage = DamageAtRank(GuRank);
        int breakVulnerable = BreakVulnerableAtRank(GuRank);
        int energyCap = EnergyCapAtRank(GuRank);

        // 记录催动前每个存活敌人的格挡，用于破势判定
        // （撼山阶段与多段攻击阶段均计入）。
        Dictionary<Creature, int> preBlock = combatState.Enemies
            .Where(enemy => enemy.IsAlive)
            .ToDictionary(enemy => enemy, enemy => enemy.Block);

        // 1. 撼山：先使所有敌人失去固定数值的格挡（不视为普通攻击伤害）。
        foreach (Creature enemy in preBlock.Keys)
        {
            if (enemy.Block > 0)
            {
                await CreatureCmd.LoseBlock(
                    choiceContext,
                    enemy,
                    Math.Min(blockBreak, enemy.Block),
                    Owner.Creature
                );
            }
        }

        // 2. 连击：撼山后对所有敌人造成 3 次群体伤害。
        if (damage > 0)
        {
            await DamageCmd.Attack(damage)
                .WithHitCount(3)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(combatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }

        // 3. 破势判定：目标原有正数格挡被本次催动归零即视为成功破势，
        //    对其施加易伤并计入回能/回澜。
        int brokenCount = 0;
        foreach (Creature enemy in combatState.HittableEnemies)
        {
            if (preBlock.TryGetValue(enemy, out int before) &&
                before > 0 &&
                enemy.Block <= 0)
            {
                brokenCount++;
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    enemy,
                    breakVulnerable,
                    Owner.Creature,
                    this
                );
            }
        }

        // 4. 回能：每成功破势 1 名敌人回复 1 点能量，受当前转数上限限制。
        int energy = Math.Min(brokenCount, energyCap);
        if (energy > 0)
        {
            await PlayerCmd.GainEnergy(energy, Owner);
        }

        // 5. 回澜：八转起破势≥2 追加群体伤害；九转全体破势再追加一次。
        int rebound = ReboundDamageAtRank(GuRank);
        if (rebound > 0 && brokenCount >= 2)
        {
            await DamageCmd.Attack(rebound)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(combatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);

            if (GuRank >= 9 &&
                AllAliveEnemiesBroken(combatState, preBlock))
            {
                await DamageCmd.Attack(rebound)
                    .FromCard(this, cardPlay)
                    .TargetingAllOpponents(combatState)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
        }
    }

    /// <summary>
    /// 九转回澜条件：所有存活敌人均被成功破势
    /// （每个存活敌人都拥有正数催动前格挡且已被击穿）。
    /// </summary>
    private static bool AllAliveEnemiesBroken(
        ICombatState combatState,
        IReadOnlyDictionary<Creature, int> preBlock
    )
    {
        Creature[] alive = combatState.HittableEnemies.ToArray();
        return alive.Length > 0 &&
            alive.All(enemy =>
                preBlock.TryGetValue(enemy, out int before) &&
                before > 0 &&
                enemy.Block <= 0
            );
    }

    /// <summary>
    /// 合练结果转数 = 材料最高转数 + 1（六转拔山 + 六转挽澜 → 七转）。
    /// </summary>
    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        int maxMaterialRank = materials
            .OfType<IGuRankProvider>()
            .Select(provider => provider.GuRank)
            .DefaultIfEmpty(6)
            .Max();

        return Math.Min(maxMaterialRank + 1, MaxGuRank);
    }

    internal static int DamageAtRank(int rank) => rank switch
    {
        >= 9 => 12,
        8 => 9,
        _ => 7,
    };

    internal static int BlockBreakAtRank(int rank) => rank switch
    {
        >= 9 => 20,
        8 => 16,
        _ => 12,
    };

    internal static int BreakVulnerableAtRank(int rank) =>
        rank >= 9 ? 3 : 2;

    internal static int EnergyCapAtRank(int rank) =>
        rank >= 8 ? 3 : 2;

    internal static int ReboundDamageAtRank(int rank) => rank switch
    {
        >= 9 => 12,
        8 => 9,
        _ => 0,
    };
}
