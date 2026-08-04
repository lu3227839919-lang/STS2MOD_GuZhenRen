using System.Runtime.CompilerServices;

using GuZhenRen.Cards;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 光道 Power 的唯一公共调用入口。
/// 光辉支付由玩家决定，并且一次出牌序列只支付一次；Replay 沿用首段结果。
/// </summary>
public static class GuangDaoPowerSystem
{
    private sealed class ActivationDecision
    {
        public bool Resolved;
        public bool Empowered;
    }

    private static readonly ConditionalWeakTable<
        CardModel,
        ActivationDecision
    > GuangHuiDecisions = new();

    public static bool IsGuangDaoCard(CardModel? card)
    {
        return card?.Tags.Contains(GuZhenRenTags.GuangDao) == true;
    }

    public static async Task<int> GainGuangHui(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0 ||
            !IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.CombatState == null)
        {
            return 0;
        }

        Creature owner = sourceCard.Owner.Creature;
        int before = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        int room = Math.Max(0, GuangHuiPower.MaximumAmount - before);
        int requested = Math.Min(amount, room);

        if (requested <= 0)
        {
            return 0;
        }

        await PowerCmd.Apply<GuangHuiPower>(
            choiceContext,
            owner,
            requested,
            owner,
            sourceCard
        );

        int after = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        return Math.Max(0, after - before);
    }

    internal static async Task<int> GainGuangHuiFromPower(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int amount
    )
    {
        if (amount <= 0 || owner.CombatState == null)
        {
            return 0;
        }

        int before = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        int room = Math.Max(0, GuangHuiPower.MaximumAmount - before);
        int requested = Math.Min(amount, room);
        if (requested <= 0)
        {
            return 0;
        }

        await PowerCmd.Apply<GuangHuiPower>(
            choiceContext,
            owner,
            requested,
            owner,
            cardSource: null
        );

        int after = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        return Math.Max(0, after - before);
    }

    /// <summary>
    /// 首段询问是否支付光辉；Replay 后续段复用首段选择，不重复支付或弹窗。
    /// </summary>
    public static async Task<bool> TrySpendGuangHui(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        CardPlay cardPlay,
        int amount
    )
    {
        if (amount <= 0)
        {
            return true;
        }

        ActivationDecision decision =
            GuangHuiDecisions.GetValue(
                sourceCard,
                static _ => new ActivationDecision()
            );

        if (cardPlay.PlayIndex > 0)
        {
            return decision.Resolved && decision.Empowered;
        }

        decision.Resolved = true;
        decision.Empowered = false;

        if (!IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.GetPower<GuangHuiPower>() is not
                { } power ||
            power.Amount < amount)
        {
            return false;
        }

        CardModel spend = CreateChoiceCard<SpendGuangHuiChoice>(
            sourceCard.Owner
        );
        CardModel save = CreateChoiceCard<SaveGuangHuiChoice>(
            sourceCard.Owner
        );

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_CARD_SPEND_GUANG_HUI_CHOICE.selectionScreenPrompt"
        );
        prompt.Add("Amount", amount);
        prompt.Add("SourceCard", sourceCard.Title);

        CardSelectorPrefs prefs = new(prompt, 1, 1)
        {
            Cancelable = false,
            PretendCardsCanBePlayed = true,
        };

        CardModel? selected =
            (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    [spend, save],
                    sourceCard.Owner,
                    prefs
                )
            ).FirstOrDefault();

        if (selected is not SpendGuangHuiChoice)
        {
            return false;
        }

        int before = power.Amount;
        await PowerCmd.ModifyAmount(
            choiceContext,
            power,
            -amount,
            sourceCard.Owner.Creature,
            sourceCard
        );

        int after = sourceCard.Owner.Creature
            .GetPower<GuangHuiPower>()?.Amount ?? 0;
        decision.Empowered = before - after == amount;
        return decision.Empowered;
    }

    public static async Task<bool> ApplyZhaoPo(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        if (amount <= 0 ||
            !IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            !target.IsEnemy ||
            !ReferenceEquals(
                sourceCard.Owner.Creature.CombatState,
                target.CombatState
            ))
        {
            return false;
        }

        ZhaoPoPower? applied = await PowerCmd.Apply<ZhaoPoPower>(
            choiceContext,
            target,
            amount,
            sourceCard.Owner.Creature,
            sourceCard
        );

        return applied != null;
    }

    internal static async Task EnsureZheGuang(Player player)
    {
        if (player.Creature.CombatState == null ||
            player.Creature.GetPower<ZheGuangPower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<ZheGuangPower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            1,
            player.Creature,
            cardSource: null,
            silent: true
        );
    }

    private static CardModel CreateChoiceCard<T>(Player owner)
        where T : CardModel
    {
        CardModel card = ModelDb.Card<T>().ToMutable();
        card.Owner = owner;
        return card;
    }
}

public abstract class AbstractGuangHuiChoice
    : ModCardTemplate,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    protected AbstractGuangHuiChoice()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Token,
            target: TargetType.Self,
            showInCardLibrary: false
        )
    {
    }

    public abstract override CardAssetProfile AssetProfile { get; }

    protected override bool IsPlayable => false;

    protected override void OnUpgrade()
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class SpendGuangHuiChoice
    : AbstractGuangHuiChoice
{
    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/YueGuangGu.png"
        );

    public SpendGuangHuiChoice()
        : base()
    {
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class SaveGuangHuiChoice
    : AbstractGuangHuiChoice
{
    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/ChuiDong.png"
        );

    public SaveGuangHuiChoice()
        : base()
    {
    }
}
