using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 镜月返照由月光蛊与镜光蛊推演而成。
/// 同时获得格挡并以月光反射攻击目标，提供稳定的攻防转换。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(YueGuangGu), typeof(JingGuangGu))]
[ShaZhaoRecipe(typeof(JingGuangGu), typeof(YueGuangGu))]
public sealed class JingYueFanZhao : AbstractShaZhaoCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("GuangHui", 0m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public JingYueFanZhao()
        : base(
            baseCost: 1,
            type: CardType.Attack,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        RefreshRankValues();
    }

    /// <summary>
    /// 三阶段形态杀招：镜相→月相→（六转以上）返照。
    /// 一至五转两阶段后解体，六至九转三阶段后解体。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Staged;

    public override int MaxStages =>
        GuRank >= 6 ? 3 : 2;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        // 先读取本次阶段，待效果完成后再提交生命周期状态。这样最终
        // 阶段的材料返还不会在卡牌效果之前发生。
        int stage = Math.Clamp(CurrentStage + 1, 1, MaxStages);
        try
        {
            decimal fullValue = DynamicVars.Damage.BaseValue;

            switch (stage)
            {
                // 第一阶段：镜相——获得完整数值的格挡。
                case 1:
                    await CreatureCmd.GainBlock(
                        Owner.Creature,
                        new BlockVar(fullValue, ValueProp.Move),
                        cardPlay
                    );
                    break;

                // 第二阶段：月相——对一个敌人造成完整数值伤害。
                case 2:
                {
                    Creature? target = cardPlay.Target;
                    if (target == null || !IsValidTarget(target))
                    {
                        return;
                    }

                    await DamageCmd
                        .Attack(fullValue)
                        .FromCard(this, cardPlay)
                        .Targeting(target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                    break;
                }

                // 第三阶段：返照——仅六转以上存在。
                // 获得一半数值的格挡，并对所有敌人造成一半数值伤害；
                // 七至八转获得 1 点光辉，九转获得 2 点。
                default:
                {
                    decimal halfValue = fullValue / 2m;

                    await CreatureCmd.GainBlock(
                        Owner.Creature,
                        new BlockVar(halfValue, ValueProp.Move),
                        cardPlay
                    );

                    if (CombatState != null)
                    {
                        foreach (Creature enemy in
                                 CombatState.HittableEnemies)
                        {
                            await DamageCmd
                                .Attack(halfValue)
                                .FromCard(this, cardPlay)
                                .Targeting(enemy)
                                .WithHitFx("vfx/vfx_attack_slash")
                                .Execute(choiceContext);
                        }
                    }

                    int guangHui =
                        DynamicVars["GuangHui"].IntValue;
                    if (guangHui > 0)
                    {
                        await GuangDaoPowerSystem.GainGuangHui(
                            choiceContext,
                            this,
                            guangHui
                        );
                    }
                    break;
                }
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
        decimal value = GuRank switch
        {
            <= 1 => 8,
            2 => 10,
            3 => 12,
            4 => 14,
            5 => 16,
            6 => 20,
            7 => 23,
            8 => 26,
            _ => 30,
        };

        DynamicVars.Block.BaseValue = value;
        DynamicVars.Damage.BaseValue = value;
        DynamicVars["GuangHui"].BaseValue = GuRank switch
        {
            <= 6 => 0,
            <= 8 => 1,
            _ => 2,
        };
    }
}
