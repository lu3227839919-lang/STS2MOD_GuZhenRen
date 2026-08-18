using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.LiDao;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 黑天大手印：由群力蛊与拔山蛊（或倒海拔山）推演而成的力道群攻杀招。
///
/// 结算顺序固定为：虚影与敌人格挡快照 → 撼山 → 主体群攻 → 基础易伤 →
/// 破势追加易伤 → 七转起破势回能 → 八转起回澜。
/// 虚影仅放大主体攻击，不会被显化、消耗或移除。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(QunLiGu), typeof(BaShan))]
[ShaZhaoRecipe(typeof(QunLiGu), typeof(DaoHaiBaShan))]
public sealed class HeiTianDaShouYin : AbstractShaZhaoCard
{
    private const string PhantomCountVar = "PhantomCount";
    private const string PhantomDamageVar = "PhantomDamage";
    private const string BlockBreakVar = "BlockBreak";
    private const string BaseVulnerableVar = "BaseVulnerable";
    private const string PoShiVulnerableVar = "PoShiVulnerable";
    private const string PoShiEnergyCapVar = "PoShiEnergyCap";
    private const string HuiLanThresholdVar = "HuiLanThreshold";
    private const string HuiLanDamageVar = "HuiLanDamage";

    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 9;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar(PhantomCountVar, 0m),
        new DynamicVar(PhantomDamageVar, 4m),
        new DynamicVar(BlockBreakVar, 5m),
        new DynamicVar(BaseVulnerableVar, 1m),
        new DynamicVar(PoShiVulnerableVar, 1m),
        new DynamicVar(PoShiEnergyCapVar, 0m),
        new DynamicVar(HuiLanThresholdVar, 0m),
        new DynamicVar(HuiLanDamageVar, 0m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [
            CardKeyword.Retain,
            CardKeyword.Exhaust,
            GuZhenRenKeywords.PoShi,
        ];

    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Instant;

    public HeiTianDaShouYin()
        : base(
            baseCost: 2,
            type: CardType.Attack,
            target: TargetType.AllEnemies
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
        int phantomCount = 0;
        if (CombatState != null)
        {
            phantomCount = PileType.Hand.GetPile(Owner).Cards.Count(card =>
                card.Keywords.Contains(GuZhenRenKeywords.XuYing)
            );
        }

        description.Add("BaseDamage", BaseDamageAtRank(GuRank));
        description.Add("PhantomDamage", PhantomDamageAtRank(GuRank));
        description.Add(
            "CurrentDamage",
            BaseDamageAtRank(GuRank) +
                phantomCount * PhantomDamageAtRank(GuRank)
        );
        description.Add("PhantomCount", phantomCount);
        description.Add("BlockBreak", BlockBreakAtRank(GuRank));
        description.Add("BaseVulnerable", 1);
        description.Add("PoShiVulnerable", PoShiVulnerableAtRank(GuRank));
        description.Add("PoShiEnergyCap", PoShiEnergyCapAtRank(GuRank));
        description.Add("HuiLanThreshold", HuiLanThresholdAtRank(GuRank));
        description.Add("HuiLanDamage", HuiLanDamageAtRank(GuRank));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            if (CombatState is not { } combatState)
            {
                return;
            }

            int rank = NormalizeRank(GuRank);
            int phantomCount = PileType.Hand.GetPile(Owner).Cards.Count(card =>
                card.Keywords.Contains(GuZhenRenKeywords.XuYing)
            );
            DynamicVars[PhantomCountVar].BaseValue = phantomCount;

            Creature[] startingEnemies = GuZhenRenDeterminism
                .OrderCreatures(combatState.Enemies)
                .Where(enemy => enemy.IsAlive)
                .ToArray();
            Dictionary<Creature, int> startingBlock = startingEnemies
                .ToDictionary(enemy => enemy, enemy => enemy.Block);

            // 1. 撼山：先削减开始时存活敌人的格挡，不造成普通攻击伤害。
            int blockBreak = BlockBreakAtRank(rank);
            foreach (Creature enemy in startingEnemies)
            {
                if (enemy.Block <= 0)
                {
                    continue;
                }

                await CreatureCmd.LoseBlock(
                    choiceContext,
                    enemy,
                    Math.Min(blockBreak, enemy.Block),
                    Owner.Creature
                );
            }

            // 2. 主体攻击只读取催动瞬间的虚影数量快照。
            int mainDamage = BaseDamageAtRank(rank) +
                phantomCount * PhantomDamageAtRank(rank);
            if (mainDamage > 0 && startingEnemies.Length > 0)
            {
                await DamageCmd.Attack(mainDamage)
                    .FromCard(this, cardPlay)
                    .TargetingAllOpponents(combatState)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }

            // 3. 对主体攻击后仍存活的敌人施加固定基础易伤。
            foreach (Creature enemy in startingEnemies.Where(enemy =>
                         enemy.IsAlive))
            {
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    enemy,
                    1,
                    Owner.Creature,
                    this
                );
            }

            // 4. 破势按开始时的敌人集合与格挡快照判定；即使主体攻击
            // 击杀了目标，仍保留其破势结果用于回能和“全体破势”。
            int poShiCount = 0;
            int poShiVulnerable = PoShiVulnerableAtRank(rank);
            foreach (Creature enemy in startingEnemies)
            {
                bool poShi = startingBlock[enemy] > 0 && enemy.Block <= 0;
                if (!poShi)
                {
                    continue;
                }

                poShiCount++;
                if (enemy.IsAlive && poShiVulnerable > 0)
                {
                    await PowerCmd.Apply<VulnerablePower>(
                        choiceContext,
                        enemy,
                        poShiVulnerable,
                        Owner.Creature,
                        this
                    );
                }
            }

            // 5. 七转起按破势人数回能，并应用对应转数上限。
            int energyGain = Math.Min(
                poShiCount,
                PoShiEnergyCapAtRank(rank)
            );
            if (energyGain > 0)
            {
                await PlayerCmd.GainEnergy(energyGain, Owner);
            }

            // 6. 八转起破势至少两人触发一次回澜；九转若开始时的
            // 全部存活敌人均成功破势，再独立追加一次回澜。
            int huiLanDamage = HuiLanDamageAtRank(rank);
            if (huiLanDamage > 0 &&
                poShiCount >= HuiLanThresholdAtRank(rank))
            {
                await DealHuiLanAsync(
                    choiceContext,
                    cardPlay,
                    combatState,
                    huiLanDamage
                );
            }

            bool allPoShi = startingEnemies.Length > 0 &&
                poShiCount == startingEnemies.Length;
            if (rank >= 9 && huiLanDamage > 0 && allPoShi)
            {
                await DealHuiLanAsync(
                    choiceContext,
                    cardPlay,
                    combatState,
                    huiLanDamage
                );
            }
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

    private async Task DealHuiLanAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ICombatState combatState,
        int damage
    )
    {
        if (damage <= 0 || !combatState.HittableEnemies.Any())
        {
            return;
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    private void RefreshRankValues()
    {
        int rank = NormalizeRank(GuRank);
        DynamicVars.Damage.BaseValue = BaseDamageAtRank(rank);
        DynamicVars[PhantomDamageVar].BaseValue = PhantomDamageAtRank(rank);
        DynamicVars[BlockBreakVar].BaseValue = BlockBreakAtRank(rank);
        DynamicVars[BaseVulnerableVar].BaseValue = 1;
        DynamicVars[PoShiVulnerableVar].BaseValue =
            PoShiVulnerableAtRank(rank);
        DynamicVars[PoShiEnergyCapVar].BaseValue =
            PoShiEnergyCapAtRank(rank);
        DynamicVars[HuiLanThresholdVar].BaseValue =
            HuiLanThresholdAtRank(rank);
        DynamicVars[HuiLanDamageVar].BaseValue = HuiLanDamageAtRank(rank);
    }

    private static int NormalizeRank(int rank) => Math.Clamp(rank, 5, 9);

    internal static int BaseDamageAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            5 => 8,
            6 => 11,
            7 => 18,
            8 => 27,
            _ => 36,
        };

    internal static int PhantomDamageAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            5 => 4,
            6 => 5,
            7 => 6,
            8 => 7,
            _ => 8,
        };

    internal static int BlockBreakAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            5 => 5,
            6 => 8,
            7 => 12,
            8 => 16,
            _ => 20,
        };

    internal static int PoShiVulnerableAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            <= 6 => 1,
            <= 8 => 2,
            _ => 3,
        };

    internal static int PoShiEnergyCapAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            <= 6 => 0,
            7 => 2,
            _ => 3,
        };

    internal static int HuiLanThresholdAtRank(int rank) =>
        NormalizeRank(rank) >= 8 ? 2 : 0;

    internal static int HuiLanDamageAtRank(int rank) =>
        NormalizeRank(rank) switch
        {
            8 => 9,
            >= 9 => 12,
            _ => 0,
        };
}
