using System.Reflection;

using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace GuZhenRen.Patches;

/// <summary>
/// 将蛊真人自定义关键词的悬浮说明限制在牌堆查看界面。
///
/// 关键词本身始终保留在 CardModel 上，因此规则判断、存档和多人同步
/// 不受影响；这里只在本地 UI 创建悬浮提示时隐藏模组自有关键词。
/// 游戏本体的消耗、保留、虚无等提示不会被过滤。
/// </summary>
internal static class GuKeywordHoverVisibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuKeywordHoverVisibility";

    private static readonly string[] PileInspectionMarkers =
    [
        "CardPileScreen",
        "NCardPileScreen",
        "MasterDeckScreen",
        "DeckScreen",
        "DeckViewScreen",
    ];

    [ThreadStatic]
    private static int _cardHoverScopeDepth;

    [ThreadStatic]
    private static bool _showOwnedKeywordsInCurrentScope;

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? keywordMethod =
            AccessTools.Method(
                typeof(CardModel),
                nameof(CardModel.GetKeywordsWithSources),
                [typeof(KeywordSources)]
            );
        MethodInfo? hoverTipSetInit =
            AccessTools.DeclaredMethod(
                typeof(NHoverTipSet),
                "Init",
                [
                    typeof(Control),
                    typeof(IEnumerable<IHoverTip>),
                ]
            );

        if (keywordMethod == null || hoverTipSetInit == null)
        {
            Entry.Logger.Warn(
                "[关键词视野] 当前游戏缺少卡牌关键词或悬浮提示接口；" +
                "跳过 UI 过滤，但不阻止模组初始化。"
            );
            _initialized = true;
            return;
        }

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                keywordMethod,
                postfix: new HarmonyMethod(
                    typeof(GuKeywordHoverVisibilityPatch),
                    nameof(KeywordsPostfix)
                )
                {
                    priority = Priority.Last,
                    after = [Entry.ModId + ".CardUniqueness"],
                }
            );

            harmony.Patch(
                hoverTipSetInit,
                prefix: new HarmonyMethod(
                    typeof(GuKeywordHoverVisibilityPatch),
                    nameof(HoverTipSetInitPrefix)
                )
                {
                    priority = Priority.First,
                }
            );

            IReadOnlyList<MethodInfo> providers =
                FindCardHoverProviderMethods();

            HarmonyMethod scopePrefix = new(
                typeof(GuKeywordHoverVisibilityPatch),
                nameof(CardHoverProviderPrefix)
            );
            HarmonyMethod scopeFinalizer = new(
                typeof(GuKeywordHoverVisibilityPatch),
                nameof(CardHoverProviderFinalizer)
            );

            int patchedProviderCount = 0;
            foreach (MethodInfo provider in providers)
            {
                try
                {
                    harmony.Patch(
                        provider,
                        prefix: scopePrefix,
                        finalizer: scopeFinalizer
                    );
                    patchedProviderCount++;
                }
                catch (Exception exception)
                {
                    Entry.Logger.Warn(
                        "[关键词视野] 跳过无法挂载的悬浮入口 " +
                        $"{provider.DeclaringType?.FullName}.{provider.Name}：" +
                        exception.Message
                    );
                }
            }

            if (patchedProviderCount == 0)
            {
                Entry.Logger.Warn(
                    "[关键词视野] 未发现 NCard 的悬浮提示提供方法；" +
                    "将仅使用 NHoverTipSet.Init 的后备过滤。"
                );
            }
            else
            {
                Entry.Logger.Info(
                    $"[关键词视野] 已挂载 {patchedProviderCount} 个卡牌悬浮上下文入口；" +
                    "自定义关键词仅在牌堆查看界面显示。"
                );
            }

            _initialized = true;
        }
        catch (Exception exception)
        {
            harmony.UnpatchAll(HarmonyId);
            ResetScope();
            Entry.Logger.Warn(
                "[关键词视野] UI 过滤初始化失败，已安全跳过；" +
                "不会阻止模组加载。" + exception
            );
            _initialized = true;
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
            ResetScope();
            _initialized = false;
        }
    }

    private static IReadOnlyList<MethodInfo>
        FindCardHoverProviderMethods()
    {
        return typeof(NCard)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            )
            .Where(method =>
                !method.IsAbstract &&
                !method.ContainsGenericParameters &&
                HasManagedBody(method) &&
                !typeof(Task).IsAssignableFrom(method.ReturnType) &&
                LooksLikeHoverProvider(method)
            )
            .GroupBy(method =>
                (method.Module, method.MetadataToken)
            )
            .Select(group => group.First())
            .ToArray();
    }

    private static bool HasManagedBody(MethodInfo method)
    {
        try
        {
            return method.GetMethodBody() != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeHoverProvider(MethodInfo method)
    {
        if (method.Name.Contains(
                "HoverTip",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return true;
        }

        Type returnType = method.ReturnType;
        if (typeof(IEnumerable<IHoverTip>)
            .IsAssignableFrom(returnType))
        {
            return true;
        }

        return returnType
            .GetInterfaces()
            .Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() ==
                typeof(IEnumerable<>) &&
                typeof(IHoverTip).IsAssignableFrom(
                    @interface.GetGenericArguments()[0]
                )
            );
    }

    private static void CardHoverProviderPrefix(
        object __instance,
        out HoverScopeState __state
    )
    {
        __state = new HoverScopeState(
            _cardHoverScopeDepth,
            _showOwnedKeywordsInCurrentScope
        );

        bool isPileInspection =
            __instance is Node node &&
            IsPileInspection(node);

        _showOwnedKeywordsInCurrentScope =
            _cardHoverScopeDepth > 0
                ? _showOwnedKeywordsInCurrentScope ||
                  isPileInspection
                : isPileInspection;
        _cardHoverScopeDepth++;
    }

    private static Exception? CardHoverProviderFinalizer(
        Exception? __exception,
        HoverScopeState __state
    )
    {
        _cardHoverScopeDepth = __state.Depth;
        _showOwnedKeywordsInCurrentScope =
            __state.ShowOwnedKeywords;
        return __exception;
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(Entry.ModId + ".CardUniqueness")]
    private static void KeywordsPostfix(
        KeywordSources sources,
        ref IReadOnlySet<CardKeyword> __result
    )
    {
        if (_cardHoverScopeDepth <= 0 ||
            _showOwnedKeywordsInCurrentScope ||
            !sources.HasFlag(KeywordSources.Local))
        {
            return;
        }

        HashSet<CardKeyword> filtered =
            __result
                .Where(keyword =>
                    !GuZhenRenKeywords.OwnedKeywords.Contains(keyword)
                )
                .ToHashSet();

        if (filtered.Count != __result.Count)
        {
            __result = filtered;
        }
    }

    /// <summary>
    /// 后备过滤：即使某个游戏版本绕过 NCard 的常规悬浮入口，
    /// 仍会在 NHoverTipSet.Init 时移除能反射识别出的本模组关键词提示。
    /// </summary>
    private static void HoverTipSetInitPrefix(
        Control owner,
        ref IEnumerable<IHoverTip> hoverTips
    )
    {
        if (IsPileInspection(owner))
        {
            return;
        }

        try
        {
            IHoverTip[] source = hoverTips?.ToArray() ?? [];
            IHoverTip[] filtered = source
                .Where(tip => !LooksLikeOwnedKeywordTip(tip))
                .ToArray();

            if (filtered.Length != source.Length)
            {
                hoverTips = filtered;
            }
        }
        catch
        {
            // 悬浮提示属于纯本地 UI；过滤失败时保留原提示，绝不影响游戏流程。
        }
    }

    private static bool LooksLikeOwnedKeywordTip(IHoverTip tip)
    {
        return ContainsOwnedKeywordMarker(
            tip,
            depth: 0,
            new HashSet<object>(
                ReferenceEqualityComparer.Instance
            )
        );
    }

    private static bool ContainsOwnedKeywordMarker(
        object? value,
        int depth,
        HashSet<object> visited
    )
    {
        if (value == null || depth > 2)
        {
            return false;
        }

        if (value is CardKeyword keyword)
        {
            return GuZhenRenKeywords.OwnedKeywords.Contains(keyword);
        }

        if (value is string text)
        {
            return text.Contains(
                "GU_ZHEN_REN_KEYWORD_",
                StringComparison.OrdinalIgnoreCase
            );
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum ||
            type == typeof(decimal))
        {
            return false;
        }

        if (!type.IsValueType && !visited.Add(value))
        {
            return false;
        }

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            object? fieldValue;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (ContainsOwnedKeywordMarker(
                    fieldValue,
                    depth + 1,
                    visited
                ))
            {
                return true;
            }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetMethod == null)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (ContainsOwnedKeywordMarker(
                    propertyValue,
                    depth + 1,
                    visited
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPileInspection(Node? node)
    {
        Node? current = node;

        for (int depth = 0;
             current != null && depth < 24;
             depth++, current = current.GetParent())
        {
            string typeName =
                current.GetType().FullName ??
                current.GetType().Name;
            string nodeName = current.Name.ToString();

            if (PileInspectionMarkers.Any(marker =>
                    typeName.Contains(
                        marker,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    nodeName.Contains(
                        marker,
                        StringComparison.OrdinalIgnoreCase
                    )
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetScope()
    {
        _cardHoverScopeDepth = 0;
        _showOwnedKeywordsInCurrentScope = false;
    }

    private readonly record struct HoverScopeState(
        int Depth,
        bool ShowOwnedKeywords
    );
}
