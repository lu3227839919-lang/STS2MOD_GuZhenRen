using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.XueDao;
using GuZhenRen.Multiplayer;

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
        CardKeyword.Exhaust,
        GuZhenRenKeywords.YiHai,
        GuZhenRenKeywords.ZongEDu,
    ];

    public override bool GainsBlock => true;

    public WanHaiGuiChao()
        : base(3, CardType.Attack, TargetType.AllEnemies)
    {
        SetDao(Dao.XueDao);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
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
            <= 5 => 20,
            6 => 24,
            7 => 30,
            8 => 36,
            _ => 42,
        };
        DynamicVars[DamagePerRemainsVar].BaseValue = GuRank switch
        {
            <= 6 => 14,
            7 => 16,
            8 => 18,
            _ => 20,
        };
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 5 => 10,
            6 => 12,
            7 => 15,
            8 => 18,
            _ => 21,
        };
        DynamicVars[BlockPerRemainsVar].BaseValue = GuRank switch
        {
            <= 6 => 6,
            7 => 7,
            8 => 8,
            _ => 9,
        };
    }
}
