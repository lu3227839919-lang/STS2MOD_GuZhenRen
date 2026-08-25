using System.Runtime.CompilerServices;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoXuYing :
    AbstractXuYingCard,
    ILiDaoPhantomCard,
    ICardRewardExcluded
{
    public abstract LiDaoBeastKind? BeastKind { get; }

    public virtual int PhantomSlotCost => 1;

    protected sealed override bool UsesCentralResolution => true;

    protected AbstractLiDaoXuYing(
        CardType type,
        TargetType target
    ) : base(0, type, target)
    {
    }

    internal int ScaleEffect(decimal value) =>
        Math.Max(
            0,
            (int)Math.Round(
                value * ResolutionMultiplier,
                MidpointRounding.AwayFromZero
            )
        );

    internal int ScaleDamage(decimal value) => ScaleEffect(value);

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Chance", (int)MathF.Round(BaseChance * 100f));
    }
}
internal static class LiDaoBeastEffectExecutor
{
    private sealed class RuntimeState
    {
        internal int BaiZhiTurn;
    }

    private static readonly ConditionalWeakTable<CardModel, RuntimeState>
        States = new();

    internal static async Task ExecuteAsync(
        AbstractLiDaoXuYing source,
        LiDaoBeastKind kind,
        int rank,
        PlayerChoiceContext choiceContext,
        Creature? target
    )
    {
        switch (kind)
        {
            case LiDaoBeastKind.BaiZhi:
                await ExecuteBaiZhi(source, rank, choiceContext, target);
                break;
            case LiDaoBeastKind.E:
                await ExecuteE(source, rank, choiceContext, target);
                break;
            case LiDaoBeastKind.QingNiu:
                await ExecuteQingNiu(source, rank, choiceContext, target);
                break;
            case LiDaoBeastKind.ShiGui:
                await ExecuteShiGui(source, rank);
                break;
            case LiDaoBeastKind.FeiXiong:
                await ExecuteFeiXiong(source, rank, choiceContext, target);
                break;
        }
    }

    private static async Task ExecuteBaiZhi(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target
    )
    {
        if (target == null)
        {
            return;
        }

        RuntimeState state = States.GetValue(source, static _ => new());
        int turn = source.Owner.PlayerCombatState?.TurnNumber ?? 1;
        int damage = BaiZhiGu.DamageAtRank(rank);
        if (state.BaiZhiTurn != turn)
        {
            state.BaiZhiTurn = turn;
            damage += BaiZhiGu.FirstManifestBonusAtRank(rank);
        }

        await Attack(source, context, target, source.ScaleDamage(damage));
    }

    private static async Task ExecuteE(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target
    )
    {
        if (target == null)
        {
            return;
        }

        for (int hit = 0; hit < 2 && target.IsAlive; hit++)
        {
            int damage = ELiGu.DamageAtRank(rank) +
                ELiGu.HitBonusAtRank(rank, hit);
            await Attack(source, context, target, source.ScaleDamage(damage));
        }
    }

    private static async Task ExecuteQingNiu(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target
    )
    {
        int bonusBlock = 0;
        if (target != null)
        {
            int damage = source.ScaleDamage(
                QingNiuLaoLiGu.DamageAtRank(rank)
            );
            int hpBefore = target.CurrentHp;
            await Attack(source, context, target, damage);
            if (target.CurrentHp < hpBefore)
            {
                bonusBlock = QingNiuLaoLiGu.HitBlockBonusAtRank(rank);
            }
        }

        await CreatureCmd.GainBlock(
            source.Owner.Creature,
            source.ScaleEffect(
                QingNiuLaoLiGu.BlockAtRank(rank) + bonusBlock
            ),
            ValueProp.Move,
            cardPlay: null
        );
    }

    private static Task ExecuteShiGui(
        AbstractLiDaoXuYing source,
        int rank
    )
    {
        int block = ShiGuiLiGu.BlockAtRank(rank);
        if (source.Owner.Creature.Block <= 0)
        {
            block += ShiGuiLiGu.NoBlockBonusAtRank(rank);
        }

        return CreatureCmd.GainBlock(
            source.Owner.Creature,
            source.ScaleEffect(block),
            ValueProp.Move,
            cardPlay: null
        );
    }

    private static async Task ExecuteFeiXiong(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target
    )
    {
        if (target == null)
        {
            return;
        }

        int damage = FeiXiongZhiLiGu.DamageAtRank(rank);
        if (target.Block > 0)
        {
            damage += FeiXiongZhiLiGu.BlockedTargetBonusAtRank(rank);
        }

        await Attack(source, context, target, source.ScaleDamage(damage));
    }

    private static Task Attack(
        AbstractLiDaoXuYing source,
        PlayerChoiceContext context,
        Creature target,
        int damage
    ) => DamageCmd.Attack(damage)
        .FromCard(source, cardPlay: null)
        .Targeting(target)
        .WithHitFx("vfx/vfx_attack_blunt")
        .Execute(context);
}
