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

    // 使用 images/cards/XueYunShiShen.png 同名卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(XueYunShiShen));

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
