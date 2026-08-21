// ============================================================================
// 中文维护说明
// 文件职责：负责把已保存的灾劫选择恢复为战斗效果，并路由战斗事件。
// 主要类型：TribulationStateStore。
// 实现要点：读取旧存档后先规范化字段，再参与本次逻辑判断。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Tribulations.Runtime;

public static class TribulationStateStore
{
    public static string Key(string tribulationId, string localKey) =>
        $"{tribulationId}.{localKey}";

    public static int GetCounter(TribulationContext context, string key) =>
        Current(context).GetCounter(key);

    public static bool GetFlag(TribulationContext context, string key) =>
        Current(context).GetFlag(key);

    public static string GetText(TribulationContext context, string key) =>
        Current(context).GetText(key);

    public static decimal GetDecimal(TribulationContext context, string key) =>
        Current(context).GetDecimal(key);

    public static void SetCounter(TribulationContext context, string key, int value) =>
        Modify(context, state => state.Counters[key] = value);

    public static int AddCounter(
        TribulationContext context,
        string key,
        int delta,
        int minimum = int.MinValue,
        int maximum = int.MaxValue)
    {
        int value = Math.Clamp(GetCounter(context, key) + delta, minimum, maximum);
        SetCounter(context, key, value);
        return value;
    }

    public static void SetFlag(TribulationContext context, string key, bool value) =>
        Modify(context, state => state.Flags[key] = value);

    public static void SetText(TribulationContext context, string key, string value) =>
        Modify(context, state => state.Text[key] = value ?? string.Empty);

    public static void SetDecimal(
        TribulationContext context,
        string key,
        decimal value) =>
        Modify(context, state => state.DecimalValues[key] = value);

    public static void RemovePrefix(TribulationContext context, string prefix) =>
        Modify(context, state => state.RemovePrefix(prefix));

    public static int ReadLeaderCounter(Creature leader, string key)
    {
        if (leader.CombatState == null)
            return 0;

        foreach (var player in leader.CombatState.Players)
        {
            ApertureRunData data = ApertureSystem.GetState(player);
            if (data.ActiveLeaderCombatId == leader.CombatId)
                return data.TribulationState.GetCounter(key);
        }

        return 0;
    }

    private static TribulationRuntimeState Current(TribulationContext context)
    {
        TribulationRuntimeState state =
            ApertureSystem.GetState(context.Player).TribulationState;
        state.Normalize();
        return state;
    }

    private static void Modify(
        TribulationContext context,
        Action<TribulationRuntimeState> modifier)
    {
        ApertureSystem.ModifyTribulationData(context.Player, data =>
        {
            data.TribulationState.Normalize();
            modifier(data.TribulationState);
        });
    }
}
