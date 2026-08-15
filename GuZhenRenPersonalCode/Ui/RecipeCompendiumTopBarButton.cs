using Godot;

using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.TopBar;

namespace GuZhenRen.Ui;

/// <summary>
/// A recipe-compendium button built from the live map-button visual tree.
/// Keeping it as an NTopBarButton preserves the game's native input,
/// focus, sound, shader, and hover animation behavior.
/// </summary>
internal sealed partial class RecipeCompendiumTopBarButton : NTopBarButton
{
    private static readonly StringName ShaderValue = new("v");

    private RecipeCompendiumOverlay _overlay = null!;

    internal static RecipeCompendiumTopBarButton Create(
        NTopBarMapButton mapButton,
        RecipeCompendiumOverlay overlay
    )
    {
        RecipeCompendiumTopBarButton button = new()
        {
            Name = "RecipeCompendiumButton",
            FocusMode = mapButton.FocusMode,
            MouseFilter = mapButton.MouseFilter,
            ProcessMode = mapButton.ProcessMode,
        };
        button._overlay = overlay;

        Control visual =
            mapButton.GetNode<Control>("Control").Duplicate()
                as Control ??
            throw new InvalidOperationException(
                "无法复制原生地图按钮的控件树。"
            );
        visual.Name = "Control";
        visual.MouseFilter = Control.MouseFilterEnum.Ignore;

        Control icon = visual.GetNode<Control>("Icon");
        if (icon.Material != null)
        {
            icon.Material = icon.Material.Duplicate() as Material;
        }

        ApplyCompendiumTexture(icon);
        button.AddChild(visual);

        // 原生地图按钮的根节点是 MarginContainer，复制出的 Control
        // 原本依赖该容器获得尺寸。自定义按钮由代码直接创建，其根节点
        // 只是普通 Control，因此必须显式铺满，否则 Icon 会保持零尺寸。
        visual.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect
        );
        visual.OffsetTop = 8f;
        visual.OffsetBottom = -8f;

        // 配方大全没有对应的地图快捷键，避免显示复制来的快捷键标记。
        visual.GetNodeOrNull<Control>("HotkeyIcon")?.Hide();

        return button;
    }

    internal void RefreshOpenState()
    {
        UpdateScreenOpen();
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        _overlay.ToggleDialog();
        UpdateScreenOpen();
        _hsv?.SetShaderParameter(ShaderValue, 0.9f);
    }

    protected override bool IsOpen()
    {
        return _overlay.IsDialogOpen;
    }

    protected override void OnFocus()
    {
        base.OnFocus();

        HoverTip hoverTip = new(
            new LocString(
                "static_hover_tips",
                "GU_ZHEN_REN_PERSONAL_RECIPE_COMPENDIUM.title"
            ),
            new LocString(
                "static_hover_tips",
                "GU_ZHEN_REN_PERSONAL_RECIPE_COMPENDIUM.description"
            )
        );
        NHoverTipSet? tipSet =
            NHoverTipSet.CreateAndShow(this, hoverTip);
        tipSet?.SetGlobalPosition(
            GlobalPosition +
            new Vector2(Size.X - tipSet.Size.X, Size.Y + 20f)
        );
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        NHoverTipSet.Remove(this);
    }

    private static void ApplyCompendiumTexture(Control icon)
    {
        Texture2D? texture =
            RecipeCompendiumOverlay.LoadCompendiumTexture();
        if (texture == null)
        {
            return;
        }

        if (!TryApplyTexture(icon, texture))
        {
            Entry.Logger.Warn(
                "未在原生地图按钮 Icon 节点中找到可替换纹理的子节点；" +
                "已保留地图按钮原图作为兜底。"
            );
        }
    }

    private static bool TryApplyTexture(
        Node node,
        Texture2D texture
    )
    {
        switch (node)
        {
            case TextureRect textureRect:
                textureRect.Texture = texture;
                return true;
            case TextureButton textureButton:
                textureButton.TextureNormal = texture;
                textureButton.TexturePressed = texture;
                textureButton.TextureHover = texture;
                textureButton.TextureFocused = texture;
                return true;
            case NinePatchRect ninePatchRect:
                ninePatchRect.Texture = texture;
                return true;
            case Sprite2D sprite:
                sprite.Texture = texture;
                return true;
        }

        foreach (Node child in node.GetChildren())
        {
            if (TryApplyTexture(child, texture))
            {
                return true;
            }
        }

        return false;
    }
}
