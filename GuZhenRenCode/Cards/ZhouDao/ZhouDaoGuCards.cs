using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoGuCard : AbstractGuWormCard
{
    protected AbstractZhouDaoGuCard(
        CardRarity rarity,
        TargetType target = TargetType.Self
    ) : base(0, CardType.Skill, rarity, target)
    {
        SetDao(Dao.ZhouDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }
}

public abstract class AbstractZhouDaoCompanionGuCard :
    AbstractZhouDaoGuCard,
    IZhouDaoCompanionGuCard
{
    public abstract Type CompanionCardType { get; }

    protected AbstractZhouDaoCompanionGuCard(CardRarity rarity)
        : base(rarity)
    {
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        ZhouDaoCompanionSystem.SyncForGu(this);
    }

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy
    ) => ReferenceEquals(card, this) &&
         Pile?.Type == PileType.Deck &&
         oldPileType != PileType.Deck
            ? ZhouDaoCompanionSystem.EnsureForGuAsync(this)
            : Task.CompletedTask;

    public override Task BeforeCardRemoved(CardModel card) =>
        ReferenceEquals(card, this)
            ? ZhouDaoCompanionSystem.BeforeGuRemovedAsync(this)
            : Task.CompletedTask;
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class GuangYinRenRanGu : AbstractZhouDaoCompanionGuCard
{
    public override Type CompanionCardType => typeof(GuangYinRenRan);

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public GuangYinRenRanGu() : base(CardRarity.Uncommon)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        bool recovered = ZhouDaoPowerSystem.HasGuRecoveredThisTurn(Owner);
        int gain = GuRank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            <= 8 => 3,
            _ => 4,
        };
        if (recovered && GuRank is 2 or 4 or 5)
        {
            gain++;
        }

        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if (GuRank >= 8 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
            if (GuRank >= 9)
            {
                await CardPileCmd.Draw(choiceContext, 1, Owner);
            }
        }
        else if (GuRank == 7 && recovered)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class NianGu : AbstractZhouDaoCompanionGuCard
{
    public override Type CompanionCardType => typeof(NianNianSuiSui);

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public NianGu() : base(CardRarity.Common)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int current = ZhouDaoPowerSystem.GetNianHua(Owner);
        int gain = GuRank switch
        {
            <= 2 => 1,
            <= 4 => 2,
            5 => current <= 3 ? 3 : 2,
            <= 7 => 3,
            _ => 4,
        };

        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );
        if (GuRank >= 9 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
        }
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class RiGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        <= 7 => 2,
        _ => 3,
    };

    public RiGu() : base(CardRarity.Common)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int current = ZhouDaoPowerSystem.GetNianHua(Owner);
        int gain = GuRank switch
        {
            3 => 2,
            4 => current <= 2 ? 3 : 2,
            5 or 6 => 3,
            7 or 8 => 4,
            _ => 5,
        };
        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if (GuRank == 6 && result.SuiManCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
        if (GuRank >= 8 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
        }
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class YueGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public YueGu() : base(CardRarity.Common)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        (int now, int next, int following) = GuRank switch
        {
            3 => (1, 1, 0),
            4 => (1, 2, 0),
            5 => (2, 2, 0),
            6 => (2, 3, 0),
            7 => (2, 2, 2),
            8 => (2, 3, 3),
            _ => (3, 3, 3),
        };

        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            now
        );
        await YueGuDelayPower.ScheduleAsync(
            choiceContext,
            Owner,
            next,
            following,
            this
        );
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class HuanBuGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public HuanBuGu()
        : base(CardRarity.Uncommon, TargetType.AnyEnemy)
    {
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

        Creature target = cardPlay.Target!;
        bool alreadySlowed = target.GetPower<HuanBuPower>() != null;
        (int reduction, int duration) = GuRank switch
        {
            3 => (4, 2),
            4 => (5, 2),
            5 => (6, 2),
            6 => (7, 2),
            7 => (8, 2),
            8 => (10, 2),
            _ => (12, 3),
        };

        await ApplyHuanBu(choiceContext, target, reduction, duration);
        if (alreadySlowed && GuRank >= 5)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                GuRank >= 7 ? 2 : 1
            );
        }

        if (GuRank >= 8)
        {
            int splashReduction = GuRank >= 9 ? 6 : 4;
            int splashDuration = GuRank >= 9 ? 2 : 1;
            foreach (Creature enemy in CombatState.HittableEnemies
                         .Where(enemy => !ReferenceEquals(enemy, target)))
            {
                await ApplyHuanBu(
                    choiceContext,
                    enemy,
                    splashReduction,
                    splashDuration
                );
            }
        }
    }

    private async Task ApplyHuanBu(
        PlayerChoiceContext choiceContext,
        Creature target,
        int reduction,
        int duration
    )
    {
        HuanBuPower? power = await PowerCmd.Apply<HuanBuPower>(
            choiceContext,
            target,
            duration,
            Owner.Creature,
            this
        );
        power?.SetReduction(reduction);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class SanGengGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int RecoveryDelayTurns => GuRank <= 6 ? 3 : 4;

    public SanGengGu() : base(CardRarity.Rare)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        (int energy, int draw, int years, int bonusEvents, int hpLoss) =
            GuRank switch
            {
                5 => (1, 1, 1, 2, 4),
                6 => (1, 1, 2, 2, 5),
                7 => (1, 2, 2, 3, 6),
                8 => (2, 2, 2, 3, 7),
                _ => (2, 2, 3, 3, 8),
            };

        await PlayerCmd.GainEnergy(energy, Owner);
        await CardPileCmd.Draw(choiceContext, draw, Owner);
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            Owner,
            years,
            this,
            allowSanGengBonus: false
        );
        await PowerCmd.Apply<SanGengPower>(
            choiceContext,
            Owner.Creature,
            bonusEvents,
            Owner.Creature,
            this
        );
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            this,
            cardPlay
        );
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class HuiSuGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int RecoveryDelayTurns => GuRank <= 7 ? 3 : 4;

    public HuiSuGu() : base(CardRarity.Rare)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int inspect = GuRank switch
        {
            5 => 2,
            6 or 7 => 3,
            8 => 4,
            _ => 5,
        };

        if (CombatState == null)
        {
            return;
        }

        CardModel[] candidates = CombatManager.Instance.History
            .CardPlaysFinished
            .OfType<CardPlayFinishedEntry>()
            .Where(entry =>
                entry.HappenedThisTurn(CombatState) &&
                entry.CardPlay.Player == Owner)
            .Select(entry => entry.CardPlay.Card)
            .Where(IsLegalBacktrackTarget)
            .Reverse()
            .DistinctBy(card => card.Id)
            .Take(inspect)
            .ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        CardModel source;
        if (candidates.Length == 1)
        {
            source = candidates[0];
        }
        else
        {
            IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1)
                {
                    Cancelable = false,
                    PretendCardsCanBePlayed = true,
                }
            );
            source = selected.FirstOrDefault() ?? candidates[0];
        }

        CardModel copy = CombatState.CreateCard(
            source.CanonicalInstance,
            Owner
        );
        for (int index = 0; index < source.CurrentUpgradeLevel; index++)
        {
            CardCmd.Upgrade(copy);
        }

        int delta = GuRank switch
        {
            <= 6 => 1,
            <= 8 => 0,
            _ => -1,
        };
        int baseCost = source.EnergyCost.GetWithModifiers(CostModifiers.None);
        copy.EnergyCost.SetCustomBaseCost(Math.Max(0, baseCost + delta));
        copy.AddKeyword(CardKeyword.Exhaust);
        copy.AddKeyword(GuZhenRenKeywords.XiYing);
        ZhouDaoCardState.MarkXiYing(copy, GuRank >= 9 ? 2 : 1);

        if (Owner.Creature.GetPower<XiYingWatcherPower>() == null)
        {
            await PowerCmd.Apply<XiYingWatcherPower>(
                choiceContext,
                Owner.Creature,
                1,
                Owner.Creature,
                this,
                silent: true
            );
        }

        await GuGeneratedCardFactory.AddToHandOrDiscard(copy, Owner);
    }

    private static bool IsLegalBacktrackTarget(CardModel card) =>
        card.DeckVersion != null &&
        card is not IGuWormCard &&
        !ZhouDaoCardState.IsXiYing(card) &&
        card.Type is CardType.Attack or CardType.Skill &&
        !card.EnergyCost.CostsX;
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class ZhouMaoXianGu : AbstractZhouDaoCompanionGuCard
{
    public override int MinimumAvailableGuRank => 6;
    public override Type CompanionCardType => typeof(ZhouMao);

    public override int RecoveryDelayTurns => GuRank == 6 ? 3 : 4;

    public ZhouMaoXianGu() : base(CardRarity.Rare)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        bool alreadySuiMan = ZhouDaoPowerSystem.HasSuiManThisTurn(Owner);
        int gain = GuRank >= 8 ? 3 : 2;
        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if ((GuRank == 7 && alreadySuiMan) ||
            (GuRank >= 9 && result.SuiManCount > 0))
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class SiShuiLiuNianXianGu : AbstractZhouDaoCompanionGuCard
{
    public override int MinimumAvailableGuRank => 8;
    public override Type CompanionCardType => typeof(SiShuiLiuNian);
    public override int RecoveryDelayTurns => 4;

    public SiShuiLiuNianXianGu() : base(CardRarity.Rare)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int gain = GuRank >= 9 ? 5 : 4;
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        AbstractGuZhenRenCard token = GuRank >= 9
            ? GuGeneratedCardFactory.Create<NianLiuPlus>(Owner, 9)
            : GuGeneratedCardFactory.Create<NianLiu>(Owner, 8);
        await GuGeneratedCardFactory.AddToHandOrDiscard(token, Owner);
    }
}
