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
        var combatState = CombatState;
        if (combatState is null)
        {
            return;
        }

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 七转 Replay 只重复伤害。流血、血元支付和血印分配
        // 全部只在首段结算。
        if (cardPlay.PlayIndex != 0)
        {
            return;
        }

        IReadOnlyList<Creature> survivingEnemies =
            GuZhenRenDeterminism.OrderCreatures(
                combatState.HittableEnemies
            )
            .Where(enemy => !enemy.IsDead)
            .ToList();

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

        if (survivingEnemies.Count == 0)
        {
            return;
        }

        int threshold = DynamicVars[BloodThresholdVar].IntValue;
        int totalMarks =
            XueDaoPowerSystem.GetXueYuan(this) / threshold;
        int cost = totalMarks * threshold;

        if (totalMarks <= 0 ||
            !await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                cost
            ))
        {
            return;
        }

        // 血印是总额度，不再把同一份血元完整复制给每个敌人。
        // 为保证多人确定性，按固定敌人顺序尽量均分，余数给靠前目标。
        int marksPerEnemy =
            totalMarks / survivingEnemies.Count;
        int remainder =
            totalMarks % survivingEnemies.Count;

        for (int index = 0;
             index < survivingEnemies.Count;
             index++)
        {
            int assignedMarks =
                marksPerEnemy + (index < remainder ? 1 : 0);

            if (assignedMarks <= 0)
            {
                continue;
            }

            await XueDaoPowerSystem.ApplyXueYin(
                choiceContext,
                this,
                survivingEnemies[index],
                assignedMarks
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
