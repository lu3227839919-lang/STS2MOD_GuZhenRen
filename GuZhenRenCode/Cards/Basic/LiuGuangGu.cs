using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

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

namespace GuZhenRen.Cards.GuangDao;

/// <summary>
/// 流光蛊是强调攻击/技能交替与恢复期追击的光道攻击蛊。
/// 低转只提供稳定攻击；三转起生成流光；九转生成白虹。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class LiuGuangGu
    : AbstractGuWormCard,
      IGuRecoveryEffectSource
{
    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".liu_guang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".liu_guang.pending_generation",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        EmpoweredTokenState = new(
            Entry.ModId + ".liu_guang.empowered_token",
            static () => false
        );

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new DynamicVar("RefractionBonus", 2m),
    ];

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public LiuGuangGu()
        : base(
            baseCost: 0,
            type: CardType.Attack,
            rarity: CardRarity.Uncommon,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
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

        bool refracted = Owner.Creature.GetPower<ZheGuangPower>()?
            .GuangHuiGainedThisTurn > 0;
        decimal damage = DynamicVars.Damage.BaseValue;
        if (GuRank >= 2 && refracted)
        {
            damage += DynamicVars["RefractionBonus"].BaseValue;
        }

        if (cardPlay.PlayIndex == 0)
        {
            EmpoweredTokenState[this] = GuRank >= 5 && refracted;
        }

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public void ResetRecoveryEffectState()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        EmpoweredTokenState[this] = false;
    }

    public async Task OnEnteredRecoveryAsync()
    {
        if (RecoveryHandledState[this])
        {
            return;
        }

        RecoveryHandledState[this] = true;
        if (GuRank < 3)
        {
            return;
        }

        if (GuRank == 3)
        {
            LiuGuang token = CreatePrimaryToken<LiuGuang>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                token,
                Owner
            );
            return;
        }

        PendingGenerationState[this] = true;
    }

    public async Task OnRecoveryTurnStartAsync(int turnNumber)
    {
        if (!PendingGenerationState[this])
        {
            return;
        }

        PendingGenerationState[this] = false;
        AbstractGuZhenRenCard primary = GuRank >= 9
            ? CreatePrimaryToken<BaiHong>()
            : CreatePrimaryToken<LiuGuang>();

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            primary,
            Owner
        );

        if (GuRank == 8)
        {
            LiuGuang second = CreatePrimaryToken<LiuGuang>();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                second,
                Owner
            );
        }
    }

    public Task OnRecoveredAsync()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        EmpoweredTokenState[this] = false;
        return Task.CompletedTask;
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    public override IReadOnlyList<CardModel> GetCarouselCards()
    {
        if (GuRank < 3)
        {
            return [];
        }

        bool upgraded = IsUpgraded ||
            GuRank >= 6 ||
            EmpoweredTokenState[this];

        if (GuRank >= 9)
        {
            return
            [
                GuCardReferenceFactory.Create<BaiHong>(
                    this,
                    upgraded
                ),
            ];
        }

        List<CardModel> cards =
        [
            GuCardReferenceFactory.Create<LiuGuang>(
                this,
                upgraded
            ),
        ];

        if (GuRank >= 7)
        {
            cards.Add(
                GuCardReferenceFactory.Create<LiuHui>(this)
            );
        }

        return cards;
    }

    private T CreatePrimaryToken<T>()
        where T : AbstractGuZhenRenCard
    {
        bool upgraded = IsUpgraded ||
            GuRank >= 6 ||
            EmpoweredTokenState[this];

        return GuGeneratedCardFactory.Create<T>(
            Owner,
            GuRank,
            upgraded
        );
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 7,
            2 => 8,
            3 => 9,
            4 => 10,
            5 => 12,
            6 => 15,
            7 => 17,
            8 => 20,
            _ => 24,
        };
    }
}
