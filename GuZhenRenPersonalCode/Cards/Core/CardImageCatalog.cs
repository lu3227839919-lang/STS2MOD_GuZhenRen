using System.Reflection;

using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards;

/// <summary>
/// 统一管理蛊真人模组所有卡牌的卡图路径。
///
/// 每张具体卡牌都使用与运行时类型同名的 PNG：
/// res://GuZhenRenPersonal/images/cards/{CardTypeName}.png
/// </summary>
public static class CardImageCatalog
{
    public const string ResourceImageDirectory =
        "res://GuZhenRenPersonal/images/cards";

    private static readonly object WarningLock = new();

    private static readonly HashSet<string> WarnedPaths =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 为具体卡牌创建统一的卡图配置。
    /// 即使文件缺失也保留预期路径，让 Godot/RitsuLib 的资源诊断继续可见。
    /// </summary>
    public static CardAssetProfile Create(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);

        WarnIfMissing(cardType);
        return new CardAssetProfile(
            PortraitPath: GetResourcePath(cardType)
        );
    }

    public static string GetResourcePath(Type cardType)
    {
        ArgumentNullException.ThrowIfNull(cardType);
        return $"{Entry.ResPath}/images/cards/{cardType.Name}.png";
    }

    /// <summary>
    /// 初始化时扫描程序集内所有具体 CardModel，逐张报告缺图。
    /// </summary>
    public static void ValidateAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Type[] cardTypes;
        try
        {
            cardTypes = assembly
                .GetTypes()
                .Where(static type =>
                    !type.IsAbstract &&
                    typeof(CardModel).IsAssignableFrom(type) &&
                    type.Namespace?.StartsWith(
                        "GuZhenRen",
                        StringComparison.Ordinal
                    ) == true
                )
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (ReflectionTypeLoadException exception)
        {
            cardTypes = exception.Types
                .OfType<Type>()
                .Where(static type =>
                    !type.IsAbstract &&
                    typeof(CardModel).IsAssignableFrom(type) &&
                    type.Namespace?.StartsWith(
                        "GuZhenRen",
                        StringComparison.Ordinal
                    ) == true
                )
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Entry.Logger.Warn(
                "[卡图审计] 部分类型加载失败，已对成功加载的卡牌继续检查。" +
                $" LoaderExceptions={exception.LoaderExceptions?.Length ?? 0}"
            );
        }

        int missingCount = 0;
        foreach (Type cardType in cardTypes)
        {
            if (!ResourceLoader.Exists(GetResourcePath(cardType)))
            {
                missingCount++;
            }

            WarnIfMissing(cardType);
        }

        if (missingCount == 0)
        {
            Entry.Logger.Info(
                $"[卡图审计] 已检查 {cardTypes.Length} 张卡牌，所有同名图片均存在。"
            );
            return;
        }

        Entry.Logger.Warn(
            $"[卡图审计] 已检查 {cardTypes.Length} 张卡牌，" +
            $"其中 {missingCount} 张缺少同名图片。" +
            $"请将 PNG 放入：{ResourceImageDirectory}"
        );
    }

    private static void WarnIfMissing(Type cardType)
    {
        string resourcePath = GetResourcePath(cardType);
        if (ResourceLoader.Exists(resourcePath))
        {
            return;
        }

        lock (WarningLock)
        {
            if (!WarnedPaths.Add(resourcePath))
            {
                return;
            }
        }

        Entry.Logger.Warn(
            $"[卡图缺失] {cardType.FullName} 未找到同名图片。" +
            $" 资源路径：{resourcePath}"
        );
    }
}
