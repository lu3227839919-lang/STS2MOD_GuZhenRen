using System.Reflection;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Persistence;

/// <summary>
/// 为已经移除、但仍可能留在旧存档历史卡牌快照中的字段保留网络名称。
/// 不删除旧数据、不恢复旧玩法，也不向新卡牌写入无用的默认状态。
/// </summary>
internal static class LegacySavedPropertyCompatibility
{
    // 保存格式的一部分，不能随命名空间、ModId 或玩法重命名而改变。
    internal const string LegacyBoundSourcePropertyName =
        "CardModel_GuZhenRenPersonal.xue_yun_shi_shen.bound_source";

    private static readonly object RegistrationLock = new();
    private static bool _registered;

    internal static void Register()
    {
        lock (RegistrationLock)
        {
            if (_registered)
            {
                return;
            }

            // RitsuLib 0.5.18 的名称注册入口尚未公开。只在初始化时
            // 注册已知旧名称，后续的排序、网络 ID、位宽和 Hash 计算
            // 仍由 RitsuLib / 游戏的正常初始化流程统一完成。
            // 不直接修改原版缓存，也不在序列化时临时追加网络 ID。
            Type registryType = typeof(SavedAttachedState<,>).Assembly.GetType(
                "STS2RitsuLib.Utils.SavedAttachedStateRegistry",
                throwOnError: true
            )!;
            MethodInfo registerMethod = registryType.GetMethod(
                "RegisterPropertyName",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string)],
                modifiers: null
            ) ?? throw new MissingMethodException(
                registryType.FullName,
                "RegisterPropertyName(string)"
            );

            // 委托让注册过晚等错误保留原始异常，不吞掉兼容性故障。
            Action<string> registerName =
                registerMethod.CreateDelegate<Action<string>>();
            registerName(LegacyBoundSourcePropertyName);
            _registered = true;

            Entry.Logger.Info(
                "[存档兼容] 已保留旧字段网络名称：" +
                LegacyBoundSourcePropertyName
            );
        }
    }
}
