using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 多人同步热修复：RitsuLib ExtraHand 在发起端（房主）选择蛊牌时会把
/// 蛊牌从自定义蛊牌堆临时移入原版 Hand，这个移牌只发生在发起端本地。
/// 客户端执行同一 PlayCardAction 时蛊牌仍位于自定义蛊牌堆，会在原版
/// 出牌动作的牌堆校验处直接空执行，造成主客机状态分叉。
///
/// 本补丁在各端执行 PlayCardAction 之前，把仍位于自定义蛊牌堆的蛊牌
/// 幂等补移到 Hand，让牌堆校验在两端一致通过。
/// </summary>
internal static class GuCardPlaySyncPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuCardPlaySync";

    private static bool _initialized;

    private static MethodInfo? _toCardModelMethod;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo executeAction =
            AccessTools.DeclaredMethod(
                typeof(PlayCardAction),
                "ExecuteAction"
            ) ?? throw new MissingMethodException(
                typeof(PlayCardAction).FullName,
                "ExecuteAction"
            );

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            executeAction,
            prefix: new HarmonyMethod(
                typeof(GuCardPlaySyncPatch),
                nameof(ExecuteActionPrefix)
            )
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void ExecuteActionPrefix(
        PlayCardAction __instance
    )
    {
        try
        {
            CardModel? card = ResolveCard(__instance);
            if (card is not IGuWormCard ||
                card.Pile?.Type != GuCardPileSystem.PileType)
            {
                return;
            }

            CardPile hand = PileType.Hand.GetPile(card.Owner);
            GuCardPileSystem.MoveCardToPile(card, hand);

            Entry.Logger.Info(
                $"[蛊牌催动] 出牌动作执行前已将 {card.Id} 从" +
                $"蛊牌堆补移到 Hand（跨端同步）。"
            );
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[蛊牌催动] 出牌动作前补移蛊牌失败：" +
                $"{exception.Message}"
            );
        }
    }

    private static CardModel? ResolveCard(
        PlayCardAction action
    )
    {
        // 优先读取执行时已解析的战斗卡牌实例。
        CardModel? cached = Traverse
            .Create(action)
            .Field("_card")
            .GetValue<CardModel>();
        if (cached != null)
        {
            return cached;
        }

        // 回退：与动作状态机相同的 NetCombatCard → ToCardModel 解析。
        object? netCard = Traverse
            .Create(action)
            .Property("NetCombatCard")
            .GetValue();
        if (netCard == null)
        {
            return null;
        }

        MethodInfo? toCardModel = _toCardModelMethod;
        if (toCardModel == null ||
            toCardModel.DeclaringType != netCard.GetType())
        {
            toCardModel = netCard.GetType().GetMethod(
                "ToCardModel",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );
            _toCardModelMethod = toCardModel;
        }

        return toCardModel?.Invoke(
            netCard,
            null
        ) as CardModel;
    }
}
