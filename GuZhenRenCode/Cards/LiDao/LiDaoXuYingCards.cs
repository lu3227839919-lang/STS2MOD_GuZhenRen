using System.Runtime.CompilerServices;

using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoXuYing :
    AbstractXuYingCard,
    ILiDaoPhantomCard,
    ICardRewardExcluded
{
    public abstract LiDaoBeastKind? BeastKind { get; }

    public virtual bool IsFullForcePhantom => false;

    public virtual int PhantomSlotCost => IsFullForcePhantom ? 0 : 1;

    public virtual IReadOnlyCollection<LiDaoBeastKind>
        LastManifestedKinds => BeastKind is { } kind ? [kind] : [];

    protected sealed override bool UsesCentralResolution => true;

    protected virtual decimal IntrinsicEffectMultiplier => 1m;

    protected AbstractLiDaoXuYing(
        CardType type,
        TargetType target
    ) : base(0, type, target)
    {
    }

    internal decimal GetCombinedEffectMultiplier() =>
        ResolutionMultiplier *
        IntrinsicEffectMultiplier *
        LiDaoPowerSystem.GetBeastEffectMultiplier(Owner.Creature);

    internal int ScaleEffect(decimal value) =>
        Math.Max(
            0,
            (int)Math.Round(
                value * GetCombinedEffectMultiplier(),
                MidpointRounding.AwayFromZero
            )
        );

    internal virtual void Condense()
    {
        float gain = LiDaoRankTable.CondenseChanceGain(GuRank) / 100f;
        float capped = Math.Min(0.8f, BaseChance + gain);
        IncreaseBaseChance(capped - BaseChance);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "Chance",
            (int)MathF.Round(BaseChance * 100f)
        );
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class BaiZhiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.BaiZhi;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5m, ValueProp.Move)];

    public BaiZhiXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.BaiZhi,
        GuRank,
        choiceContext,
        target,
        LiDaoPhantomSystem.OtherManifestedForCurrentAttack
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.BaiZhiChance(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = LiDaoRankTable.BaiZhiDamage(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class FeiXiongXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.FeiXiong;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("DivineMight", 0m),
    ];

    public FeiXiongXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.FeiXiong,
        GuRank,
        choiceContext,
        target,
        LiDaoPhantomSystem.OtherManifestedForCurrentAttack
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.FeiXiongChance(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.FeiXiongDamage(GuRank);
        DynamicVars["DivineMight"].BaseValue =
            LiDaoRankTable.FeiXiongDivineMight(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class EXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.E;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public EXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.E,
        GuRank,
        choiceContext,
        target,
        LiDaoPhantomSystem.OtherManifestedForCurrentAttack
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.EChance(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = LiDaoRankTable.EDamage(GuRank);
        DynamicVars["Hits"].BaseValue = LiDaoRankTable.EHits(GuRank);
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
    ];

    public QingNiuXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.QingNiu,
        GuRank,
        choiceContext,
        target,
        LiDaoPhantomSystem.OtherManifestedForCurrentAttack
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.QingNiuChance(GuRank) / 100f);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.QingNiuDamage(GuRank);
        DynamicVars.Block.BaseValue =
            LiDaoRankTable.QingNiuBlock(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class ShiGuiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.ShiGui;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move)];

    public ShiGuiXuYing() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.ShiGui,
        GuRank,
        choiceContext,
        target,
        LiDaoPhantomSystem.OtherManifestedForCurrentAttack
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.ShiGuiChance(GuRank) / 100f);
        DynamicVars.Block.BaseValue = LiDaoRankTable.ShiGuiBlock(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class BaiShouXuYing : AbstractLiDaoXuYing
{
    private static readonly SavedAttachedState<CardModel, string>
        CompositionState = new(
            Entry.ModId + ".li_dao.bai_shou_phantom_composition",
            static () => string.Empty
        );

    private static readonly SavedAttachedState<CardModel, int>
        CondenseCountState = new(
            Entry.ModId + ".li_dao.bai_shou_condense_count",
            static () => 0
        );

    private string _composition = string.Empty;
    private int _lastSelected = -1;
    private LiDaoBeastKind[] _lastManifestedKinds = [];

    public override LiDaoBeastKind? BeastKind => null;

    public override IReadOnlyCollection<LiDaoBeastKind>
        LastManifestedKinds => _lastManifestedKinds;

    protected override decimal IntrinsicEffectMultiplier =>
        LiDaoRankTable.BaiShouEffectPercent(GuRank) / 100m +
        (GuRank >= 6
            ? Math.Min(3, CondenseCountState[this] / 2) * 0.10m
            : 0m);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EffectPercent", 60m),
        new DynamicVar("InternalBonusPercent", 0m),
    ];

    public BaiShouXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    internal void ConfigureComposition(
        IReadOnlyList<LiDaoBeastKind> kinds
    )
    {
        LiDaoBeastKind[] normalized = kinds
            .Distinct()
            .OrderBy(kind => kind)
            .Take(3)
            .ToArray();

        if (normalized.Length != 3)
        {
            throw new ArgumentException(
                "百兽虚影需要三种不同兽力。",
                nameof(kinds)
            );
        }

        _composition = string.Join(',', normalized.Select(kind => (int)kind));
        CompositionState[this] = _composition;
    }

    protected override async Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        LiDaoBeastKind[] choices = GetComposition();
        Rng rng = RitsuLibFramework.GetModPlayerRng(
            Owner,
            Entry.ModId,
            "li_dao/bai_shou"
        );

        LiDaoBeastKind first = SelectKind(
            choices,
            rng,
            GuRank >= 7 ? _lastSelected : -1
        );
        _lastSelected = (int)first;
        List<LiDaoBeastKind> manifested = [first];

        await LiDaoBeastEffectExecutor.ExecuteAsync(
            this,
            first,
            GuRank,
            choiceContext,
            target,
            LiDaoPhantomSystem.OtherManifestedForCurrentAttack
        );

        bool triggerSecond = GuRank >= 9 ||
            (GuRank >= 8 && rng.NextInt(100) < 35);

        if (triggerSecond)
        {
            LiDaoBeastKind second = SelectKind(choices, rng, (int)first);
            manifested.Add(second);
            _lastSelected = (int)second;

            await LiDaoBeastEffectExecutor.ExecuteAsync(
                this,
                second,
                GuRank,
                choiceContext,
                target,
                otherManifested: true
            );
        }

        _lastManifestedKinds = manifested.Distinct().ToArray();
    }

    internal override void Condense()
    {
        base.Condense();
        CondenseCountState[this]++;
        DynamicVars["InternalBonusPercent"].BaseValue =
            GuRank >= 6
                ? Math.Min(3, CondenseCountState[this] / 2) * 10
                : 0;
    }

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(LiDaoRankTable.BaiShouChance(GuRank) / 100f);
        DynamicVars["EffectPercent"].BaseValue =
            LiDaoRankTable.BaiShouEffectPercent(GuRank);
        DynamicVars["InternalBonusPercent"].BaseValue =
            GuRank >= 6
                ? Math.Min(3, CondenseCountState[this] / 2) * 10
                : 0;
    }

    private LiDaoBeastKind[] GetComposition()
    {
        string serialized = CompositionState[this];
        if (string.IsNullOrWhiteSpace(serialized))
        {
            serialized = _composition;
        }

        LiDaoBeastKind[] parsed = serialized
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out int kind) ? kind : -1)
            .Where(kind => Enum.IsDefined(typeof(LiDaoBeastKind), kind))
            .Select(kind => (LiDaoBeastKind)kind)
            .Distinct()
            .Take(3)
            .ToArray();

        return parsed.Length == 3
            ? parsed
            :
            [
                LiDaoBeastKind.BaiZhi,
                LiDaoBeastKind.E,
                LiDaoBeastKind.QingNiu,
            ];
    }

    private static LiDaoBeastKind SelectKind(
        IReadOnlyList<LiDaoBeastKind> choices,
        Rng rng,
        int excluded
    )
    {
        LiDaoBeastKind[] eligible = choices
            .Where(kind => (int)kind != excluded)
            .ToArray();
        return rng.NextItem(eligible.Length > 0 ? eligible : choices.ToArray());
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class QuanLiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => null;

    public override bool IsFullForcePhantom => true;

    // 与全力以赴蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(QuanLiYiFuGu));

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("EffectPercent", 100m)];

    public QuanLiXuYing() : base(CardType.Skill, TargetType.Self)
    {
        SetBaseChance(1f);
        RefreshRankValues();
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => Task.CompletedTask;

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues() =>
        DynamicVars["EffectPercent"].BaseValue =
            LiDaoRankTable.FullForcePercent(GuRank);
}

internal static class LiDaoBeastEffectExecutor
{
    private sealed class RuntimeState
    {
        internal int ManifestedMask;
        internal int FeiXiongTurn;
        internal int ShiGuiTurn;
    }

    private static readonly ConditionalWeakTable<CardModel, RuntimeState>
        States = new();

    internal static async Task ExecuteAsync(
        AbstractLiDaoXuYing source,
        LiDaoBeastKind kind,
        int rank,
        PlayerChoiceContext choiceContext,
        Creature? target,
        bool otherManifested
    )
    {
        RuntimeState state = States.GetValue(
            source,
            static _ => new RuntimeState()
        );
        int turn = source.Owner.PlayerCombatState?.TurnNumber ?? 1;
        bool firstEver = (state.ManifestedMask & (1 << (int)kind)) == 0;
        state.ManifestedMask |= 1 << (int)kind;

        switch (kind)
        {
            case LiDaoBeastKind.BaiZhi:
                await ExecuteBaiZhi(source, rank, choiceContext, target, firstEver);
                break;
            case LiDaoBeastKind.FeiXiong:
                bool firstThisTurn = state.FeiXiongTurn != turn;
                state.FeiXiongTurn = turn;
                await ExecuteFeiXiong(
                    source,
                    rank,
                    choiceContext,
                    target,
                    firstThisTurn
                );
                break;
            case LiDaoBeastKind.E:
                await ExecuteE(source, rank, choiceContext, target);
                break;
            case LiDaoBeastKind.QingNiu:
                await ExecuteQingNiu(
                    source,
                    rank,
                    choiceContext,
                    target,
                    otherManifested,
                    firstEver
                );
                break;
            case LiDaoBeastKind.ShiGui:
                bool firstTurtleThisTurn = state.ShiGuiTurn != turn;
                state.ShiGuiTurn = turn;
                await ExecuteShiGui(
                    source,
                    rank,
                    choiceContext,
                    firstTurtleThisTurn
                );
                break;
        }
    }

    private static async Task ExecuteBaiZhi(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target,
        bool firstEver
    )
    {
        if (target == null)
        {
            return;
        }

        int damage = LiDaoRankTable.BaiZhiDamage(rank);
        if (firstEver && rank is >= 3 and <= 5)
        {
            damage += rank == 5 ? 3 : 2;
        }
        if (target.Block <= 0 && rank >= 7)
        {
            damage += rank switch { 7 => 3, 8 => 4, _ => 5 };
        }

        await Attack(source, context, target, source.ScaleEffect(damage));
    }

    private static async Task ExecuteFeiXiong(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target,
        bool firstThisTurn
    )
    {
        if (target == null)
        {
            return;
        }

        decimal rankNineMultiplier = rank >= 9 && firstThisTurn ? 1.5m : 1m;
        int total = LiDaoRankTable.FeiXiongDamage(rank);
        if (target.Block > 0 && rank is >= 3 and <= 5)
        {
            total += rank switch { 3 => 4, 4 => 5, _ => 6 };
        }

        int divine = LiDaoRankTable.FeiXiongDivineMight(rank);
        int normalDamage = source.ScaleEffect(
            Math.Max(0, total - divine) * rankNineMultiplier
        );
        int divineDamage = source.ScaleEffect(divine * rankNineMultiplier);

        if (normalDamage > 0)
        {
            await Attack(source, context, target, normalDamage);
        }
        if (divineDamage > 0 && target.IsAlive)
        {
            await CreatureCmd.Damage(
                context,
                target,
                divineDamage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                source.Owner.Creature,
                source,
                cardPlay: null
            );
        }

        if (rank >= 8 && source.CombatState != null)
        {
            int quake = source.ScaleEffect(6m * rankNineMultiplier);
            foreach (Creature enemy in GuZhenRenDeterminism
                         .OrderCreatures(source.CombatState.HittableEnemies)
                         .Where(enemy => enemy.IsAlive && !ReferenceEquals(enemy, target)))
            {
                await CreatureCmd.Damage(
                    context,
                    enemy,
                    quake,
                    ValueProp.Unpowered,
                    source.Owner.Creature,
                    source,
                    cardPlay: null
                );
            }
        }
    }

    private static async Task ExecuteE(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target
    )
    {
        Creature? current = target;
        int hits = LiDaoRankTable.EHits(rank);

        for (int hit = 0; hit < hits; hit++)
        {
            if (current == null || current.IsDead)
            {
                if (rank < 7)
                {
                    break;
                }
                current = SelectPursuitTarget(source);
                if (current == null)
                {
                    break;
                }
            }

            int damage = LiDaoRankTable.EDamage(rank);
            if (rank == 3 && hit == 1)
            {
                damage += 2;
            }

            await Attack(source, context, current, source.ScaleEffect(damage));
        }
    }

    private static async Task ExecuteQingNiu(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        Creature? target,
        bool otherManifested,
        bool firstEver
    )
    {
        if (target != null)
        {
            await Attack(
                source,
                context,
                target,
                source.ScaleEffect(LiDaoRankTable.QingNiuDamage(rank))
            );
        }

        int block = LiDaoRankTable.QingNiuBlock(rank);
        if (rank >= 8 && firstEver)
        {
            block += 3;
        }
        if (rank >= 9 && otherManifested)
        {
            block += 5;
        }

        await CreatureCmd.GainBlock(
            source.Owner.Creature,
            source.ScaleEffect(block),
            ValueProp.Move,
            cardPlay: null
        );
    }

    private static Task ExecuteShiGui(
        AbstractLiDaoXuYing source,
        int rank,
        PlayerChoiceContext context,
        bool firstThisTurn
    )
    {
        int block = LiDaoRankTable.ShiGuiBlock(rank);
        if (source.Owner.Creature.Block <= 0 && rank is >= 5 and <= 7)
        {
            block += rank switch { 5 => 2, 6 => 3, _ => 4 };
        }
        if (rank == 8 && firstThisTurn)
        {
            block += 5;
        }
        if (rank >= 9 && firstThisTurn)
        {
            block += (int)Math.Round(block * 0.5m, MidpointRounding.AwayFromZero);
        }

        return CreatureCmd.GainBlock(
            source.Owner.Creature,
            source.ScaleEffect(block),
            ValueProp.Move,
            cardPlay: null
        );
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

    private static Creature? SelectPursuitTarget(AbstractLiDaoXuYing source) =>
        source.CombatState == null
            ? null
            : GuZhenRenDeterminism
                .OrderCreatures(source.CombatState.HittableEnemies)
                .Where(enemy => enemy.IsAlive)
                .OrderBy(enemy => enemy.CurrentHp)
                .ThenBy(enemy => enemy.CombatId)
                .FirstOrDefault();
}
