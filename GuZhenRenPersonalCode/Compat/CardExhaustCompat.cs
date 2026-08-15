using System.Reflection;
using System.Runtime.ExceptionServices;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 跨游戏 API 版本调用 CardCmd.Exhaust。
///
/// 0.110.1 及后续版本修改了 CardCmd.Exhaust 的参数签名和返回类型。
/// 因此不能在编译期硬绑定某一个重载。此适配器按参数类型查找当前游戏
/// 实际提供的方法，并为新增的可选参数提供默认值。
/// </summary>
internal static class CardExhaustCompat
{
    private static readonly object ResolveLock = new();
    private static MethodInfo? _resolvedMethod;

    internal static Task ExhaustAsync(
        PlayerChoiceContext choiceContext,
        CardModel card
    )
    {
        MethodInfo method = GetMethod();
        object?[] arguments = BuildArguments(
            method,
            choiceContext,
            card
        );

        object? result;
        try
        {
            result = method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();
            throw;
        }

        return result as Task
            ?? throw new InvalidOperationException(
                $"{FormatSignature(method)} did not return Task."
            );
    }

    /// <summary>
    /// 返回当前运行时所有能够安全挂载 Harmony Postfix 的 Exhaust 重载。
    /// 只选择返回值为 Task 或 Task&lt;T&gt; 且含 CardModel 参数的方法。
    /// </summary>
    internal static IReadOnlyList<MethodInfo>
        FindPatchableMethods()
    {
        return typeof(CardCmd)
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static
            )
            .Where(method =>
                method.Name == nameof(CardCmd.Exhaust) &&
                IsTaskReturnType(method.ReturnType) &&
                method
                    .GetParameters()
                    .Any(parameter =>
                        typeof(CardModel).IsAssignableFrom(
                            parameter.ParameterType
                        )
                    )
            )
            .OrderByDescending(ScoreMethod)
            .ToArray();
    }

    internal static bool TryReadArguments(
        object[] arguments,
        out PlayerChoiceContext choiceContext,
        out CardModel? card
    )
    {
        card = arguments
            .OfType<CardModel>()
            .FirstOrDefault();

        choiceContext = arguments
            .OfType<PlayerChoiceContext>()
            .FirstOrDefault()
            ?? new BlockingPlayerChoiceContext();

        return card != null;
    }

    internal static string FormatSignature(MethodInfo method)
    {
        string parameters = string.Join(
            ", ",
            method
                .GetParameters()
                .Select(parameter =>
                    $"{parameter.ParameterType.Name} {parameter.Name}"
                )
        );

        return $"{method.DeclaringType?.FullName}.{method.Name}" +
               $"({parameters})";
    }

    private static MethodInfo GetMethod()
    {
        if (_resolvedMethod != null)
        {
            return _resolvedMethod;
        }

        lock (ResolveLock)
        {
            _resolvedMethod ??= typeof(CardCmd)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
                .Where(method =>
                    method.Name == nameof(CardCmd.Exhaust) &&
                    IsTaskReturnType(method.ReturnType) &&
                    CanBuildArguments(method)
                )
                .OrderByDescending(ScoreMethod)
                .FirstOrDefault();

            return _resolvedMethod
                ?? throw new MissingMethodException(
                    "No compatible CardCmd.Exhaust overload was found. " +
                    "Available overloads: " +
                    string.Join(
                        " | ",
                        typeof(CardCmd)
                            .GetMethods(
                                BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.Static
                            )
                            .Where(method =>
                                method.Name ==
                                nameof(CardCmd.Exhaust)
                            )
                            .Select(FormatSignature)
                    )
                );
        }
    }

    private static bool CanBuildArguments(MethodInfo method)
    {
        bool hasCard = false;

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            Type type = parameter.ParameterType;

            if (typeof(CardModel).IsAssignableFrom(type))
            {
                hasCard = true;
                continue;
            }

            if (type == typeof(PlayerChoiceContext) ||
                type == typeof(CancellationToken) ||
                type == typeof(bool) ||
                parameter.HasDefaultValue)
            {
                continue;
            }

            return false;
        }

        return hasCard;
    }

    private static bool IsTaskReturnType(Type returnType) =>
        typeof(Task).IsAssignableFrom(returnType);

    private static int ScoreMethod(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        int score = 100 - parameters.Length;

        if (parameters.Any(parameter =>
                parameter.ParameterType ==
                typeof(PlayerChoiceContext)))
        {
            score += 20;
        }

        if (parameters.Any(parameter =>
                parameter.ParameterType ==
                typeof(CardModel)))
        {
            score += 20;
        }

        score += parameters.Count(parameter =>
            parameter.HasDefaultValue
        );

        return score;
    }

    private static object?[] BuildArguments(
        MethodInfo method,
        PlayerChoiceContext choiceContext,
        CardModel card
    )
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] arguments = new object?[parameters.Length];

        for (int index = 0; index < parameters.Length; index++)
        {
            ParameterInfo parameter = parameters[index];
            Type type = parameter.ParameterType;

            if (typeof(CardModel).IsAssignableFrom(type))
            {
                arguments[index] = card;
                continue;
            }

            if (type == typeof(PlayerChoiceContext))
            {
                arguments[index] = choiceContext;
                continue;
            }

            if (type == typeof(CancellationToken))
            {
                arguments[index] = CancellationToken.None;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }

            if (type == typeof(bool))
            {
                arguments[index] = false;
                continue;
            }

            throw new MissingMethodException(
                $"Cannot supply parameter '{parameter.Name}' " +
                $"for {FormatSignature(method)}."
            );
        }

        return arguments;
    }
}
