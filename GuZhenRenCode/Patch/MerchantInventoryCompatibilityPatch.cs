using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Merchant;

namespace GuZhenRen.Patches;

/// <summary>
/// 允许角色卡池在唯一性过滤后少于商店固定槽位数。
///
/// 原游戏会固定生成 2 张攻击、2 张技能和 1 张能力牌；当模组角色
/// 没有能力牌，或已有的唯一牌被过滤后候选不足时，
/// MerchantCardEntry.Populate 会抛出异常并阻止玩家进入商店。
///
/// 对这种“没有可用候选牌”的预期情况，本补丁保留空库存槽位。
/// 商店 UI 原生支持 IsStocked=false，因此不需要伪造重复牌，
/// 也不会绕过唯一性规则。
/// </summary>
internal static class MerchantInventoryCompatibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".MerchantInventoryCompatibility";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? populate = AccessTools.Method(
            typeof(MerchantCardEntry),
            nameof(MerchantCardEntry.Populate)
        );

        if (populate == null)
        {
            throw new MissingMethodException(
                "商店兼容补丁所需的 MerchantCardEntry.Populate 不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);

        harmony.Patch(
            populate,
            finalizer: new HarmonyMethod(
                typeof(MerchantInventoryCompatibilityPatch),
                nameof(PopulateFinalizer)
            )
        );

        MethodInfo? setOnSale = AccessTools.Method(
            typeof(MerchantCardEntry),
            nameof(MerchantCardEntry.SetOnSale)
        );
        MethodInfo? createInventory = AccessTools.Method(
            typeof(MerchantInventory),
            nameof(MerchantInventory.CreateForNormalMerchant)
        );

        if (setOnSale == null || createInventory == null)
        {
            harmony.UnpatchAll(HarmonyId);
            throw new MissingMethodException(
                "商店兼容补丁所需的促销或库存创建方法不存在。"
            );
        }

        harmony.Patch(
            setOnSale,
            prefix: new HarmonyMethod(
                typeof(MerchantInventoryCompatibilityPatch),
                nameof(SetOnSalePrefix)
            )
        );
        harmony.Patch(
            createInventory,
            postfix: new HarmonyMethod(
                typeof(MerchantInventoryCompatibilityPatch),
                nameof(CreateInventoryPostfix)
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

    private static bool SetOnSalePrefix(
        MerchantCardEntry __instance
    )
    {
        // 随机促销位置可能正好是空槽位；不要对空库存计算价格。
        return __instance.CreationResult != null;
    }

    private static void CreateInventoryPostfix(
        MerchantInventory __result
    )
    {
        if (__result.CharacterCardEntries.Any(entry =>
                entry.IsStocked && entry.IsOnSale
            ))
        {
            return;
        }

        // 原促销槽位为空时，把促销转移到第一个实际有货的角色牌槽位。
        __result.CharacterCardEntries
            .FirstOrDefault(entry => entry.IsStocked)
            ?.SetOnSale();
    }

    private static Exception? PopulateFinalizer(
        MerchantCardEntry __instance,
        Exception? __exception
    )
    {
        if (__exception == null)
        {
            return null;
        }

        // 若牌已经成功生成，异常来自后续价格或 Hook，不应吞掉。
        if (__instance.CreationResult != null)
        {
            return __exception;
        }

        if (!IsMissingCandidateFailure(__exception))
        {
            return __exception;
        }

        Entry.Logger.Warn(
            "商店卡牌槽位没有符合当前类型、稀有度与唯一性规则的候选牌，" +
            "已将该槽位安全留空。" +
            $" 原始错误：{__exception.Message}"
        );

        // MerchantCardEntry.IsStocked 会返回 false；原生商店 UI 会忽略空槽位。
        return null;
    }

    private static bool IsMissingCandidateFailure(
        Exception exception
    )
    {
        if (exception is InvalidOperationException &&
            exception.Message.StartsWith(
                "Can't generate valid rarity for merchant card type ",
                StringComparison.Ordinal
            ))
        {
            return true;
        }

        // 指定稀有度的重载在候选为空时可能把 null 传给 CreateCard，
        // 不同游戏构建可能表现为 ArgumentNullException 或 NullReferenceException。
        // 只有堆栈明确来自 CardFactory.CreateForMerchant 时才视为缺候选。
        if (exception is not ArgumentNullException &&
            exception is not NullReferenceException)
        {
            return false;
        }

        return exception.StackTrace?.Contains(
            "MegaCrit.Sts2.Core.Factories.CardFactory.CreateForMerchant",
            StringComparison.Ordinal
        ) == true;
    }
}
