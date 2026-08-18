using GuZhenRen.Characters;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 拔山：力道群体攻击蛊，无需解封。
/// 催动顺序固定为：撼山削格挡 → 群体伤害 → 基础易伤 → 破势额外易伤。
/// 只有目标在此次催动前拥有正数格挡，且此次催动将其格挡降至 0 时，
/// 才触发额外破势易伤（撼山与后续攻击阶段均计入）。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class BaShan : AbstractGuWormCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 7;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 3,
        6 => 3,
        _ => 4,
    };

    public BaShan()
        : base(
            1,
            CardType.Attack,
            CardRarity.Uncommon,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("BlockBreak", BlockBreakAtRank(GuRank));
        description.Add("Damage", DamageAtRank(GuRank));
        description.Add("BaseVulnerable", 1);
        description.Add("BreakVulnerableBonus", BreakVulnerableBonusAtRank(GuRank));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ICombatState combatState = CombatState!;
        int blockBreak = BlockBreakAtRank(GuRank);
        int damage = DamageAtRank(GuRank);
        int breakVulnerable = BreakVulnerableBonusAtRank(GuRank);

        // 记录催动前每个存活敌人的格挡，用于破势判定。
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

        // 2. 群体伤害（撼山结算后对所有敌人造成伤害）。
        if (damage > 0)
        {
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(combatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }

        // 3. 基础易伤 + 破势额外易伤。
        foreach (Creature enemy in combatState.HittableEnemies)
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                enemy,
                1,
                Owner.Creature,
                this
            );

            if (preBlock.TryGetValue(enemy, out int before) &&
                before > 0 &&
                enemy.Block <= 0)
            {
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    enemy,
                    breakVulnerable,
                    Owner.Creature,
                    this
                );
            }
        }
    }

    internal static int BlockBreakAtRank(int rank) => rank switch
    {
        >= 7 => 12,
        6 => 8,
        _ => 5,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        >= 7 => 15,
        6 => 11,
        _ => 8,
    };

    internal static int BreakVulnerableBonusAtRank(int rank) =>
        rank >= 7 ? 2 : 1;
}
