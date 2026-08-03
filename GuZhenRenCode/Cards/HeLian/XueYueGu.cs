using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(YueMangGu),
    typeof(XueQiGu)
)]
public sealed class XueYueGu : AbstractHeLianGuCard
{
    private const string BloodThresholdVar = "BloodThreshold";

    public override int MaxGuRank => 7;

    public override int MaxUses => IsUpgraded ? 2 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new PowerVar<LiuXuePower>(1m),
        new DynamicVar(BloodThresholdVar, 4m),
    ];

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/XueYueGu.png"
        );

    public XueYueGu()
        : base(
            baseCost: 0,
            type: CardType.Attack,
            rarity: CardRarity.Uncommon,
            target: TargetType.AllEnemies
        )
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        IReadOnlyList<Creature> survivingEnemies =
            GuZhenRenDeterminism.OrderCreatures(
                Owner.Creature.CombatState?.HittableEnemies ?? []
            ).ToList();

        int bleed = DynamicVars[typeof(LiuXuePower).Name].IntValue;
        foreach (Creature enemy in survivingEnemies)
        {
            await XueDaoPowerSystem.ApplyLiuXue(
                choiceContext,
                this,
                enemy,
                bleed
            );
        }

        // 血元支付和血印结算只发生在出牌序列首段；Replay 只重放
        // 伤害与流血，避免同一份血元在多个段落被重复消费或结算。
        if (cardPlay.PlayIndex != 0)
        {
            return;
        }

        int threshold = DynamicVars[BloodThresholdVar].IntValue;
        int marks = XueDaoPowerSystem.GetXueYuan(this) / threshold;
        int cost = marks * threshold;

        if (marks <= 0 ||
            !await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                cost
            ))
        {
            return;
        }

        foreach (Creature enemy in survivingEnemies.Where(e => !e.IsDead))
        {
            await XueDaoPowerSystem.ApplyXueYin(
                choiceContext,
                this,
                enemy,
                marks
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
        DynamicVars[typeof(LiuXuePower).Name].BaseValue =
            1 + Math.Max(0, (GuRank - 1) / 2);
        DynamicVars[BloodThresholdVar].BaseValue =
            GuRank >= 6 ? 2 : 4;

        if (IsMutable)
        {
            BaseReplayCount = GuRank >= 7 ? 1 : 0;
        }
    }
}
