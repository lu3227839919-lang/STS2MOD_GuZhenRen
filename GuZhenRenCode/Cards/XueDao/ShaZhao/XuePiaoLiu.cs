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
[ShaZhaoRecipe(typeof(XueYueGu), typeof(DaoChiXueFuGu))]
[ShaZhaoRecipe(typeof(DaoChiXueFuGu), typeof(XueYueGu))]
public sealed class XuePiaoLiu : AbstractShaZhaoCard
{
    private const string MaxBleedConsumedVar = "MaxBleedConsumed";
    private const string DamagePerBleedVar = "DamagePerBleed";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar(MaxBleedConsumedVar, 2m),
        new DynamicVar(DamagePerBleedVar, 3m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public XuePiaoLiu()
        : base(2, CardType.Attack, TargetType.AnyEnemy)
    {
        SetDao(Dao.XueDao);
        RefreshRankValues();
    }

    /// <summary>
    /// 两次型范围攻击杀招：每场最多使用 2 次。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Charged;

    public override int ShaZhaoMaxUses => 2;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            Creature? destination = cardPlay.Target;
            if (destination == null || !IsValidTarget(destination) ||
                CombatState == null)
            {
                return;
            }

            List<Creature> route = GuZhenRenDeterminism
                .OrderCreatures(CombatState.HittableEnemies)
                .Where(enemy => enemy.IsAlive && !ReferenceEquals(enemy, destination))
                .ToList();
            route.Add(destination);

            int carry = 0;
            int maximumConsumed = DynamicVars[MaxBleedConsumedVar].IntValue;
            int damagePerBleed = DynamicVars[DamagePerBleedVar].IntValue;

            foreach (Creature enemy in route)
            {
                LiuXuePower? power = XueDaoPowerSystem.GetLiuXue(
                    enemy,
                    Owner.Creature
                );
                int totalBleed = (power?.Amount ?? 0) + carry;
                int consumed = Math.Min(maximumConsumed, totalBleed);
                int remaining = Math.Max(0, totalBleed - consumed);

                await XueDaoPowerSystem.SetLiuXueAmount(
                    choiceContext,
                    this,
                    enemy,
                    0
                );

                if (enemy.IsAlive)
                {
                    await DamageCmd
                        .Attack(
                            DynamicVars.Damage.BaseValue +
                            consumed * damagePerBleed
                        )
                        .FromCard(this, cardPlay)
                        .Targeting(enemy)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }

                if (ReferenceEquals(enemy, destination))
                {
                    // 六转质变：血潮抵达终点时，按本次经过的敌人数量
                    // 对终点额外施加流血（九转上限 4 层）。
                    if (GuRank >= 6 && enemy.IsAlive)
                    {
                        int maxExtraBleed = GuRank >= 9 ? 4 : 3;
                        int extraBleed = Math.Min(
                            maxExtraBleed,
                            route.Count
                        );
                        if (extraBleed > 0)
                        {
                            await XueDaoPowerSystem.ApplyLiuXue(
                                choiceContext,
                                this,
                                enemy,
                                extraBleed
                            );
                        }
                    }

                    if (remaining > 0 && enemy.IsAlive)
                    {
                        await XueDaoPowerSystem.ApplyLiuXue(
                            choiceContext,
                            this,
                            enemy,
                            remaining
                        );
                    }
                    carry = 0;
                }
                else
                {
                    carry = remaining;
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
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 4,
            2 => 5,
            3 => 6,
            4 => 7,
            5 => 8,
            6 => 10,
            7 => 12,
            8 => 14,
            _ => 16,
        };
        DynamicVars[MaxBleedConsumedVar].BaseValue = GuRank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            <= 7 => 3,
            _ => 4,
        };
        DynamicVars[DamagePerBleedVar].BaseValue = GuRank switch
        {
            <= 2 => 2,
            <= 5 => 3,
            <= 7 => 4,
            <= 8 => 5,
            _ => 6,
        };
    }
}
