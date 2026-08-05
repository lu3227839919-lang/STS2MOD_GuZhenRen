using System.Reflection;
using System.Runtime.CompilerServices;

using GuZhenRen.Cards;
using GuZhenRen.Characters;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;

using STS2RitsuLib;

namespace GuZhenRen.Patches;

/// <summary>
/// 为方源补齐 DARV / Dusty Tome 的自定义角色兼容。
///
/// BaseLib 的 ITomeCard 扩展没有方源映射时，Dusty Tome 的 AncientCard
/// 会保持为空，导致事件生成或领取遗物时崩溃。本补丁不再将 Dusty Tome
/// 绑定到某张杀招，而是从合法蛊虫池中确定性随机一张蛊虫，沿用原版
/// Dusty Tome 的普通升级流程，并在加入牌组后将其初始化为六转。
/// </summary>
internal static class AncientCompatibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".AncientCompatibility";

    private const string DustyTomeRngStreamId =
        "ancient/darv/random_rank6_gu";

    private const int DustyTomeGuRank = 6;

    private sealed class DustyTomeSelectionState
    {
        public required CardModel CanonicalCard { get; init; }
    }

    private readonly record struct DustyTomeGrantState(
        CardModel CanonicalCard,
        HashSet<CardModel> ExistingCards
    );

    private static readonly ConditionalWeakTable<
        DustyTome,
        DustyTomeSelectionState
    > SelectionStates = new();

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? setupForPlayer =
            AccessTools.GetDeclaredMethods(typeof(DustyTome))
                .FirstOrDefault(static method =>
                {
                    if (!string.Equals(
                            method.Name,
                            "SetupForPlayer",
                            StringComparison.Ordinal
                        ))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType == typeof(Player);
                });

        MethodInfo? afterObtained = AccessTools.Method(
            typeof(DustyTome),
            nameof(DustyTome.AfterObtained),
            Type.EmptyTypes
        );

        Harmony harmony = new(HarmonyId);

        if (setupForPlayer != null)
        {
            harmony.Patch(
                setupForPlayer,
                prefix: new HarmonyMethod(
                    typeof(AncientCompatibilityPatch),
                    nameof(DustyTomeSetupPrefix)
                )
                {
                    priority = Priority.First,
                },
                finalizer: new HarmonyMethod(
                    typeof(AncientCompatibilityPatch),
                    nameof(DustyTomeSetupFinalizer)
                )
                {
                    priority = Priority.Last,
                }
            );
        }
        else
        {
            Entry.Logger.Warn(
                "[先古遗民兼容] 未找到 DustyTome.SetupForPlayer(Player)。"
            );
        }

        if (afterObtained != null)
        {
            harmony.Patch(
                afterObtained,
                prefix: new HarmonyMethod(
                    typeof(AncientCompatibilityPatch),
                    nameof(DustyTomeAfterObtainedPrefix)
                )
                {
                    priority = Priority.Last,
                },
                postfix: new HarmonyMethod(
                    typeof(AncientCompatibilityPatch),
                    nameof(DustyTomeAfterObtainedPostfix)
                )
                {
                    priority = Priority.Last,
                }
            );
        }
        else
        {
            Entry.Logger.Warn(
                "[先古遗民兼容] 未找到 DustyTome.AfterObtained()。"
            );
        }

        _initialized = true;
        Entry.Logger.Info(
            "[先古遗民兼容] DARV 的 Dusty Tome 已改为随机给予一张六转蛊虫。"
        );
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

    /// <summary>
    /// 方源不走原版角色到先古牌的固定映射，而是在事件选项生成时
    /// 选择一张随机蛊虫并写入 AncientCard。
    /// </summary>
    private static bool DustyTomeSetupPrefix(
        DustyTome __instance,
        Player? __0
    )
    {
        if (!IsGuZhenRenPlayer(__0))
        {
            return true;
        }

        return !TryAssignRandomGuCard(
            __instance,
            __0!,
            "SetupForPlayer"
        );
    }

    /// <summary>
    /// 兼容其他前置补丁先执行的情况。只在方源分支、且已经成功补齐
    /// AncientCard 时恢复空引用；其他异常继续交给原流程处理。
    /// </summary>
    private static Exception? DustyTomeSetupFinalizer(
        DustyTome __instance,
        Player? __0,
        Exception? __exception
    )
    {
        if (__exception == null)
        {
            return null;
        }

        if (!IsGuZhenRenPlayer(__0) ||
            __exception is not NullReferenceException)
        {
            return __exception;
        }

        if (!TryAssignRandomGuCard(
                __instance,
                __0!,
                "SetupForPlayer finalizer"
            ))
        {
            return __exception;
        }

        Entry.Logger.Warn(
            "[先古遗民兼容] DustyTome.SetupForPlayer 的第三方扩展" +
            "触发空引用，但随机六转蛊虫候选已成功补齐，事件可以继续。" +
            $" 原错误：{__exception.Message}"
        );
        return null;
    }

    /// <summary>
    /// 领取前再次写入同一随机候选，防止事件恢复、QuickSL 或其他 Mod
    /// 在选项生成后清空 AncientCard；同时记录已有同名实例，供后置补丁
    /// 精确定位本次新加入的牌。
    /// </summary>
    private static void DustyTomeAfterObtainedPrefix(
        DustyTome __instance,
        out DustyTomeGrantState? __state
    )
    {
        __state = null;

        Player? owner = __instance.Owner;
        if (!IsGuZhenRenPlayer(owner) ||
            !TryGetOrCreateSelection(
                __instance,
                owner!,
                out CardModel? canonical
            ))
        {
            return;
        }

        __instance.AncientCard = canonical.Id;

        HashSet<CardModel> existingCards = owner!
            .Deck
            .Cards
            .Where(card => card.Id == canonical.Id)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        __state = new DustyTomeGrantState(
            canonical,
            existingCards
        );

        Entry.Logger.Info(
            "[先古遗民兼容] AfterObtained：Dusty Tome 的随机蛊虫" +
            $"候选为 {canonical.Id}。"
        );
    }

    /// <summary>
    /// Dusty Tome 原版流程负责把选中的牌加入牌组并进行普通升级；
    /// 此处只把本次新加入的蛊虫固定为六转，并登记仙蛊唯一性状态。
    /// </summary>
    private static void DustyTomeAfterObtainedPostfix(
        DustyTome __instance,
        DustyTomeGrantState? __state
    )
    {
        Player? owner = __instance.Owner;
        if (!IsGuZhenRenPlayer(owner) ||
            __state is not { } state)
        {
            return;
        }

        AbstractGuZhenRenCard? grantedCard = owner!
            .Deck
            .Cards
            .OfType<AbstractGuZhenRenCard>()
            .LastOrDefault(card =>
                card is IGuWormCard &&
                card.Id == state.CanonicalCard.Id &&
                !state.ExistingCards.Contains(card)
            );

        SelectionStates.Remove(__instance);

        if (grantedCard == null)
        {
            Entry.Logger.Warn(
                "[先古遗民兼容] Dusty Tome 已领取，但未在牌组中找到" +
                $"新加入的随机蛊虫 {state.CanonicalCard.Id}。" +
                "牌组容量或仙蛊唯一性规则可能拒绝了本次加入。"
            );
            return;
        }

        grantedCard.InitializeGuRankFromSource(DustyTomeGuRank);
        GuZhenRenCardRules.RegisterXianGuClaim(
            grantedCard,
            owner.RunState.TotalFloor
        );

        Entry.Logger.Info(
            "[先古遗民兼容] Dusty Tome 已给予升级后的六转蛊虫：" +
            $"{grantedCard.Id}。"
        );
    }

    private static bool TryAssignRandomGuCard(
        DustyTome tome,
        Player player,
        string stage
    )
    {
        if (!TryGetOrCreateSelection(
                tome,
                player,
                out CardModel? canonical
            ))
        {
            return false;
        }

        try
        {
            tome.AncientCard = canonical.Id;
            Entry.Logger.Info(
                $"[先古遗民兼容] {stage}：Dusty Tome 随机候选" +
                $"已设置为 {canonical.Id}，领取后初始化为六转。"
            );
            return true;
        }
        catch (Exception exception)
        {
            Entry.Logger.Error(
                $"[先古遗民兼容] {stage}：设置 Dusty Tome 随机蛊虫失败：" +
                exception
            );
            return false;
        }
    }

    private static bool TryGetOrCreateSelection(
        DustyTome tome,
        Player player,
        out CardModel? canonical
    )
    {
        if (SelectionStates.TryGetValue(
                tome,
                out DustyTomeSelectionState? existing
            ))
        {
            canonical = existing.CanonicalCard;
            return true;
        }

        AbstractGuZhenRenCard[] allCandidates = ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .AllCards
            .OfType<AbstractGuZhenRenCard>()
            .Where(card =>
                card is IGuWormCard &&
                card.MaxGuRank >= DustyTomeGuRank &&
                GuZhenRenCardRules.CanAppearInCardReward(
                    player,
                    card
                )
            )
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .OrderBy(card => card.Id.ToString(), StringComparer.Ordinal)
            .ToArray();

        if (allCandidates.Length == 0)
        {
            canonical = null;
            Entry.Logger.Error(
                "[先古遗民兼容] 蛊虫卡池中没有可作为 Dusty Tome 奖励的六转候选。"
            );
            return false;
        }

        AbstractGuZhenRenCard[] uniqueCandidates = allCandidates
            .Where(card =>
                !GuZhenRenCardRules.HasSameXianGu(
                    player.RunState,
                    card
                )
            )
            .ToArray();

        AbstractGuZhenRenCard[] candidates =
            uniqueCandidates.Length > 0
                ? uniqueCandidates
                : allCandidates;

        Rng rng = RitsuLibFramework.GetModPlayerRng(
            player,
            Entry.ModId,
            DustyTomeRngStreamId
        );

        canonical = rng.NextItem(candidates);
        SelectionStates.Add(
            tome,
            new DustyTomeSelectionState
            {
                CanonicalCard = canonical,
            }
        );
        return true;
    }

    private static bool IsGuZhenRenPlayer(Player? player)
    {
        return player?.Character is GuZhenRenCharacter;
    }
}
