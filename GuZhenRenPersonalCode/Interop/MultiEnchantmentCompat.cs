using System.Reflection;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace GuZhenRen.Interop;

/// <summary>
/// MultiEnchantmentMod 公共 API 的窄反射桥。
///
/// 运行时依赖由 manifest 保证；这里不建立编译期 DLL 引用，避免把前置
/// 复制进本模组发布目录。寄生写入使用 UntilCombatEnds 作用域，避免
/// 战斗中的寄生被同步进永久牌组；读取、移除和属性刷新同样只调用
/// 前置承诺兼容的公开 API。
/// </summary>
internal static class MultiEnchantmentCompat
{
    private const string AssemblyName = "MultiEnchantmentMod";
    private const string ApiTypeName =
        "MultiEnchantmentMod.Api.MultiEnchantmentApi";
    private const string ScopeTypeName =
        "MultiEnchantmentMod.Api.EnchantmentScope";
    private const int MinimumApiVersion = 2;

    private static readonly object Sync = new();

    private static Func<CardModel, IReadOnlyList<EnchantmentModel>>?
        _getEnchantments;
    private static MethodInfo? _enchant;
    private static MethodInfo? _removeEnchantment;
    private static MethodInfo? _notifyPropsChanged;
    private static MethodInfo? _scanAssembly;
    private static object? _untilCombatEndsScope;
    private static bool _assemblyScanned;

    internal static void EnsureAvailable()
    {
        ResolveRequiredMembers();

        lock (Sync)
        {
            if (_assemblyScanned)
            {
                return;
            }

            Invoke(
                _scanAssembly!,
                [typeof(MultiEnchantmentCompat).Assembly]
            );
            _assemblyScanned = true;
        }
    }

    internal static IReadOnlyList<EnchantmentModel> GetEnchantments(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ResolveRequiredMembers();
        return _getEnchantments!(card);
    }

    internal static TEnchantment? GetEnchantment<TEnchantment>(
        CardModel? card
    )
        where TEnchantment : EnchantmentModel
    {
        return card == null
            ? null
            : GetEnchantments(card).OfType<TEnchantment>().FirstOrDefault();
    }

    internal static EnchantmentModel? Enchant(
        CardModel card,
        EnchantmentModel enchantment,
        decimal amount,
        bool untilCombatEnds
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(enchantment);
        ResolveRequiredMembers();

        return Invoke(
            _enchant!,
            [
                card,
                enchantment,
                amount,
                untilCombatEnds ? _untilCombatEndsScope : null,
            ]
        ) as EnchantmentModel;
    }

    internal static bool RemoveEnchantment(
        CardModel card,
        EnchantmentModel enchantment
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(enchantment);
        ResolveRequiredMembers();

        ParameterInfo reasonParameter =
            _removeEnchantment!.GetParameters()[2];
        object reason = reasonParameter.HasDefaultValue &&
            reasonParameter.DefaultValue is not null
                ? reasonParameter.DefaultValue
                : Activator.CreateInstance(reasonParameter.ParameterType)!;

        return Invoke(
            _removeEnchantment,
            [card, enchantment, reason]
        ) is true;
    }

    internal static void NotifyPropsChanged(
        EnchantmentModel enchantment
    )
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        ResolveRequiredMembers();
        Invoke(_notifyPropsChanged!, [enchantment]);
    }

    private static void ResolveRequiredMembers()
    {
        if (_getEnchantments != null &&
            _enchant != null &&
            _removeEnchantment != null &&
            _notifyPropsChanged != null &&
            _scanAssembly != null &&
            _untilCombatEndsScope != null)
        {
            return;
        }

        lock (Sync)
        {
            if (_getEnchantments != null &&
                _enchant != null &&
                _removeEnchantment != null &&
                _notifyPropsChanged != null &&
                _scanAssembly != null &&
                _untilCombatEndsScope != null)
            {
                return;
            }

            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(static candidate =>
                    string.Equals(
                        candidate.GetName().Name,
                        AssemblyName,
                        StringComparison.Ordinal
                    ));
            Type? apiType = assembly?.GetType(ApiTypeName);
            Type? scopeType = assembly?.GetType(ScopeTypeName);
            if (apiType == null || scopeType == null)
            {
                throw new InvalidOperationException(
                    "GuZhenRen requires MultiEnchantmentMod v2.5.2 or newer."
                );
            }

            int currentVersion = apiType.GetProperty(
                    "CurrentVersion",
                    BindingFlags.Static | BindingFlags.Public
                )?.GetValue(null) as int? ?? 0;
            if (currentVersion < MinimumApiVersion)
            {
                throw new InvalidOperationException(
                    $"MultiEnchantmentMod API {currentVersion} is too old; " +
                    $"API {MinimumApiVersion} or newer is required."
                );
            }

            MethodInfo[] methods = apiType.GetMethods(
                BindingFlags.Static | BindingFlags.Public
            );
            MethodInfo? enchant = methods.FirstOrDefault(
                method =>
                    method.Name == "Enchant" &&
                    !method.IsGenericMethodDefinition &&
                    method.GetParameters() is { Length: 4 } parameters &&
                    parameters[0].ParameterType == typeof(CardModel) &&
                    parameters[1].ParameterType ==
                        typeof(EnchantmentModel) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == scopeType
            );
            MethodInfo? getEnchantments = methods.FirstOrDefault(
                static method =>
                    method.Name == "GetEnchantments" &&
                    !method.IsGenericMethodDefinition &&
                    method.GetParameters() is { Length: 1 } parameters &&
                    parameters[0].ParameterType == typeof(CardModel)
            );
            MethodInfo? removeEnchantment = methods.FirstOrDefault(
                static method =>
                    method.Name == "RemoveEnchantment" &&
                    method.GetParameters() is { Length: 3 } parameters &&
                    parameters[0].ParameterType == typeof(CardModel) &&
                    parameters[1].ParameterType ==
                        typeof(EnchantmentModel)
            );
            MethodInfo? notifyPropsChanged = methods.FirstOrDefault(
                static method =>
                    method.Name == "NotifyPropsChanged" &&
                    method.GetParameters() is { Length: 1 } parameters &&
                    parameters[0].ParameterType ==
                        typeof(EnchantmentModel)
            );
            MethodInfo? scanAssembly = methods.FirstOrDefault(
                static method =>
                    method.Name == "ScanAssembly" &&
                    method.GetParameters() is { Length: 1 } parameters &&
                    parameters[0].ParameterType == typeof(Assembly)
            );
            object? untilCombatEndsScope = scopeType.GetProperty(
                    "UntilCombatEnds",
                    BindingFlags.Static | BindingFlags.Public
                )?.GetValue(null);

            if (enchant == null ||
                getEnchantments == null ||
                removeEnchantment == null ||
                notifyPropsChanged == null ||
                scanAssembly == null ||
                untilCombatEndsScope == null)
            {
                throw new MissingMethodException(
                    "MultiEnchantmentMod public API is missing a required " +
                    "multi-slot method. Update the dependency to v2.5.2 or newer."
                );
            }

            _getEnchantments =
                (Func<CardModel, IReadOnlyList<EnchantmentModel>>)
                Delegate.CreateDelegate(
                    typeof(Func<CardModel, IReadOnlyList<EnchantmentModel>>),
                    getEnchantments
                );
            _enchant = enchant;
            _removeEnchantment = removeEnchantment;
            _notifyPropsChanged = notifyPropsChanged;
            _scanAssembly = scanAssembly;
            _untilCombatEndsScope = untilCombatEndsScope;
        }
    }

    private static object? Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                $"MultiEnchantmentMod API call {method.Name} failed.",
                exception.InnerException ?? exception
            );
        }
    }
}
