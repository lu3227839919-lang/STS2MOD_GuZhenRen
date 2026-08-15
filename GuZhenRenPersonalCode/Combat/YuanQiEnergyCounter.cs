using Godot;

using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRen.Combat;

/// <summary>
/// 使用角色专属次能量场景显示元气。
///
/// 场景根节点不会直接进入树，因此即便它沿用了原版 NEnergyCounter
/// 脚本，也不会用未初始化的 Player 订阅原生能量事件。这里只取出场景
/// 的视觉子树，并由次级资源系统驱动数值。
/// </summary>
public sealed partial class YuanQiEnergyCounter : Control
{
    private const string DarkMaterialPath =
        "res://materials/ui/energy_orb_dark.tres";

    private static readonly Vector2 FallbackSize =
        new(180f, 180f);

    // 元气表以原生能量表为坐标原点，放在其右上方。正向 X 偏移能
    // 避免左下角界面越过屏幕边界，负向 Y 偏移则保留斜向层叠效果。
    private static readonly Vector2 SecondaryCounterOffset =
        new(96f, -102f);

    private static readonly Vector2 FallbackLocalPosition =
        SecondaryCounterOffset;

    private SecondaryResourceDefinition _definition = null!;
    private string _scenePath = string.Empty;
    private Label _amountLabel = null!;
    private Control? _layers;
    private Control? _rotationLayers;
    private Node? _backVfx;
    private Node? _frontVfx;
    private Player? _player;
    private int _amount;
    private int? _maxAmount;

    public static YuanQiEnergyCounter Create(
        SecondaryResourceDefinition definition,
        string scenePath
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);

        YuanQiEnergyCounter counter = new()
        {
            Name = "YuanQiEnergyCounter",
            _definition = definition,
            _scenePath = scenePath,
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            // 保持默认绘制层级，让 NGame 的 HoverTipsContainer 按
            // 场景树顺序绘制在元气表上方，避免能量球遮挡牌堆说明。
            ZIndex = 0,
        };

        bool loadedScene = counter.TryAdoptSceneVisuals();
        counter.EnsureAmountLabel(loadedScene);
        counter.ResolveVisualNodes();
        counter.DisableChildMouseInput(counter);

        SecondaryResourceHoverTipBinder.Bind(
            counter,
            definition,
            () => counter._amount,
            () => counter._maxAmount
        );

        return counter;
    }

    public void Bind(Player? player)
    {
        _player = player;
        Refresh(player);
    }

    public void Refresh(Player? player)
    {
        if (player == null)
        {
            Visible = false;
            return;
        }

        int oldAmount = _amount;
        _player = player;
        _amount = SecondaryResourceCmd.Get(player, _definition.Id);
        _maxAmount =
            SecondaryResourceCmd.GetMax(player, _definition.Id);

        _amountLabel.Text = _maxAmount.HasValue
            ? $"{_amount}/{_maxAmount.Value}"
            : _amount.ToString();
        _amountLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            _amount <= 0 ? StsColors.red : StsColors.cream
        );
        _amountLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            _amount <= 0
                ? StsColors.unplayableEnergyCostOutline
                : new Color(0.08f, 0.18f, 0.24f)
        );

        ApplyZeroAmountVisualState();

        if (_amount > oldAmount)
        {
            RestartParticlesRecursive(_backVfx);
            RestartParticlesRecursive(_frontVfx);
        }

        Visible = _definition.IsVisibleInCombatUi(player);
    }

    /// <summary>
    /// NCombatUi.Activate 的后置刷新发生在原生能量表创建之后。
    /// 此时把元气表挂到原生能量表下，使二者共用进出场动画；原生能量
    /// 容器保持游戏自己的自适应位置，避免固定屏幕坐标在不同分辨率下
    /// 把元气表推到视口之外。
    /// </summary>
    public void AttachBesideNativeEnergyCounter(NCombatUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        NEnergyCounter? nativeEnergyCounter =
            ui.EnergyCounterContainer
                .GetChildren()
                .OfType<NEnergyCounter>()
                .FirstOrDefault();

        Node anchor = nativeEnergyCounter ??
            ui.EnergyCounterContainer;

        if (!ReferenceEquals(GetParent(), anchor))
        {
            Reparent(anchor, keepGlobalTransform: false);
        }

        Position = SecondaryCounterOffset;
    }

    public override void _Process(double delta)
    {
        if (_rotationLayers == null)
        {
            return;
        }

        float speed = _amount <= 0 ? 5f : 30f;

        for (int index = 0;
             index < _rotationLayers.GetChildCount();
             index++)
        {
            _rotationLayers
                .GetChild<Control>(index)
                .RotationDegrees +=
                (float)delta * speed * (index + 1);
        }
    }

    private bool TryAdoptSceneVisuals()
    {
        if (!ResourceLoader.Exists(_scenePath))
        {
            return false;
        }

        try
        {
            PackedScene? packedScene =
                ResourceLoader.Load<PackedScene>(_scenePath);
            Node? sceneRoot = packedScene?.Instantiate();

            if (sceneRoot is not Control source)
            {
                sceneRoot?.Free();
                return false;
            }

            Vector2 sourceSize = source.Size == Vector2.Zero
                ? FallbackSize
                : source.Size;

            // 场景文件里的旧绝对坐标会在窄屏或缩放界面中越界。
            // 根节点统一使用相对原生能量表的偏移；视觉子树仍保持原样。
            Position = SecondaryCounterOffset;
            Scale = source.Scale;
            PivotOffset = source.PivotOffset;
            CustomMinimumSize =
                source.CustomMinimumSize == Vector2.Zero
                    ? sourceSize
                    : source.CustomMinimumSize;
            Size = sourceSize;

            foreach (Node child in source.GetChildren().ToArray())
            {
                source.RemoveChild(child);
                AddChild(child);
            }

            source.Free();
            return true;
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                "元气表场景加载失败，改用程序化回退界面：" +
                exception.Message
            );
            return false;
        }
    }

    private void EnsureAmountLabel(bool loadedScene)
    {
        _amountLabel =
            GetNodeOrNull<Label>("Label") ??
            FindChild(
                "Label",
                recursive: true,
                owned: false
            ) as Label ??
            new Label();

        bool needsParent = _amountLabel.GetParent() == null;

        if (!loadedScene && needsParent)
        {
            Position = FallbackLocalPosition;
            CustomMinimumSize = FallbackSize;
            Size = FallbackSize;

            if (ResourceLoader.Exists(
                    YuanQiSystem.LargeIconPath
                ))
            {
                AddChild(
                    new TextureRect
                    {
                        Name = "Energy2Big",
                        Texture = ResourceLoader.Load<Texture2D>(
                            YuanQiSystem.LargeIconPath
                        ),
                        ExpandMode =
                            TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode =
                            TextureRect.StretchModeEnum
                                .KeepAspectCentered,
                        MouseFilter = MouseFilterEnum.Ignore,
                        Size = FallbackSize,
                    }
                );
            }
        }

        _amountLabel.Name = "Label";
        _amountLabel.MouseFilter = MouseFilterEnum.Ignore;
        _amountLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        _amountLabel.VerticalAlignment =
            VerticalAlignment.Center;
        _amountLabel.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            9
        );

        if (needsParent)
        {
            _amountLabel.CustomMinimumSize = Size;
            _amountLabel.Size = Size;
            _amountLabel.AddThemeFontSizeOverride(
                ThemeConstants.Label.FontSize,
                36
            );
            AddChild(_amountLabel);
        }
    }

    private void ResolveVisualNodes()
    {
        _layers = FindChild(
            "Layers",
            recursive: true,
            owned: false
        ) as Control;
        _rotationLayers = FindChild(
            "RotationLayers",
            recursive: true,
            owned: false
        ) as Control;
        _backVfx = FindChild(
            "EnergyVfxBack",
            recursive: true,
            owned: false
        );
        _frontVfx = FindChild(
            "EnergyVfxFront",
            recursive: true,
            owned: false
        );
    }

    private void ApplyZeroAmountVisualState()
    {
        Material? darkMaterial =
            _amount <= 0 && ResourceLoader.Exists(DarkMaterialPath)
                ? ResourceLoader.Load<Material>(DarkMaterialPath)
                : null;

        if (_layers != null)
        {
            foreach (Control layer in
                     _layers.GetChildren().OfType<Control>())
            {
                layer.Material = darkMaterial;
            }

            _layers.Modulate =
                _amount <= 0 ? Colors.DarkGray : Colors.White;
        }

        if (_amount <= 0)
        {
            StopParticlesRecursive(_backVfx);
            StopParticlesRecursive(_frontVfx);
        }

        if (_rotationLayers == null)
        {
            return;
        }

        foreach (Control layer in
                 _rotationLayers.GetChildren().OfType<Control>())
        {
            layer.Material = darkMaterial;
        }
    }

    private static void RestartParticlesRecursive(Node? root)
    {
        if (root == null)
        {
            return;
        }

        if (root is CpuParticles2D cpuParticles)
        {
            cpuParticles.Visible = true;
            cpuParticles.Restart();
            cpuParticles.Emitting = true;
        }
        else if (root is GpuParticles2D gpuParticles)
        {
            gpuParticles.Visible = true;
            gpuParticles.Restart();
            gpuParticles.Emitting = true;
        }

        foreach (Node child in root.GetChildren())
        {
            RestartParticlesRecursive(child);
        }
    }

    private static void StopParticlesRecursive(Node? root)
    {
        if (root == null)
        {
            return;
        }

        if (root is CpuParticles2D cpuParticles)
        {
            cpuParticles.Emitting = false;
            cpuParticles.Visible = false;
        }
        else if (root is GpuParticles2D gpuParticles)
        {
            gpuParticles.Emitting = false;
            gpuParticles.Visible = false;
        }

        foreach (Node child in root.GetChildren())
        {
            StopParticlesRecursive(child);
        }
    }

    private void DisableChildMouseInput(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Control control)
            {
                control.MouseFilter = MouseFilterEnum.Ignore;
            }

            DisableChildMouseInput(child);
        }
    }
}
