using System.Text.RegularExpressions;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 本地化兼容层。
///
/// 1. 为“虚影”“仙蛊”和一至九转关键词提供中英文代码本地化；
/// 2. 为旧 PCK 中缺失的合练/升炼文本提供中英文回退；
/// 3. 覆盖白豕蛊与小光蛊的旧版卡牌描述，使效果文本与新代码一致；
/// 4. 修正旧 PCK 中缺少 SmartFormat 空格式段分隔符的能量图标写法。
///
/// 所有逻辑都只读取 LocString、当前语言和确定性的静态映射，
/// 不读取本地玩家、战斗状态或随机数；多人各端会得到相同文本。
/// </summary>
internal static partial class LocalizationCompatibilityPatch
{
    private const string PatcherName =
        "LocalizationCompatibility";

    private static readonly IReadOnlyDictionary<
        string,
        KeywordLocalizationFallback
    > KeywordLocalizationFallbacks =
        BuildKeywordLocalizationFallbacks();

    private static readonly IReadOnlyDictionary<
        string,
        KeywordLocalizationFallback
    > CardDescriptionOverrides =
        new Dictionary<
            string,
            KeywordLocalizationFallback
        >
        {
            [
                "GU_ZHEN_REN_CARD_BAI_SHI_GU.description"
            ] = new(
                Zhs:
                    "获得{Dexterity:diff()}层临时[gold]敏捷[/gold]。{BlockRetentionTurns:cond:>0?接下来{BlockRetentionTurns}个回合，防御不会在你的回合开始时消失。|}",
                Eng:
                    "Gain {Dexterity:diff()} temporary [gold]Dexterity[/gold]. {BlockRetentionTurns:cond:>0?For the next {BlockRetentionTurns} turn(s), your Block is not removed at the start of your turn.|}"
            ),
            [
                "GU_ZHEN_REN_CARD_XIAO_GUANG_GU.description"
            ] = new(
                Zhs:
                    "施加{WeakPower:diff()}层[gold]虚弱[/gold]。获得{ShanYao}层[gold]闪耀[/gold]。",
                Eng:
                    "Apply {WeakPower:diff()} [gold]Weak[/gold]. Gain {ShanYao} [gold]Radiance[/gold]."
            ),
            [
                "GU_ZHEN_REN_CARD_FEI_XIONG_XU_YING.description"
            ] = new(
                Zhs:
                    "当你打出一张攻击牌时，有{ChancePercent}%概率对所有敌人造成{Damage:diff()}点伤害。 NL 力量对这次伤害提供{StrengthMultiplier}倍效果。",
                Eng:
                    "Whenever you play an Attack, have a {ChancePercent}% chance to deal {Damage:diff()} damage to ALL enemies. NL Strength affects this damage {StrengthMultiplier} times."
            ),
        };

    private static readonly IReadOnlyDictionary<
        string,
        KeywordLocalizationFallback
    > RestSiteLocalizationFallbacks =
        new Dictionary<
            string,
            KeywordLocalizationFallback
        >
        {
            ["OPTION_GU_ZHEN_REN_GU_RANK_UP.name"] = new(
                Zhs: "升炼",
                Eng: "Gu Refinement"
            ),
            ["OPTION_GU_ZHEN_REN_GU_RANK_UP.description"] = new(
                Zhs: "选择一至两张尚未达到九转的蛊牌，使每张各提升一转。每个休息点只能成功升炼一次。",
                Eng: "Choose one or two Gu cards below rank 9 and increase each selected card by 1 rank. Refinement can succeed once per rest site."
            ),
            ["OPTION_GU_ZHEN_REN_GU_RANK_UP.selectionPrompt"] = new(
                Zhs: "选择一至两张要提升一转的蛊牌，然后点击确认。",
                Eng: "Choose one or two Gu cards to increase by 1 rank, then click Confirm."
            ),
            ["OPTION_GU_ZHEN_REN_HE_LIAN.name"] = new(
                Zhs: "合练",
                Eng: "Gu Fusion"
            ),
            ["OPTION_GU_ZHEN_REN_HE_LIAN.description"] = new(
                Zhs: "选择一张或多张蛊虫牌并主动点击确认。可选数量上限由当前可制作配方决定；只有材料种类和数量完整匹配配方时才会消耗材料并获得结果。失败不会消耗本次机会。每个休息点只能成功合练一次。",
                Eng: "Choose one or more Gu cards and click Confirm manually. The selection limit is determined by currently craftable recipes. Materials are consumed only when both their types and quantities match a recipe. A failed attempt does not consume the use. Fusion can succeed once per rest site."
            ),
            ["OPTION_GU_ZHEN_REN_HE_LIAN.selectionPrompt"] = new(
                Zhs: "选择合练材料，然后点击确认。材料数量由配方决定。",
                Eng: "Choose fusion materials, then click Confirm. The required count depends on the recipe."
            ),
        };

    private static ModPatcher? _patcher;
    private static bool _initialized;

    /// <summary>
    /// 匹配缺少末尾空格式段冒号的 energyIcons 调用。
    /// 已修正的 “:energyIcons(...):}” 不会再次命中。
    /// </summary>
    [GeneratedRegex(
        @":energyIcons\((?<options>[^{}()]*)\)\}",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex LegacyEnergyIconsSyntaxRegex();

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        /*
         * 展示层兼容不应阻断整个模组。
         * 先标记已尝试，防止可选补丁失败后在同一次启动中反复注册。
         */
        _initialized = true;

        try
        {
            _patcher =
                RitsuLibFramework.CreatePatcher(
                    Entry.ModId,
                    PatcherName,
                    "localization compatibility"
                );

            _patcher.RegisterPatch<
                ProvideKeywordLocalizationFallbackPatch
            >();
            _patcher.RegisterPatch<
                ProvideRestSiteLocalizationFallbackPatch
            >();
            _patcher.RegisterPatch<
                ProvideCardDescriptionOverridePatch
            >();
            _patcher.RegisterPatch<
                NormalizeLegacyEnergyIconSyntaxPatch
            >();

            /*
             * 所有补丁均 IsCritical=false。
             * RitsuLib 会记录可选补丁失败，但不会因此阻断模组加载。
             */
            _patcher.PatchAll();
        }
        catch (Exception exception)
        {
            /*
             * CreatePatcher/RegisterPatch 等外围异常也按可选功能处理。
             * 这里绝不能把本地化展示问题升级为整个模组初始化失败。
             */
            TryUnpatch();
            _patcher = null;

            TryWarn(
                "本地化兼容补丁安装失败，已跳过该可选展示功能：" +
                exception
            );
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            TryUnpatch();
        }
        finally
        {
            _patcher = null;
            _initialized = false;
        }
    }

    private static void TryUnpatch()
    {
        try
        {
            _patcher?.UnpatchAll();
        }
        catch (Exception exception)
        {
            TryWarn(
                "撤销本地化兼容补丁失败：" +
                exception
            );
        }
    }

    private static void TryWarn(string message)
    {
        try
        {
            Entry.Logger.Warn(message);
        }
        catch
        {
            // 可选展示补丁的日志失败同样不能阻断模组初始化。
        }
    }

    /// <summary>
    /// 构建新关键词的代码内回退本地化。
    ///
    /// 若 PCK 后续加入同名 card_keywords 条目，原始本地化优先，
    /// 此映射会自动停止接管相应 key。
    /// </summary>
    private static IReadOnlyDictionary<
        string,
        KeywordLocalizationFallback
    > BuildKeywordLocalizationFallbacks()
    {
        Dictionary<string, KeywordLocalizationFallback>
            fallbacks = new();

        AddKeywordFallback(
            fallbacks,
            nameof(GuZhenRenKeywords.XuYing),
            zhsTitle: "虚影",
            zhsDescription:
                "回合结束时保留在手牌中。不能被手动打出。",
            engTitle: "Phantom",
            engDescription:
                "Retained in your hand at the end of the turn. Cannot be played manually."
        );

        AddKeywordFallback(
            fallbacks,
            nameof(GuZhenRenKeywords.XianGu),
            zhsTitle: "仙蛊",
            zhsDescription:
                "六转及以上的蛊。每局所有玩家只能永久拥有一张同名仙蛊；此规则与[gold]唯一[/gold]关键词相互独立。",
            engTitle: "Immortal Gu",
            engDescription:
                "A rank 6 or higher Gu. Across the run, all players may permanently own only one Immortal Gu with the same card identity. This rule is independent of [gold]Unique[/gold]."
        );

        string[] chineseRankNames =
        [
            "一转",
            "二转",
            "三转",
            "四转",
            "五转",
            "六转",
            "七转",
            "八转",
            "九转",
        ];

        for (int rank = 1; rank <= 9; rank++)
        {
            string title =
                chineseRankNames[rank - 1];

            AddKeywordFallback(
                fallbacks,
                "Rank" + rank,
                zhsTitle: title,
                zhsDescription:
                    $"这是一张{title}蛊牌。",
                engTitle: "Rank " + rank,
                engDescription:
                    $"This is a rank {rank} Gu card."
            );
        }

        return fallbacks;
    }

    private static void AddKeywordFallback(
        IDictionary<string, KeywordLocalizationFallback>
            fallbacks,
        string localName,
        string zhsTitle,
        string zhsDescription,
        string engTitle,
        string engDescription
    )
    {
        string keywordId = ModContentRegistry
            .GetQualifiedKeywordId(
                Entry.ModId,
                localName
            );

        fallbacks[keywordId + ".title"] =
            new KeywordLocalizationFallback(
                zhsTitle,
                engTitle
            );
        fallbacks[keywordId + ".description"] =
            new KeywordLocalizationFallback(
                zhsDescription,
                engDescription
            );
    }

    private readonly record struct
        KeywordLocalizationFallback(
            string Zhs,
            string Eng
        );

    private static bool HasLocalizationEntry(
        string tableName,
        string entryKey
    )
    {
        try
        {
            return LocManager.Instance
                .GetTable(tableName)
                .HasEntry(entryKey);
        }
        catch (LocException)
        {
            // rest_site_ui 是游戏原生表；这里只处理模组条目尚未合并的情况。
            return false;
        }
    }

    /// <summary>
    /// 在 PCK 尚未包含新增关键词文本时提供回退。
    /// 已存在的实际本地化条目始终优先。
    /// </summary>
    private sealed class
        ProvideKeywordLocalizationFallbackPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.KeywordFallback";

        public static bool IsCritical => false;

        public static string Description =>
            "Provide code-only localization for Gu card keywords";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod<LocString>(
                nameof(LocString.GetRawText)
            ),
        ];

        private static bool Prefix(
            LocString __instance,
            ref string __result
        )
        {
            if (!string.Equals(
                    __instance.LocTable,
                    "card_keywords",
                    StringComparison.Ordinal
                ) ||
                !KeywordLocalizationFallbacks.TryGetValue(
                    __instance.LocEntryKey,
                    out KeywordLocalizationFallback fallback
                ))
            {
                return true;
            }

            // PCK 或其他本地化包已提供条目时，完全保留原文本。
            // 这些条目由代码始终覆盖，确保只替换 DLL 时不会继续读取
            // 旧 PCK 中过时的“两张材料”说明。
            __result = string.Equals(
                LocManager.Instance.Language,
                "zhs",
                StringComparison.OrdinalIgnoreCase
            )
                ? fallback.Zhs
                : fallback.Eng;

            return false;
        }
    }

    /// <summary>
    /// 当旧 PCK 未打包模组篝火文本时，为合练与升炼提供
    /// 中英文代码回退；实际本地化条目存在时仍以资源文件为准。
    /// </summary>
    private sealed class
        ProvideRestSiteLocalizationFallbackPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.RestSiteFallback";

        public static bool IsCritical => false;

        public static string Description =>
            "Provide localization fallback for Gu rest site options";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod<LocString>(
                nameof(LocString.GetRawText)
            ),
        ];

        private static bool Prefix(
            LocString __instance,
            ref string __result
        )
        {
            if (!string.Equals(
                    __instance.LocTable,
                    "rest_site_ui",
                    StringComparison.Ordinal
                ) ||
                !RestSiteLocalizationFallbacks.TryGetValue(
                    __instance.LocEntryKey,
                    out KeywordLocalizationFallback fallback
                ))
            {
                return true;
            }

            // 自定义篝火规则经常只通过 DLL 更新，因此这些 key
            // 始终由当前代码覆盖，避免继续读取旧 PCK 文本。
            __result = string.Equals(
                LocManager.Instance.Language,
                "zhs",
                StringComparison.OrdinalIgnoreCase
            )
                ? fallback.Zhs
                : fallback.Eng;

            return false;
        }
    }

    /// <summary>
    /// 旧 PCK 中的两张牌仍描述永久敏捷与伤害。
    /// 代码交付无法直接修改 PCK，因此在读取原始文本时覆盖这两个 key。
    /// 只依赖当前语言和静态文本，不涉及任何玩家或战斗状态。
    /// </summary>
    private sealed class
        ProvideCardDescriptionOverridePatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.CardDescriptionOverrides";

        public static bool IsCritical => false;

        public static string Description =>
            "Override legacy and hidden-keyword card descriptions";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod<LocString>(
                nameof(LocString.GetRawText)
            ),
        ];

        private static bool Prefix(
            LocString __instance,
            ref string __result
        )
        {
            if (!string.Equals(
                    __instance.LocTable,
                    "cards",
                    StringComparison.Ordinal
                ) ||
                !CardDescriptionOverrides.TryGetValue(
                    __instance.LocEntryKey,
                    out KeywordLocalizationFallback fallback
                ))
            {
                return true;
            }

            __result = string.Equals(
                LocManager.Instance.Language,
                "zhs",
                StringComparison.OrdinalIgnoreCase
            )
                ? fallback.Zhs
                : fallback.Eng;

            return false;
        }
    }

    /// <summary>
    /// 使用 RitsuLib 的可选 IPatchMethod 形式，仅补丁结构简单的
    /// LocString.GetRawText；不再直接补丁含异常过滤器的
    /// LocManager.SmartFormat，避免 Harmony 生成异常块失败。
    /// </summary>
    private sealed class NormalizeLegacyEnergyIconSyntaxPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.NormalizeLegacyEnergyIconSyntax";

        public static bool IsCritical => false;

        public static string Description =>
            "Normalize legacy energyIcons SmartFormat syntax";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod<LocString>(
                nameof(LocString.GetRawText)
            ),
        ];

        private static void Postfix(
            ref string __result
        )
        {
            if (string.IsNullOrEmpty(__result) ||
                !__result.Contains(
                    ":energyIcons(",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            __result =
                LegacyEnergyIconsSyntaxRegex().Replace(
                    __result,
                    ":energyIcons(${options}):}"
                );
        }
    }
}
