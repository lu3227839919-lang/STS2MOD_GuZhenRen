using System.Reflection;

using Godot;
using HarmonyLib;

using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Nodes.Combat;

namespace GuZhenRen.Patches;

/// <summary>
/// 万我临时生命血条兼容层。
///
/// RitsuLib 0.5.13 的 IHealthBarVisualGraftSource 只在
/// CurrentHp + GraftHp 超过 MaxHp 时实际扩展并绘制血条；
/// 当临时生命仍落在原最大生命范围内时，本补丁负责把临时生命
/// 绘制在当前红色生命右侧。
///
/// 一旦 CurrentHp + TempHp > MaxHp，本补丁隐藏自己的条段，
/// 继续由 RitsuLib Visual Graft 负责扩展血条，避免重复绘制。
/// </summary>
internal static class WoLiTempHpHealthBarPatch
{
    private const string HarmonyId =
        Entry.ModId + ".WoLiTempHpHealthBar";

    private const string StripNodeName =
        "GuZhenRenWoLiTempHpStrip";

    private static readonly Color TempHpColor =
        new("FFB52E");

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo refreshForeground =
            AccessTools.DeclaredMethod(
                typeof(NHealthBar),
                "RefreshForeground"
            ) ??
            throw new MissingMethodException(
                typeof(NHealthBar).FullName,
                "RefreshForeground"
            );

        Harmony harmony = new(HarmonyId);

        try
        {
            HarmonyMethod postfix = new(
                typeof(WoLiTempHpHealthBarPatch),
                nameof(Postfix)
            )
            {
                priority = Priority.Last,
            };

            harmony.Patch(
                refreshForeground,
                postfix: postfix
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            throw;
        }
    }

    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    private static void Postfix(NHealthBar __instance)
    {
        try
        {
            RefreshTempHpStrip(__instance);
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                "[WoLiTempHpHealthBar] 刷新临时生命条失败：" +
                exception
            );
        }
    }

    private static void RefreshTempHpStrip(
        NHealthBar healthBar
    )
    {
        if (healthBar._creature is not { } creature ||
            creature.CurrentHp <= 0 ||
            creature.MaxHp <= 0)
        {
            HideStrip(healthBar);
            return;
        }

        WoLiTempHpPower? tempHpPower =
            creature.GetPower<WoLiTempHpPower>();

        int tempHp = tempHpPower?.TempHp ?? 0;
        if (tempHp <= 0)
        {
            HideStrip(healthBar);
            return;
        }

        // 超出最大生命的情况由 RitsuLib Visual Graft 负责。
        // 这里仅处理“当前生命右侧、最大生命以内”的临时生命段。
        long visualHp =
            (long)creature.CurrentHp + tempHp;

        if (visualHp > creature.MaxHp)
        {
            HideStrip(healthBar);
            return;
        }

        if (!EnsureStrip(
                healthBar,
                out NinePatchRect strip))
        {
            return;
        }

        float maxWidth =
            healthBar._expectedMaxFgWidth > 0f
                ? healthBar._expectedMaxFgWidth
                : healthBar._hpForegroundContainer.Size.X;

        if (maxWidth <= 0f)
        {
            strip.Visible = false;
            return;
        }

        float currentWidth =
            (float)creature.CurrentHp /
            creature.MaxHp *
            maxWidth;

        float tempWidth =
            (float)tempHp /
            creature.MaxHp *
            maxWidth;

        if (tempWidth < 0.5f)
        {
            strip.Visible = false;
            return;
        }

        strip.Visible = true;
        strip.Material = null;
        strip.Modulate = Colors.White;
        strip.SelfModulate = TempHpColor;

        // 与 RitsuLib graft 的 NinePatch 布局保持一致：
        // 从当前生命边缘开始，到 CurrentHp + TempHp 的位置结束。
        strip.OffsetLeft =
            currentWidth > 0f
                ? Math.Max(
                    0f,
                    currentWidth - strip.PatchMarginLeft
                )
                : 0f;

        strip.OffsetRight =
            currentWidth +
            tempWidth -
            maxWidth;
    }

    private static bool EnsureStrip(
        NHealthBar healthBar,
        out NinePatchRect strip
    )
    {
        strip = null!;

        if (healthBar._poisonForeground is not
                NinePatchRect template ||
            template.GetParent() is not Control mask ||
            healthBar._hpForeground is not { }
                hpForeground)
        {
            return false;
        }

        if (mask.GetNodeOrNull<NinePatchRect>(
                StripNodeName) is { } existing)
        {
            strip = existing;
            return true;
        }

        strip = (NinePatchRect)template.Duplicate();
        strip.Name = StripNodeName;
        strip.Visible = false;
        strip.Modulate = Colors.White;
        strip.SelfModulate = Colors.White;
        strip.Material = null;
        strip.ZIndex = 0;
        strip.OffsetLeft = 0f;
        strip.OffsetRight = 0f;
        strip.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        mask.AddChild(strip);

        int insertAt = Math.Clamp(
            hpForeground.GetIndex() + 1,
            0,
            mask.GetChildCount() - 1
        );
        mask.MoveChild(strip, insertAt);

        return true;
    }

    private static void HideStrip(
        NHealthBar healthBar
    )
    {
        if (healthBar._poisonForeground?.GetParent()
                is not Control mask ||
            mask.GetNodeOrNull<NinePatchRect>(
                StripNodeName) is not { } strip)
        {
            return;
        }

        strip.Visible = false;
        strip.Material = null;
        strip.SelfModulate = Colors.White;
    }
}
