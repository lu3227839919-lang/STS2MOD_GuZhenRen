using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 血滴子：攻击全体、施加血印，并在每次催动时获得一张绑定本体的
/// 血云噬身。只有该衍生牌触发血印时，才会加快来源血滴子的恢复。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueDiZiGu : AbstractGuWormCard
{
    private const string BreedCapVar = "BreedCap";

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 3,
        <= 8 => 4,
        _ => 5,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new PowerVar<XueYinPower>(1m),
        new DynamicVar(BreedCapVar, 1m),
    ];

    // 暂用血翅蛊卡图，避免缺图时出现空白卡面。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: CardImageCatalog.GetResourcePath(typeof(XueChiGu))
    );

    public XueDiZiGu()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (CombatState == null)
        {
            return;
        }

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(CombatState.HittableEnemies)
            .Where(static target => target.IsAlive)
            .ToArray();

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_bloody_impact")
            .SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);

        int marks = DynamicVars[typeof(XueYinPower).Name].IntValue +
            (targets.Length == 1 ? 1 : 0);
        foreach (Creature target in targets.Where(static c => c.IsAlive))
        {
            await XueDaoPowerSystem.ApplyXueYin(
                choiceContext,
                this,
                target,
                marks
            );
        }

        XueYunShiShen derived =
            GuGeneratedCardFactory.Create<XueYunShiShen>(
                Owner,
                GuRank,
                upgraded: false
            );
        derived.BindSource(this);
        await GuGeneratedCardFactory.AddToHandOrDiscard(
            derived,
            Owner
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
    [
        GuCardReferenceFactory.Create<XueYunShiShen>(this, false),
    ];

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 3,
            2 => 4,
            3 => 5,
            4 => 6,
            5 => 7,
            6 => 9,
            7 => 11,
            8 => 13,
            _ => 16,
        };
        DynamicVars[typeof(XueYinPower).Name].BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 6 => 2,
            <= 8 => 3,
            _ => 4,
        };
        DynamicVars[BreedCapVar].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 8 => 2,
            _ => 3,
        };
    }

    protected override void OnUpgrade()
    {
    }
}

/// <summary>
/// 血滴子的战斗衍生牌。非消耗，正常进入弃牌堆并可在本场战斗中循环。
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XueYunShiShen : AbstractXueDaoToken
{
    private const string ExtraHitsVar = "ExtraHits";
    private const string BreedCapVar = "BreedCap";

    private static readonly SavedAttachedState<CardModel, string>
        BoundSourceState = new(
            Entry.ModId + ".xue_yun_shi_shen.bound_source",
            static () => string.Empty
        );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        new DynamicVar(ExtraHitsVar, 1m),
        new PowerVar<LiuXuePower>(1m),
        new DynamicVar(BreedCapVar, 1m),
    ];

    // 暂用刀翅血蝠衍生牌卡图，避免缺图时出现空白卡面。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: CardImageCatalog.GetResourcePath(typeof(DaoChiXueFu))
    );

    public XueYunShiShen()
        : base(1, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
    {
        RefreshRankValues();
    }

    internal void BindSource(XueDiZiGu source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (NetCombatCardDb.Instance.TryGetCardId(source, out uint netId))
        {
            BoundSourceState[this] = $"net:{netId}";
            return;
        }

        int deckIndex = GuZhenRenDeterminism.GetDeckCardIndex(source);
        BoundSourceState[this] = deckIndex == int.MaxValue
            ? string.Empty
            : $"deck:{deckIndex}";
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (CombatState == null)
        {
            return;
        }

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(CombatState.HittableEnemies)
            .Where(static target => target.IsAlive)
            .ToArray();
        Dictionary<Creature, int> marksBefore = targets.ToDictionary(
            static target => target,
            target => XueDaoPowerSystem
                .GetXueYin(target, Owner.Creature)?.Amount ?? 0,
            (IEqualityComparer<Creature>)ReferenceEqualityComparer.Instance
        );

        int extraHits = DynamicVars[ExtraHitsVar].IntValue;
        foreach (Creature target in targets)
        {
            int hits = 1 + (marksBefore[target] > 0 ? extraHits : 0);
            for (int hit = 0; hit < hits && target.IsAlive; hit++)
            {
                await DamageCmd
                    .Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_bloody_impact")
                    .Execute(choiceContext);
            }
        }

        int bleed = DynamicVars[typeof(LiuXuePower).Name].IntValue;
        foreach (Creature target in targets.Where(target =>
                     target.IsAlive && marksBefore[target] > 0))
        {
            await XueDaoPowerSystem.ApplyLiuXue(
                choiceContext,
                this,
                target,
                bleed
            );
        }

        int activatedMarks = targets.Count(target =>
            marksBefore[target] >
            (XueDaoPowerSystem
                .GetXueYin(target, Owner.Creature)?.Amount ?? 0)
        );
        AccelerateBoundSource(
            Math.Min(
                activatedMarks,
                DynamicVars[BreedCapVar].IntValue
            )
        );
    }

    private void AccelerateBoundSource(int turns)
    {
        if (turns <= 0 || ResolveBoundSource() is not { } source)
        {
            return;
        }

        int currentTurn = Owner.PlayerCombatState?.TurnNumber ?? 1;
        GuCardUsageRules.AccelerateRecoveryBy(
            source,
            turns,
            currentTurn
        );
    }

    private XueDiZiGu? ResolveBoundSource()
    {
        string encoded = BoundSourceState[this];
        if (encoded.StartsWith("net:", StringComparison.Ordinal) &&
            uint.TryParse(encoded.AsSpan(4), out uint netId) &&
            NetCombatCardDb.Instance.TryGetCard(netId, out CardModel? card) &&
            card is XueDiZiGu networkSource &&
            networkSource.Owner == Owner)
        {
            return networkSource;
        }

        if (encoded.StartsWith("deck:", StringComparison.Ordinal) &&
            int.TryParse(encoded.AsSpan(5), out int deckIndex) &&
            Owner.PlayerCombatState is { } combatState)
        {
            return combatState.AllCards
                .OfType<XueDiZiGu>()
                .FirstOrDefault(candidate =>
                    GuZhenRenDeterminism.GetDeckCardIndex(candidate) ==
                    deckIndex
                );
        }

        return null;
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 2,
            2 => 3,
            <= 4 => 4,
            <= 6 => 5,
            7 => 6,
            8 => 7,
            _ => 9,
        };
        DynamicVars[ExtraHitsVar].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 8 => 2,
            _ => 3,
        };
        DynamicVars[typeof(LiuXuePower).Name].BaseValue = GuRank switch
        {
            <= 6 => 1,
            _ => 2,
        };
        DynamicVars[BreedCapVar].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 8 => 2,
            _ => 3,
        };
        // The canonical model already has the rank-one cost from the
        // constructor and cannot be mutated during ModelDb initialization.
        if (!IsCanonical)
        {
            EnergyCost.SetCustomBaseCost(GuRank >= 9 ? 0 : 1);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
