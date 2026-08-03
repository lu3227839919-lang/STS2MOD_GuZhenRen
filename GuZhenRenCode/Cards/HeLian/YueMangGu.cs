using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(YueGuangGu),
    typeof(XiaoGuangGu),
    typeof(XiaoGuangGu)
)]
public sealed class YueMangGu : AbstractHeLianGuCard
{
    private const int GuangHuiCost = 2;

    public override int MaxUses => IsUpgraded ? 3 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("BonusDamage", 2m),
        new RepeatVar("Hits", 2),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/YueMangGu.png"
        );

    public YueMangGu()
        : base(
            baseCost: 0,
            type: CardType.Attack,
            rarity: CardRarity.Uncommon,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("GuangHuiCost", GuangHuiCost);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        bool empowered = await GuangDaoPowerSystem
            .TrySpendGuangHui(choiceContext, this, GuangHuiCost);
        decimal damage = DynamicVars.Damage.BaseValue;

        if (empowered)
        {
            damage += DynamicVars["BonusDamage"].BaseValue;
        }

        int hitCount = DynamicVars["Hits"].IntValue;
        for (int hit = 0; hit < hitCount; hit++)
        {
            using IDisposable? suppression =
                GuRank < 6 && hit > 0
                    ? ZhaoPoTriggerScope.Suppress()
                    : null;

            await DamageCmd
                .Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (empowered)
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Hits"].BaseValue =
            2 + Math.Max(0, (GuRank - 1) / 2);
    }
}
