namespace GuZhenRen.Interop;

/// <summary>
/// 兼容旧工程中可能存在的初始化调用。
///
/// 当前 Mod 只使用游戏原生的 MinionPower，没有调用 MinionLib API，
/// 因此不应为这个无引用包装器保留额外程序集依赖。
/// </summary>
internal static class MinionLibSupport
{
    public static string RuntimeVersion =>
        "not-used";

    public static void Initialize()
    {
        // 有意留空：当前玩法不依赖 MinionLib。
    }
}
