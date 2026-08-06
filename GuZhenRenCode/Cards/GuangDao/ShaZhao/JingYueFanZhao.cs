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
        [CardKeyword.Exhaust];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public JingYueFanZhao()
        : base(
            baseCost: 2,
            type: CardType.Attack,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        RefreshRankValues();
    }

    /// <summary>
    /// 三阶段形态杀招：镜相→月相→返照，最终阶段后消耗并返还材料。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Staged;

    public override int MaxStages => 3;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await AdvanceLifecycleAsync(choiceContext);

        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int guangHui = DynamicVars["GuangHui"].IntValue;
        if (guangHui > 0 && cardPlay.PlayIndex == 0)
        {
            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                guangHui
            );
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
            <= 1 => 10,
            2 => 12,
            3 => 14,
            4 => 15,
            5 => 16,
            6 => 16,
            7 => 20,
            8 => 24,
            _ => 28,
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
