namespace GuZhenRen.Aperture;

/// <summary>
/// 兼容旧版工程中残留的奖励补丁入口。
/// 当前版本暂不移植灾劫系统，因此不修改卡牌奖励数量。
/// </summary>
internal static class ApertureRewardPatch
{
    internal static void Initialize()
    {
        // 当前无灾劫版本不安装卡牌奖励补丁。
    }

    internal static void Uninitialize()
    {
        // 未安装 Harmony 补丁，无需回滚。
    }
}
