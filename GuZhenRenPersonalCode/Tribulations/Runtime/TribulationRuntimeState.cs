// ============================================================================
// 中文维护说明
// 文件职责：负责把已保存的灾劫选择恢复为战斗效果，并路由战斗事件。
// 主要类型：TribulationRuntimeState。
// 实现要点：读取旧存档后先规范化字段，再参与本次逻辑判断。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
namespace GuZhenRen.Tribulations.Runtime;

/// <summary>
/// Serializable per-combat state for the active tribulation.  All keys are
/// namespaced by the owning definition so QuickSL and reconnect resume the
/// same escalation chain instead of rebuilding it from transient fields.
/// </summary>
public sealed class TribulationRuntimeState
{
    public Dictionary<string, int> Counters { get; set; } = [];
    public Dictionary<string, bool> Flags { get; set; } = [];
    public Dictionary<string, string> Text { get; set; } = [];
    public Dictionary<string, decimal> DecimalValues { get; set; } = [];

    public int GetCounter(string key) =>
        Counters.TryGetValue(key, out int value) ? value : 0;

    public bool GetFlag(string key) =>
        Flags.TryGetValue(key, out bool value) && value;

    public string GetText(string key) =>
        Text.TryGetValue(key, out string? value) ? value : string.Empty;

    public decimal GetDecimal(string key) =>
        DecimalValues.TryGetValue(key, out decimal value) ? value : 0m;

    public void Normalize()
    {
        Counters ??= [];
        Flags ??= [];
        Text ??= [];
        DecimalValues ??= [];
    }

    public void RemovePrefix(string prefix)
    {
        foreach (string key in Counters.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                     .ToArray())
            Counters.Remove(key);
        foreach (string key in Flags.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                     .ToArray())
            Flags.Remove(key);
        foreach (string key in Text.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                     .ToArray())
            Text.Remove(key);
        foreach (string key in DecimalValues.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                     .ToArray())
            DecimalValues.Remove(key);
    }
}
