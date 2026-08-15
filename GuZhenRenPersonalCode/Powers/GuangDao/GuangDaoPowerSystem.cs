using System.Runtime.CompilerServices;
using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 光道 Power 的唯一公共调用入口。
/// 满足光辉条件时自动支付；一次出牌序列只支付一次，Replay 沿用首段结果。
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
    /// 首段检测光辉并自动支付；Replay 后续段复用首段结果，不重复支付。
    /// </summary>
    public static async Task<bool> TryAutoSpendGuangHui(
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

        if (!cardPlay.IsFirstInSeries)
        {
            return decision.Resolved && decision.Empowered;
        }

        decision.Resolved = true;
        decision.Empowered = false;

        if (!IsGuangDaoCard(sourceCard) || sourceCard.IsCanonical)
        {
            return false;
        }

        // 折光原本在 AfterCardPlayed 才发放光辉，导致本张牌触发
        // 折光后刚好达到阈值时无法立即耀化。自动支付前先提前
        // 结算当前牌的折光，并由 ZheGuangPower 防止出牌后重复发放。
        if (sourceCard.Owner.Creature.GetPower<ZheGuangPower>() is
            { } zheGuang)
        {
            await zheGuang.ResolveBeforeAutoSpend(
                choiceContext,
                cardPlay
            );
        }

        if (sourceCard.Owner.Creature.GetPower<GuangHuiPower>() is not
                { } power ||
            power.Amount < amount)
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

        if (decision.Empowered)
        {
            Entry.Logger.Info(
                $"[光辉自动支付] {sourceCard.Id} 自动消耗 {amount} 点光辉：{before} -> {after}。"
            );
        }
        else
        {
            Entry.Logger.Warn(
                $"[光辉自动支付] {sourceCard.Id} 请求消耗 {amount} 点光辉，但结算后数量为 {before} -> {after}。"
            );
        }

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

}
