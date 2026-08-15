using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Aperture;

/// <summary>
/// 空窍/仙窍与后续可选内容之间的扩展桥。
/// 当前不包含灾劫与十转。
/// </summary>
public static class ApertureContentBridge
{
    /// <summary>
    /// 空窍/仙窍转数变化后的扩展通知。
    /// </summary>
    public static Action<Player, int, int>? RankAdvanced { get; set; }

    /// <summary>
    /// 进入九转时请求播放主题音乐。
    /// 第二个参数表示牌组中是否存在 ShaGu。
    /// </summary>
    public static Action<Player, bool>? PlayRankNineTheme { get; set; }
}
