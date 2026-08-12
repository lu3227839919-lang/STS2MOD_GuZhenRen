using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Patches;

/// <summary>
/// 多人同步热修复：RitsuLib ExtraHand 在发起端选择蛊牌时会把
/// 蛊牌从自定义蛊牌堆临时移入原版 Hand，这个移牌只发生在发起端本地。
/// 客户端执行同一 PlayCardAction 时蛊牌仍位于自定义蛊牌堆，会在原版
/// 出牌动作的牌堆校验处直接空执行，造成主客机状态分叉。
///
/// 本补丁在各端执行 PlayCardAction 之前，把仍位于自定义蛊牌堆的蛊牌
/// 幂等补移到 Hand，让牌堆校验在两端一致通过；同时在原版 checksum
/// 计算期间临时隐藏发起端尚未确认的本地 Hand 变更，避免队友并发动作
/// 把目标选择中的临时状态捕获为永久分歧。
/// </summary>
internal static class GuCardPlaySyncPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuCardPlaySync";

    private static bool _initialized;

    private static int _executingActionCount;

    private static MethodInfo? _toCardModelMethod;

    private const string ChecksumTrackerTypeName =
        "MegaCrit.Sts2.Core.Multiplayer.Game.ChecksumTracker";

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

        MethodInfo obtainAndTrackChecksum =
            ResolveObtainAndTrackChecksumMethod();

        MethodInfo getPlayerSlotIndex =
            AccessTools.DeclaredMethod(
                typeof(RunState),
                nameof(RunState.GetPlayerSlotIndex),
                [typeof(Player)]
            ) ?? throw new MissingMethodException(
                typeof(RunState).FullName,
                $"{nameof(RunState.GetPlayerSlotIndex)}(Player)"
            );

        Harmony harmony = new(HarmonyId);
        try
        {
            harmony.Patch(
                executeAction,
                prefix: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ExecuteActionPrefix)
                ),
                postfix: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ExecuteActionPostfix)
                ),
                finalizer: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ExecuteActionFinalizer)
                )
            );

            harmony.Patch(
                obtainAndTrackChecksum,
                prefix: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ChecksumPrefix)
                ),
                postfix: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ChecksumPostfix)
                ),
                finalizer: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(ChecksumFinalizer)
                )
            );

            harmony.Patch(
                getPlayerSlotIndex,
                postfix: new HarmonyMethod(
                    typeof(GuCardPlaySyncPatch),
                    nameof(GetPlayerSlotIndexPostfix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            Interlocked.Exchange(ref _executingActionCount, 0);
            _initialized = false;
        }
    }

    /// <summary>
    /// RitsuLib ExtraHand 会在本地玩家确认目标前把蛊牌临时移入 Hand。
    /// 如果玩家在上一张牌仍在结算时开始下一次选择，这个仅本地发生的
    /// 提前移牌会污染上一动作结束时的多人校验。UI 入口据此暂时禁止
    /// 新的蛊牌选择；同步动作本身仍可正常排队和执行。
    /// </summary>
    internal static bool IsCardActionExecuting =>
        Volatile.Read(ref _executingActionCount) > 0;

    /// <summary>
    /// SL 或第三方异步出牌钩子可能在选牌界面存续期间重建 Player。
    /// PlayerChoiceSynchronizer 仍持有当前 RunState，但原版
    /// GetPlayerSlotIndex(Player) 只按对象引用查找；旧实例因而返回 -1，
    /// 随后 GetChoiceId 会用 -1 访问选择 ID 数组。
    ///
    /// NetId 才是多人协议中的稳定玩家身份。正常引用匹配时完全保留
    /// 原版结果，仅在引用失效而同 NetId 玩家仍存在时回退到网络身份。
    /// </summary>
    private static void GetPlayerSlotIndexPostfix(
        RunState __instance,
        Player player,
        ref int __result
    )
    {
        if (__result >= 0 || player == null)
        {
            return;
        }

        int networkSlot = __instance.GetPlayerSlotIndex(player.NetId);
        if (networkSlot < 0)
        {
            return;
        }

        __result = networkSlot;
        Entry.Logger.Warn(
            $"[玩家选择同步] Player 引用已失效，已按 NetId " +
            $"{player.NetId} 恢复到槽位 {networkSlot}。"
        );
    }

    private static void ChecksumPrefix(
        out GuActivationModeSystem.PendingChecksumScope? __state
    )
    {
        __state = null;

        try
        {
            __state =
                GuActivationModeSystem.NormalizePendingCardForChecksum();
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[蛊牌同步] checksum 前状态归一化失败：" +
                $"{exception.Message}"
            );
        }
    }

    private static void ChecksumPostfix(
        GuActivationModeSystem.PendingChecksumScope? __state
    )
    {
        RestorePendingCardAfterChecksum(__state);
    }

    private static Exception? ChecksumFinalizer(
        Exception? __exception,
        GuActivationModeSystem.PendingChecksumScope? __state
    )
    {
        // Postfix 与 finalizer 都可能到达这里；scope.Dispose() 幂等。
        RestorePendingCardAfterChecksum(__state);
        return __exception;
    }

    private static void RestorePendingCardAfterChecksum(
        GuActivationModeSystem.PendingChecksumScope? state
    )
    {
        try
        {
            state?.Dispose();
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[蛊牌同步] checksum 后恢复 pending 蛊牌失败：" +
                $"{exception.Message}"
            );
        }
    }

    private static void ExecuteActionPrefix(
        PlayCardAction __instance
    )
    {
        Interlocked.Increment(ref _executingActionCount);

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

    private static void ExecuteActionPostfix(ref Task __result)
    {
        __result = AwaitActionAndReleaseGateAsync(__result);
    }

    private static Exception? ExecuteActionFinalizer(
        Exception? __exception
    )
    {
        // async 异常由包装后的 Task finally 释放；这里只处理原方法在
        // 返回 Task 之前同步抛出的异常，避免 UI 永久被锁住。
        if (__exception != null)
        {
            ReleaseActionGate();
        }

        return __exception;
    }

    private static async Task AwaitActionAndReleaseGateAsync(
        Task actionTask
    )
    {
        try
        {
            await actionTask;
        }
        finally
        {
            ReleaseActionGate();
        }
    }

    private static void ReleaseActionGate()
    {
        int remaining = Interlocked.Decrement(
            ref _executingActionCount
        );
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _executingActionCount, 0);
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

    private static MethodInfo ResolveObtainAndTrackChecksumMethod()
    {
        Type checksumTrackerType =
            AccessTools.TypeByName(ChecksumTrackerTypeName) ??
            throw new TypeLoadException(
                $"Could not resolve {ChecksumTrackerTypeName}."
            );

        MethodInfo? method = checksumTrackerType
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            )
            .SingleOrDefault(candidate =>
            {
                if (candidate.Name != "ObtainAndTrackChecksum")
                {
                    return false;
                }

                ParameterInfo[] parameters =
                    candidate.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType ==
                        typeof(GameAction);
            });

        return method ?? throw new MissingMethodException(
            ChecksumTrackerTypeName,
            "ObtainAndTrackChecksum(string, GameAction)"
        );
    }
}
