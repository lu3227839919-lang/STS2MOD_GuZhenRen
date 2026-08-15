using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class YueGuangGu
    : AbstractGuWormCard,
      IGuRecoveryEffectSource
{
    private const int GuangHuiCost = 2;

    private static readonly SavedAttachedState<CardModel, bool>
        RecoveryHandledState = new(
            Entry.ModId + ".yue_guang.recovery_handled",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        PendingGenerationState = new(
            Entry.ModId + ".yue_guang.pending_generation",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        LastActivationEmpoweredState = new(
            Entry.ModId + ".yue_guang.last_empowered",
            static () => false
        );

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BonusDamage", 4m),
        new DynamicVar("ZhaoPoConditionBonus", 2m),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

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

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("GuangHuiCost", GuangHuiCost);
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

        // 月光蛊从一转起即可耀化。五转不再是耀化解锁门槛，
        // 只保留“耀化使恢复生成牌升级”的额外成长。
        bool empowered = await GuangDaoPowerSystem.TryAutoSpendGuangHui(
                choiceContext,
                this,
                cardPlay,
                GuangHuiCost
            );

        if (cardPlay.IsFirstInSeries)
        {
            LastActivationEmpoweredState[this] = empowered;
        }
        else if (GuRank >= 6 && !empowered)
        {
            // 六转以上仅耀化时才让 Replay 段生效。
            return;
        }

        bool targetHadZhaoPo = target.GetPower<ZhaoPoPower>() is
            { Amount: > 0 };
        decimal damage = DynamicVars.Damage.BaseValue;

        if (GuRank >= 2 && targetHadZhaoPo)
        {
            damage += DynamicVars["ZhaoPoConditionBonus"].BaseValue;
        }

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

        if (empowered && cardPlay.IsFirstInSeries)
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );
        }
    }

    public void ResetRecoveryEffectState()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        LastActivationEmpoweredState[this] = false;
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
            YueRen moonblade = CreateYueRen();
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                moonblade,
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

        AbstractGuZhenRenCard generated =
            GuRank >= 9 && LastActivationEmpoweredState[this]
                ? GuGeneratedCardFactory.Create<ManYueRen>(
                    Owner,
                    GuRank,
                    upgraded: false
                )
                : CreateYueRen();

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            generated,
            Owner
        );
    }

    public Task OnRecoveredAsync()
    {
        RecoveryHandledState[this] = false;
        PendingGenerationState[this] = false;
        LastActivationEmpoweredState[this] = false;
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

        bool moonbladeUpgraded =
            GuRank >= 5 && LastActivationEmpoweredState[this];

        if (GuRank >= 9)
        {
            return
            [
                GuCardReferenceFactory.Create<YueRen>(
                    this,
                    moonbladeUpgraded
                ),
                GuCardReferenceFactory.Create<CanYue>(this),
                GuCardReferenceFactory.Create<ManYueRen>(
                    this,
                    false
                ),
            ];
        }

        List<CardModel> cards =
        [
            GuCardReferenceFactory.Create<YueRen>(
                this,
                moonbladeUpgraded
            ),
        ];

        if (GuRank >= 7)
        {
            cards.Add(
                GuCardReferenceFactory.Create<CanYue>(this)
            );
        }

        return cards;
    }

    private YueRen CreateYueRen()
    {
        bool upgraded =
            GuRank >= 5 && LastActivationEmpoweredState[this];

        return GuGeneratedCardFactory.Create<YueRen>(
            Owner,
            GuRank,
            upgraded
        );
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            1 => 8,
            2 => 9,
            3 => 10,
            4 => 12,
            5 => 14,
            6 => 16,
            7 => 18,
            8 => 21,
            _ => 24,
        };

        DynamicVars["BonusDamage"].BaseValue = GuRank >= 9
            ? 4
            : 4;
        DynamicVars["ZhaoPoConditionBonus"].BaseValue = 2;
        DynamicVars[typeof(ZhaoPoPower).Name].BaseValue =
            GuRank >= 8 ? 2 : 1;

        if (IsMutable)
        {
            BaseReplayCount = GuRank >= 6 ? 1 : 0;
        }
    }
}
