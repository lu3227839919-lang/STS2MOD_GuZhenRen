using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.XueDao;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

[RegisterCard(typeof(GuZhenRen.Characters.GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(XueLuGu), typeof(XueFuWangGu))]
[ShaZhaoRecipe(typeof(XueFuWangGu), typeof(XueLuGu))]
public sealed class WanHaiGuiChao : AbstractShaZhaoCard
{
    private const string DamagePerRemainsVar = "DamagePerRemains";
    private const string BlockPerRemainsVar = "BlockPerRemains";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(24m, ValueProp.Move),
        new DynamicVar(DamagePerRemainsVar, 14m),
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar(BlockPerRemainsVar, 6m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
        GuZhenRenKeywords.GetShiHaiKeyword(3),
    ];

    public override bool GainsBlock => true;

    public WanHaiGuiChao()
        : base(3, CardType.Attack, TargetType.AllEnemies)
    {
        SetDao(Dao.XueDao);
        RefreshRankValues();
    }

    /// <summary>
    /// 本场封印型终结杀招：材料在战斗结束前不返还。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Sealed;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            int consumed = await XueDaoCardSystem.ConsumeOldestRemains(
                choiceContext,
                Owner,
                3
            );

            int totalDamage = DynamicVars.Damage.IntValue +
                consumed * DynamicVars[DamagePerRemainsVar].IntValue;
            int block = DynamicVars.Block.IntValue +
                consumed * DynamicVars[BlockPerRemainsVar].IntValue;

            await CreatureCmd.GainBlock(
                Owner.Creature,
                block,
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );

            if (GuRank >= 6 && consumed >= 3)
            {
                await PowerCmd.Apply<WanHaiGuiChaoRetainBlockPower>(
                    choiceContext,
                    Owner.Creature,
                    block / 2,
                    Owner.Creature,
                    this
                );
            }

            if (CombatState == null)
            {
                return;
            }

            Creature[] enemies = GuZhenRenDeterminism
                .OrderCreatures(CombatState.HittableEnemies)
                .Where(enemy => enemy.IsAlive)
                .ToArray();

            if (enemies.Length == 0)
            {
                return;
            }

            int perEnemy = totalDamage / enemies.Length;
            int remainder = totalDamage % enemies.Length;

            for (int index = 0; index < enemies.Length; index++)
            {
                int damage = perEnemy + (index < remainder ? 1 : 0);
                await DamageCmd
                    .Attack(damage)
                    .FromCard(this, cardPlay)
                    .Targeting(enemies[index])
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
            }
        }
        finally
        {
            await AdvanceLifecycleAsync(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
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

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 16,
            2 => 18,
            3 => 20,
            4 => 24,
            5 => 28,
            6 => 36,
            7 => 44,
            8 => 54,
            _ => 66,
        };
        DynamicVars[DamagePerRemainsVar].BaseValue = GuRank switch
        {
            <= 2 => 10,
            <= 4 => 12,
            5 => 14,
            6 => 16,
            7 => 18,
            8 => 21,
            _ => 24,
        };
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 1 => 8,
            2 => 9,
            3 => 10,
            4 => 12,
            5 => 14,
            6 => 18,
            7 => 22,
            8 => 27,
            _ => 33,
        };
        DynamicVars[BlockPerRemainsVar].BaseValue = GuRank switch
        {
            <= 2 => 4,
            <= 4 => 5,
            5 => 6,
            6 => 8,
            7 => 9,
            8 => 10,
            _ => 12,
        };
    }
}
