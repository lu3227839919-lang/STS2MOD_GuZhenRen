using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class YueGuangGu : AbstractGuWormCard
{
    private const int GuangHuiCost = 2;

    public override int MaxUses => IsUpgraded ? 2 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("BonusDamage", 3m),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/YueGuangGu.png"
        );

    public YueGuangGu()
        : base(
            baseCost: 0,
            type: CardType.Attack,
            rarity: CardRarity.Common,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
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

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

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
        int preImmortalIncrease = Math.Max(
            0,
            Math.Min(GuRank, 5) - 1
        );
        DynamicVars.Damage.BaseValue = 6 + preImmortalIncrease;
        DynamicVars[typeof(ZhaoPoPower).Name].BaseValue =
            1 + (GuRank >= 6 ? GuRank - 5 : 0);
        // 规范模型构造期间不可写 BaseReplayCount；转数
        // 赋值、读档和升转会在可变卡牌实例上刷新。
        if (IsMutable)
        {
            BaseReplayCount = GuRank >= 6 ? 1 : 0;
        }
    }
}
