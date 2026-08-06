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
/// 白虹贯日由月光蛊与流光蛊推演而成。
/// 消耗目标已有照破提高伤害；光辉足够时自动支付，并无视目标至多固定数值的格挡。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(YueGuangGu), typeof(LiuGuangGu))]
[ShaZhaoRecipe(typeof(LiuGuangGu), typeof(YueGuangGu))]
public sealed class BaiHongGuanRi : AbstractShaZhaoCard
{
    private const int GuangHuiCost = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("MaxZhaoPoConsumed", 1m),
        new DynamicVar("DamagePerZhaoPo", 4m),
        new DynamicVar("PierceCap", 8m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain, CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public BaiHongGuanRi()
        : base(
            baseCost: 2,
            type: CardType.Attack,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("GuangHuiCost", GuangHuiCost);
    }

    /// <summary>
    /// 瞬发终结杀招：使用一次后消耗并返还材料。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Instant;

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

        ZhaoPoPower? zhaoPoPower = target.GetPower<ZhaoPoPower>();
        int consumed = Math.Min(
            zhaoPoPower?.Amount ?? 0,
            DynamicVars["MaxZhaoPoConsumed"].IntValue
        );

        if (zhaoPoPower != null && consumed > 0)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                zhaoPoPower,
                -consumed,
                Owner.Creature,
                this
            );
        }

        decimal damage = DynamicVars.Damage.BaseValue +
            consumed * DynamicVars["DamagePerZhaoPo"].BaseValue;

        bool empowered = await GuangDaoPowerSystem.TryAutoSpendGuangHui(
            choiceContext,
            this,
            cardPlay,
            GuangHuiCost
        );

        decimal piercedDamage = empowered
            ? Math.Min(
                DynamicVars["PierceCap"].BaseValue,
                Math.Min(damage, (decimal)target.Block)
            )
            : 0m;

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 普通攻击已经照常消耗格挡；额外的不可格挡伤害使本次攻击
        // 等效无视至多 PierceCap 点原有格挡，但不会在无格挡时白送伤害。
        if (piercedDamage > 0 && !target.IsDead)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                piercedDamage,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                this,
                cardPlay
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
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 14,
            2 => 17,
            3 => 20,
            4 => 24,
            5 => 28,
            6 => 36,
            7 => 42,
            8 => 50,
            _ => 60,
        };
        DynamicVars["MaxZhaoPoConsumed"].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 7 => 2,
            _ => 3,
        };
        DynamicVars["DamagePerZhaoPo"].BaseValue = GuRank switch
        {
            <= 4 => 4,
            5 => 5,
            6 => 6,
            7 => 7,
            _ => 8,
        };
        DynamicVars["PierceCap"].BaseValue = GuRank switch
        {
            <= 1 => 8,
            2 => 10,
            3 => 12,
            4 => 14,
            5 => 16,
            6 => 20,
            7 => 24,
            8 => 28,
            _ => 32,
        };
    }
}
