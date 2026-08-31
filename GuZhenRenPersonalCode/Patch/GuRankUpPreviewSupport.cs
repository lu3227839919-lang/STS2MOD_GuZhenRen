using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 为篝火升炼提供原生升级预览范围，并把升转后新增或变化的卡面内容
/// 标成绿色。范围外不会改变原生升级效果对蛊虫的限制。
/// </summary>
internal static class GuRankUpPreviewSupport
{
    private static readonly object ScopeLock = new();

    private static ConditionalWeakTable<
        CardModel,
        PreviewState
    > _previewStates = new();

    private static PreviewContext? _activeContext;

    internal static bool IsActive
    {
        get
        {
            lock (ScopeLock)
            {
                return _activeContext != null;
            }
        }
    }

    private sealed class PreviewContext(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    )
    {
        internal int RemainingSlots { get; } = remainingSlots;

        internal HashSet<CardModel> ExcludedCards { get; } =
            new(excludedCards);
    }

    private sealed record PreviewState(string BeforeDescription);

    private readonly record struct DescriptionToken(
        int Start,
        int Length,
        string Text,
        bool IsWhitespace
    );

    internal static void PatchUpgradeDescription(Harmony harmony)
    {
        MethodInfo? method = AccessTools.DeclaredMethod(
            typeof(CardModel),
            nameof(CardModel.GetDescriptionForUpgradePreview)
        );

        if (method == null)
        {
            throw new MissingMethodException(
                "升炼绿色差异预览所需的卡牌描述方法不存在。"
            );
        }

        harmony.Patch(
            method,
            postfix: new HarmonyMethod(
                typeof(GuRankUpPreviewSupport),
                nameof(GetUpgradeDescriptionPostfix)
            )
        );
    }

    internal static IDisposable Begin(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    )
    {
        if (remainingSlots is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingSlots)
            );
        }

        PreviewContext current = new(
            remainingSlots,
            excludedCards
        );
        PreviewContext? previous;

        lock (ScopeLock)
        {
            previous = _activeContext;
            _activeContext = current;
        }

        return new PreviewScope(current, previous);
    }

    internal static bool TryGetIsUpgradable(
        CardModel card,
        out bool result
    )
    {
        PreviewContext? context = GetActiveContext();
        if (context == null)
        {
            result = false;
            return false;
        }

        result = card is AbstractGuWormCard gu &&
            IsEligible(gu, context, checkExcluded: true);
        return true;
    }

    internal static bool TryIncreaseForPreview(
        AbstractGuWormCard gu
    )
    {
        PreviewContext? context = GetActiveContext();
        if (context == null ||
            !IsEligible(gu, context, checkExcluded: false))
        {
            return false;
        }

        string beforeDescription = gu.GetDescriptionForPile(
            PileType.None
        );
        Dictionary<string, decimal> beforeValues =
            gu.DynamicVars.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.BaseValue,
                StringComparer.Ordinal
            );

        if (!gu.TryIncreaseGuRank())
        {
            return false;
        }

        MarkChangedDynamicVars(gu, beforeValues);

        _previewStates.Remove(gu);
        _previewStates.Add(
            gu,
            new PreviewState(beforeDescription)
        );
        return true;
    }

    internal static void Reset()
    {
        lock (ScopeLock)
        {
            _activeContext = null;
        }

        _previewStates = new();
    }

    private static void GetUpgradeDescriptionPostfix(
        CardModel __instance,
        ref string __result
    )
    {
        if (__instance is not AbstractGuWormCard ||
            !_previewStates.TryGetValue(
                __instance,
                out PreviewState? state
            ))
        {
            return;
        }

        __result = HighlightAddedOrChangedText(
            state.BeforeDescription,
            __result
        );
    }

    private static void MarkChangedDynamicVars(
        CardModel card,
        IReadOnlyDictionary<string, decimal> beforeValues
    )
    {
        foreach ((string name, DynamicVar variable) in card.DynamicVars)
        {
            if (beforeValues.TryGetValue(name, out decimal before) &&
                before != variable.BaseValue)
            {
                // 数值已经由升转逻辑写入；增加 0 只设置原生
                // WasJustUpgraded 标记，使 :diff() 按绿色显示。
                variable.UpgradeValueBy(0);
            }
        }
    }

    private static bool IsEligible(
        AbstractGuWormCard gu,
        PreviewContext context,
        bool checkExcluded
    )
    {
        if (checkExcluded && context.ExcludedCards.Contains(gu))
        {
            return false;
        }

        int slotCost = gu.GuRank < GuZhenRenCardRules.XianGuRank
            ? 1
            : 2;

        return slotCost <= context.RemainingSlots &&
            gu.GuRank < gu.MaxGuRank &&
            GuZhenRenCardRules.CanReachGuRank(
                gu,
                gu.GuRank + 1
            );
    }

    private static PreviewContext? GetActiveContext()
    {
        lock (ScopeLock)
        {
            return _activeContext;
        }
    }

    private static string HighlightAddedOrChangedText(
        string beforeFormatted,
        string afterFormatted
    )
    {
        string before = StripBbCode(beforeFormatted);
        string after = StripBbCode(afterFormatted);
        List<DescriptionToken> beforeTokens = Tokenize(before);
        List<DescriptionToken> afterTokens = Tokenize(after);
        bool[] matchedAfter = FindMatchedAfterTokens(
            beforeTokens,
            afterTokens
        );
        bool[] highlighted = new bool[after.Length];

        for (int index = 0; index < afterTokens.Count; index++)
        {
            DescriptionToken token = afterTokens[index];
            if (matchedAfter[index] || token.IsWhitespace)
            {
                continue;
            }

            Array.Fill(
                highlighted,
                true,
                token.Start,
                token.Length
            );
        }

        // 两个变化片段之间只有空白时一并着色，英文预览不会出现
        // 逐词断开的绿色标签。
        for (int index = 0; index < highlighted.Length; index++)
        {
            if (highlighted[index] || !char.IsWhiteSpace(after[index]))
            {
                continue;
            }

            int end = index;
            while (end < highlighted.Length &&
                   char.IsWhiteSpace(after[end]))
            {
                end++;
            }

            bool leftChanged = index > 0 && highlighted[index - 1];
            bool rightChanged =
                end < highlighted.Length && highlighted[end];
            if (leftChanged && rightChanged)
            {
                Array.Fill(
                    highlighted,
                    true,
                    index,
                    end - index
                );
            }

            index = end - 1;
        }

        if (!highlighted.Any(value => value))
        {
            return afterFormatted;
        }

        return ApplyVisibleHighlights(afterFormatted, highlighted);
    }

    private static bool[] FindMatchedAfterTokens(
        IReadOnlyList<DescriptionToken> before,
        IReadOnlyList<DescriptionToken> after
    )
    {
        int[,] lengths = new int[before.Count + 1, after.Count + 1];

        for (int left = before.Count - 1; left >= 0; left--)
        {
            for (int right = after.Count - 1; right >= 0; right--)
            {
                lengths[left, right] =
                    before[left].Text == after[right].Text
                        ? lengths[left + 1, right + 1] + 1
                        : Math.Max(
                            lengths[left + 1, right],
                            lengths[left, right + 1]
                        );
            }
        }

        bool[] matched = new bool[after.Count];
        int beforeIndex = 0;
        int afterIndex = 0;

        while (beforeIndex < before.Count &&
               afterIndex < after.Count)
        {
            if (before[beforeIndex].Text == after[afterIndex].Text)
            {
                matched[afterIndex] = true;
                beforeIndex++;
                afterIndex++;
            }
            else if (lengths[beforeIndex + 1, afterIndex] >=
                     lengths[beforeIndex, afterIndex + 1])
            {
                beforeIndex++;
            }
            else
            {
                afterIndex++;
            }
        }

        return matched;
    }

    private static List<DescriptionToken> Tokenize(string text)
    {
        List<DescriptionToken> result = [];
        int index = 0;

        while (index < text.Length)
        {
            int start = index;
            bool whitespace = char.IsWhiteSpace(text[index]);
            bool word = char.IsLetterOrDigit(text[index]) ||
                text[index] == '_' ||
                text[index] == '%';
            index++;

            while (index < text.Length)
            {
                bool nextWhitespace = char.IsWhiteSpace(text[index]);
                bool nextWord = char.IsLetterOrDigit(text[index]) ||
                    text[index] == '_' ||
                    text[index] == '%';

                if (whitespace != nextWhitespace ||
                    (!whitespace && word != nextWord) ||
                    (!whitespace && !word))
                {
                    break;
                }

                index++;
            }

            result.Add(
                new DescriptionToken(
                    start,
                    index - start,
                    text[start..index],
                    whitespace
                )
            );
        }

        return result;
    }

    private static string StripBbCode(string formatted)
    {
        StringBuilder result = new(formatted.Length);

        for (int index = 0; index < formatted.Length; index++)
        {
            if (formatted[index] == '[')
            {
                int tagEnd = formatted.IndexOf(']', index + 1);
                if (tagEnd >= 0)
                {
                    index = tagEnd;
                    continue;
                }
            }

            result.Append(formatted[index]);
        }

        return result.ToString();
    }

    private static string ApplyVisibleHighlights(
        string formatted,
        IReadOnlyList<bool> highlighted
    )
    {
        StringBuilder result = new(formatted.Length + 64);
        int visibleIndex = 0;
        bool greenOpen = false;

        for (int index = 0; index < formatted.Length; index++)
        {
            if (formatted[index] == '[')
            {
                int tagEnd = formatted.IndexOf(']', index + 1);
                if (tagEnd >= 0)
                {
                    if (greenOpen)
                    {
                        result.Append("[/green]");
                        greenOpen = false;
                    }

                    result.Append(
                        formatted,
                        index,
                        tagEnd - index + 1
                    );
                    index = tagEnd;
                    continue;
                }
            }

            bool shouldBeGreen =
                visibleIndex < highlighted.Count &&
                highlighted[visibleIndex];

            if (shouldBeGreen && !greenOpen)
            {
                result.Append("[green]");
                greenOpen = true;
            }
            else if (!shouldBeGreen && greenOpen)
            {
                result.Append("[/green]");
                greenOpen = false;
            }

            result.Append(formatted[index]);
            visibleIndex++;
        }

        if (greenOpen)
        {
            result.Append("[/green]");
        }

        return result.ToString();
    }

    private sealed class PreviewScope(
        PreviewContext current,
        PreviewContext? previous
    ) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (ScopeLock)
            {
                if (ReferenceEquals(_activeContext, current))
                {
                    _activeContext = previous;
                }
            }

            _disposed = true;
        }
    }
}
