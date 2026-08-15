using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using GuZhenRen.Cards;
using GuZhenRen.Combat;

using Godot;

using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Content;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 本地化兼容层。
///
/// 1. 按当前语言从对应语言目录的单一 JSON 读取文本；
/// 2. 从 JSON 为旧 PCK 中缺失的合练/升炼文本提供中英文回退；
/// 3. 从 JSON 覆盖旧版卡牌描述，使效果文本与新代码一致；
/// 4. 修正旧 PCK 中缺少 SmartFormat 空格式段分隔符的能量图标写法。
///
/// 所有逻辑都只读取 LocString、当前语言和 GuZhenRenPersonal/localization/{language} 下的 JSON，
/// 不读取本地玩家、战斗状态或随机数；多人各端使用相同 JSON 时会得到相同文本。
/// </summary>
internal static partial class LocalizationCompatibilityPatch
{
    private const string PatcherName =
        "LocalizationCompatibility";

    private const string LocalizationFileName =
        "LocalizationCompatibilityPatch.localization.json";

    private static readonly JsonSerializerOptions
        LocalizationJsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

    private static string? _loadedLanguage;
    private static LocalizationDocument? _loadedLocalizationData;

    private static LocalizationDocument
        LocalizationData =>
        GetLocalizationDocument();

    private static IReadOnlyDictionary<
        string,
        string
    > KeywordLocalizationFallbacks =>
        BuildKeywordLocalizationFallbacks();

    private static IReadOnlyDictionary<
        string,
        string
    > CardDescriptionOverrides =>
        LocalizationData.CardDescriptionOverrides;

    private static IReadOnlyDictionary<
        string,
        string
    > CharacterNameOverrides =>
        LocalizationData.CharacterNameOverrides;

    private static IReadOnlyDictionary<
        string,
        string
    > RestSiteLocalizationFallbacks =>
        LocalizationData.RestSiteLocalizationFallbacks;

    private static IReadOnlyDictionary<
        (string Table, string Key),
        string
    > CombatInterfaceLocalizationFallbacks =>
        BuildCombatInterfaceLocalizationFallbacks();

    private static readonly PropertyInfo HoverTipTitleProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Title))!;

    private static readonly PropertyInfo HoverTipDescriptionProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Description))!;

    private static readonly PropertyInfo HoverTipIconProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Icon))!;

    private static ModPatcher? _patcher;
    private static bool _initialized;

    /// <summary>
    /// 匹配缺少末尾空格式段冒号的 energyIcons 调用。
    /// 已修正的 “:energyIcons(...):}” 不会再次命中。
    /// </summary>
    /*
     * 不使用 GeneratedRegex：
     * Godot 的 .godot/mono/temp/obj 可能残留并再次编译生成的 g.cs，
     * 从而让源声明与生成实现同时成为候选并触发 CS0121。
     */
    private static readonly Regex
        LegacyEnergyIconsSyntaxRegex =
        new(
            @":energyIcons\((?<options>[^{}()]*)\)\}",
            RegexOptions.CultureInvariant
        );

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
                ProvideCombatInterfaceLocalizationFallbackPatch
            >();
            _patcher.RegisterPatch<
                ProvideFormattedCombatInterfaceLocalizationFallbackPatch
            >();
            _patcher.RegisterPatch<
                ProvideSecondaryResourceHoverTipLocalizationPatch
            >();
            _patcher.RegisterPatch<
                ProvideCardDescriptionOverridePatch
            >();
            _patcher.RegisterPatch<
                ProvideCharacterNameOverridePatch
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
            _loadedLanguage = null;
            _loadedLocalizationData = null;
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
    /// 按当前语言加载独立 JSON。
    /// 简体中文使用 zhs；其他语言默认使用 eng。
    /// </summary>
    private static LocalizationDocument
        GetLocalizationDocument()
    {
        string language = string.Equals(
            LocManager.Instance.Language,
            "zhs",
            StringComparison.OrdinalIgnoreCase
        )
            ? "zhs"
            : "eng";

        if (_loadedLocalizationData is not null &&
            string.Equals(
                _loadedLanguage,
                language,
                StringComparison.Ordinal
            ))
        {
            return _loadedLocalizationData;
        }

        _loadedLanguage = language;
        _loadedLocalizationData =
            LoadLocalizationDocument(language);
        return _loadedLocalizationData;
    }

    private static LocalizationDocument
        LoadLocalizationDocument(
            string language
        )
    {
        try
        {
            string localizationFileName =
                LocalizationFileName;

            string? assemblyDirectory =
                Path.GetDirectoryName(
                    typeof(LocalizationCompatibilityPatch)
                        .Assembly
                        .Location
                );

            string[] candidates =
            [
                /*
                 * 首选项目与模组发布目录：
                 * GuZhenRenPersonal/localization/zhs/<language file>
                 * GuZhenRenPersonal/localization/eng/<language file>
                 */
                Path.Combine(
                    assemblyDirectory ??
                        AppContext.BaseDirectory,
                    "GuZhenRenPersonal",
                    "localization",
                    language,
                    localizationFileName
                ),
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GuZhenRenPersonal",
                    "localization",
                    language,
                    localizationFileName
                ),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "GuZhenRenPersonal",
                    "localization",
                    language,
                    localizationFileName
                ),

                /*
                 * 兼容上一版目录：
                 * GuZhenRenPersonal/localization/<language file>
                 */
                Path.Combine(
                    assemblyDirectory ??
                        AppContext.BaseDirectory,
                    "GuZhenRenPersonal",
                    "localization",
                    localizationFileName
                ),
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GuZhenRenPersonal",
                    "localization",
                    localizationFileName
                ),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "GuZhenRenPersonal",
                    "localization",
                    localizationFileName
                ),

                /*
                 * 兼容最早版本：JSON 与 DLL 位于同一目录。
                 */
                Path.Combine(
                    assemblyDirectory ??
                        AppContext.BaseDirectory,
                    localizationFileName
                ),
                Path.Combine(
                    AppContext.BaseDirectory,
                    localizationFileName
                ),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    localizationFileName
                ),
            ];

            string? localizationPath = null;
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    localizationPath = candidate;
                    break;
                }
            }

            if (localizationPath is null)
            {
                TryWarn(
                    "未找到本地化 JSON，将继续使用 PCK 原有文本：" +
                    localizationFileName
                );
                return new LocalizationDocument();
            }

            string json = File.ReadAllText(
                localizationPath
            );
            LocalizationDocument? document =
                JsonSerializer.Deserialize<
                    LocalizationDocument
                >(
                    json,
                    LocalizationJsonOptions
                );

            if (document is null)
            {
                TryWarn(
                    "本地化 JSON 内容为空，将继续使用 PCK 原有文本：" +
                    localizationPath
                );
                return new LocalizationDocument();
            }

            document.Normalize();
            return document;
        }
        catch (Exception exception)
        {
            TryWarn(
                "加载本地化 JSON 失败，将继续使用 PCK 原有文本：" +
                exception
            );
            return new LocalizationDocument();
        }
    }

    private static IReadOnlyDictionary<
        string,
        string
    > BuildKeywordLocalizationFallbacks()
    {
        Dictionary<string, string>
            fallbacks = new(
                StringComparer.Ordinal
            );

        foreach (
            KeyValuePair<
                string,
                KeywordLocalizationEntry
            > pair in LocalizationData.Keywords
        )
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            string keywordId = ModContentRegistry
                .GetQualifiedKeywordId(
                    Entry.ModId,
                    pair.Key
                );

            fallbacks[keywordId + ".title"] =
                pair.Value.Title;
            fallbacks[keywordId + ".description"] =
                pair.Value.Description;
        }

        return fallbacks;
    }

    private static IReadOnlyDictionary<
        (string Table, string Key),
        string
    > BuildCombatInterfaceLocalizationFallbacks()
    {
        Dictionary<
            (string Table, string Key),
            string
        > fallbacks = new();

        foreach (
            TableLocalizationEntry entry in
                LocalizationData
                    .CombatInterfaceLocalizationFallbacks
        )
        {
            if (string.IsNullOrWhiteSpace(entry.Table) ||
                string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            string expandedKey = entry.Key
                .Replace(
                    "{GuCardPileId}",
                    GuCardPileSystem.PileId,
                    StringComparison.Ordinal
                )
                .Replace(
                    "{GuCardDiscardPileId}",
                    GuCardPileSystem.DiscardPileId,
                    StringComparison.Ordinal
                )
                .Replace(
                    "{GuCardStoragePileId}",
                    GuCardPileSystem.StoragePileId,
                    StringComparison.Ordinal
                )
                .Replace(
                    "{GuCardRecoveryPileId}",
                    GuCardPileSystem.RecoveryPileId,
                    StringComparison.Ordinal
                );

            fallbacks[
                (
                    entry.Table,
                    expandedKey
                )
            ] = entry.Text;
        }

        return fallbacks;
    }

    private sealed class KeywordLocalizationEntry
    {
        public KeywordLocalizationEntry()
        {
        }

        public string Title
        {
            get;
            set;
        } = "";

        public string Description
        {
            get;
            set;
        } = "";
    }

    private sealed class TableLocalizationEntry
    {
        public TableLocalizationEntry()
        {
        }

        public string Table
        {
            get;
            set;
        } = "";

        public string Key
        {
            get;
            set;
        } = "";

        public string Text
        {
            get;
            set;
        } = "";
    }

    private sealed class LocalizationDocument
    {
        public LocalizationDocument()
        {
        }

        public int SchemaVersion
        {
            get;
            set;
        } = 1;

        public string Language
        {
            get;
            set;
        } = "";

        public Dictionary<
            string,
            KeywordLocalizationEntry
        > Keywords
        {
            get;
            set;
        } = new(
            StringComparer.Ordinal
        );

        public Dictionary<
            string,
            string
        > CardDescriptionOverrides
        {
            get;
            set;
        } = new(
            StringComparer.Ordinal
        );

        public Dictionary<
            string,
            string
        > CharacterNameOverrides
        {
            get;
            set;
        } = new(
            StringComparer.Ordinal
        );

        public Dictionary<
            string,
            string
        > RestSiteLocalizationFallbacks
        {
            get;
            set;
        } = new(
            StringComparer.Ordinal
        );

        public List<TableLocalizationEntry>
            CombatInterfaceLocalizationFallbacks
        {
            get;
            set;
        } = [];

        public void Normalize()
        {
            Keywords ??= new(
                StringComparer.Ordinal
            );
            CardDescriptionOverrides ??= new(
                StringComparer.Ordinal
            );
            CharacterNameOverrides ??= new(
                StringComparer.Ordinal
            );
            RestSiteLocalizationFallbacks ??= new(
                StringComparer.Ordinal
            );
            CombatInterfaceLocalizationFallbacks ??= [];
        }
    }

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
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            // PCK 或其他本地化包已提供条目时，完全保留原文本。
            // 这些条目由代码始终覆盖，确保只替换 DLL 时不会继续读取
            // 旧 PCK 中过时的“两张材料”说明。
            __result = fallback;

            return false;
        }
    }

    /// <summary>
    /// 当旧 PCK 未打包模组篝火文本时，为合练与升炼提供
    /// JSON 中英文回退；实际本地化条目存在时仍以资源文件为准。
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
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            // 自定义篝火规则经常只通过 DLL 更新，因此这些 key
            // 始终由当前兼容层覆盖，避免继续读取旧 PCK 文本。
            __result = fallback;

            return false;
        }
    }

    /// <summary>
    /// 为蛊虫牌堆右键操作、弃牌堆和元气表提供 JSON 文本。
    /// 始终覆盖旧 PCK 中同名条目，避免只替换 DLL 时仍显示旧说明。
    /// </summary>
    private sealed class
        ProvideCombatInterfaceLocalizationFallbackPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.CombatInterfaceFallback";

        public static bool IsCritical => false;

        public static string Description =>
            "Provide localization for Gu piles and Yuan Qi UI";

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
            if (!CombatInterfaceLocalizationFallbacks.TryGetValue(
                    (
                        __instance.LocTable,
                        __instance.LocEntryKey
                    ),
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            __result = fallback;

            return false;
        }
    }

    /// <summary>
    /// HoverTip 会直接调用 LocString.GetFormattedText；在部分游戏构建中，
    /// SmartFormat 内部对 GetRawText 的调用会被内联，导致上面的原始文本
    /// 回退无法接管。这里为不含格式变量的战斗界面文本补充同等回退。
    ///
    /// selectionPrompt 含 {SelectedCount}，继续交给 SmartFormat 处理，
    /// 避免直接返回文本时丢失动态变量替换。
    /// </summary>
    private sealed class
        ProvideFormattedCombatInterfaceLocalizationFallbackPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.FormattedCombatInterfaceFallback";

        public static bool IsCritical => false;

        public static string Description =>
            "Provide formatted localization for Gu piles and Yuan Qi UI";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod<LocString>(
                nameof(LocString.GetFormattedText)
            ),
        ];

        private static bool Prefix(
            LocString __instance,
            ref string __result
        )
        {
            if (string.Equals(
                    __instance.LocTable,
                    "static_hover_tips",
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    __instance.LocEntryKey,
                    GuCardPileSystem.PileId + ".selectionPrompt",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            if (!CombatInterfaceLocalizationFallbacks.TryGetValue(
                    (
                        __instance.LocTable,
                        __instance.LocEntryKey
                    ),
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            __result = fallback;

            return false;
        }
    }

    /// <summary>
    /// RitsuLib 会先调用 LocTable.GetLocString 检查条目是否真实存在；
    /// 缝补旧 PCK 时，缺失条目会在 LocString 回退补丁运行前就被转换成
    /// 键名占位符。这里仅接管本模组的元气 HoverTip 工厂调用，直接创建
    /// 已本地化的原生 HoverTip，避免依赖缺失的 PCK 条目。
    /// </summary>
    private sealed class
        ProvideSecondaryResourceHoverTipLocalizationPatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.SecondaryResourceHoverTip";

        public static bool IsCritical => false;

        public static string Description =>
            "Provide raw localized Yuan Qi hover tip";

        public static ModPatchTarget[] GetTargets() =>
        [
            PatchTarget.OptionalMethod(
                typeof(SecondaryResourceHoverTipFactory),
                nameof(SecondaryResourceHoverTipFactory.Create),
                typeof(SecondaryResourceDefinition),
                typeof(int),
                typeof(int?)
            ),
        ];

        private static bool Prefix(
            SecondaryResourceDefinition definition,
            ref HoverTip __result
        )
        {
            if (!string.Equals(
                    definition.Id,
                    YuanQiSystem.ResourceId,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            IReadOnlyDictionary<
                (string Table, string Key),
                string
            > fallbacks =
                CombatInterfaceLocalizationFallbacks;

            if (!fallbacks.TryGetValue(
                    (
                        "secondary_resources",
                        "GU_ZHEN_REN_PERSONAL_SECONDARY_RESOURCE_YUAN_QI.title"
                    ),
                    out string? title
                ) ||
                !fallbacks.TryGetValue(
                    (
                        "secondary_resources",
                        "GU_ZHEN_REN_PERSONAL_SECONDARY_RESOURCE_YUAN_QI.description"
                    ),
                    out string? description
                ))
            {
                return true;
            }

            Texture2D? icon = null;
            string? iconPath = definition.LargeIconPath ??
                definition.SmallIconPath;
            if (!string.IsNullOrWhiteSpace(iconPath) &&
                ResourceLoader.Exists(iconPath.Trim()))
            {
                icon = ResourceLoader.Load<Texture2D>(
                    iconPath.Trim()
                );
            }

            __result = CreateRawHoverTip(
                definition.Id,
                title,
                description,
                icon
            );
            return false;
        }
    }

    private static HoverTip CreateRawHoverTip(
        string id,
        string title,
        string description,
        Texture2D? icon
    )
    {
        object boxed = default(HoverTip);
        HoverTipTitleProperty.SetValue(boxed, title);
        HoverTipDescriptionProperty.SetValue(boxed, description);
        HoverTipIconProperty.SetValue(boxed, icon);

        HoverTip tip = (HoverTip)boxed;
        tip.Id = id;
        return tip;
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
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            __result = fallback;

            return false;
        }
    }

    /// <summary>
    /// 角色显示名始终使用“古月方源”，同时覆盖旧 PCK 里可能存在的“蛊真人”名称。
    /// </summary>
    private sealed class
        ProvideCharacterNameOverridePatch
        : IPatchMethod
    {
        public static string PatchId =>
            Entry.ModId +
            ".Localization.CharacterNameOverride";

        public static bool IsCritical => false;

        public static string Description =>
            "Override Gu Zhen Ren character name with Gu Yue Fang Yuan";

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
                    "characters",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            string entryKey = __instance.LocEntryKey;
            int separator = entryKey.LastIndexOf('.');

            if (separator <= 0 ||
                !entryKey[..separator].Contains(
                    "GU_ZHEN_REN_PERSONAL_CHARACTER",
                    StringComparison.Ordinal
                ) ||
                !CharacterNameOverrides.TryGetValue(
                    entryKey[(separator + 1)..],
                    out string? fallback
                ) ||
                fallback is null)
            {
                return true;
            }

            __result = fallback;

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
                LegacyEnergyIconsSyntaxRegex.Replace(
                    __result,
                    ":energyIcons(${options}):}"
                );
        }
    }
}
