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

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class BaiZhiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.BaiZhi;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("FirstBonus", 0m),
    ];

    public BaiZhiXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.BaiZhi,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(BaiZhiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = BaiZhiGu.DamageAtRank(GuRank);
        DynamicVars["FirstBonus"].BaseValue =
            BaiZhiGu.FirstManifestBonusAtRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class EXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.E;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
        new DynamicVar("SecondHitBonus", 0m),
    ];

    public EXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.E,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(ELiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = ELiGu.DamageAtRank(GuRank);
        DynamicVars["Hits"].BaseValue = 2m;
        DynamicVars["SecondHitBonus"].BaseValue =
            ELiGu.HitBonusAtRank(GuRank, 1);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class QingNiuXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.QingNiu;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new BlockVar(2m, ValueProp.Move),
        new DynamicVar("HitBlockBonus", 0m),
    ];

    public QingNiuXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.QingNiu,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(QingNiuLaoLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            QingNiuLaoLiGu.DamageAtRank(GuRank);
        DynamicVars.Block.BaseValue = QingNiuLaoLiGu.BlockAtRank(GuRank);
        DynamicVars["HitBlockBonus"].BaseValue =
            QingNiuLaoLiGu.HitBlockBonusAtRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class ShiGuiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.ShiGui;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("NoBlockBonus", 0m),
    ];

    public ShiGuiXuYing() : base(CardType.Skill, TargetType.Self) =>
        RefreshRankValues();

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.ShiGui,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(ShiGuiLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Block.BaseValue = ShiGuiLiGu.BlockAtRank(GuRank);
        DynamicVars["NoBlockBonus"].BaseValue =
            ShiGuiLiGu.NoBlockBonusAtRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class FeiXiongXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.FeiXiong;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.FeiXiong,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(FeiXiongZhiLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            FeiXiongZhiLiGu.DamageAtRank(GuRank);
        DynamicVars["BlockBonus"].BaseValue =
            FeiXiongZhiLiGu.BlockedTargetBonusAtRank(GuRank);
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
