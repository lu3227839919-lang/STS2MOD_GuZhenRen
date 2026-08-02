using Godot;

using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

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

    // 原版储君会把能量容器移到此处，为左侧的第二表盘留出空间。
    private static readonly Vector2 RegentEnergyContainerPosition =
        new(100f, 806f);

    private static readonly Vector2 FallbackLocalPosition =
        new(-132f, -16f);

    private SecondaryResourceDefinition _definition = null!;
    private string _scenePath = string.Empty;
    private MegaLabel _amountLabel = null!;
    private Control? _layers;
    private Control? _rotationLayers;
    private NParticlesContainer? _backVfx;
    private NParticlesContainer? _frontVfx;
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
            ZIndex = 1,
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

        _amountLabel.SetTextAutoSize(
            _maxAmount.HasValue
                ? $"{_amount}/{_maxAmount.Value}"
                : _amount.ToString()
        );
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
            _backVfx?.Restart();
            _frontVfx?.Restart();
        }

        Visible = _definition.IsVisibleInCombatUi(player);
    }

    /// <summary>
    /// NCombatUi.Activate 的后置刷新发生在原生能量表创建之后。
    /// 此时按储君的布局移动容器，并把元气表挂到原生能量表下，使二者
    /// 共用进出场动画。
    /// </summary>
    public void AttachBesideNativeEnergyCounter(NCombatUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        ui.EnergyCounterContainer.SetPosition(
            RegentEnergyContainerPosition,
            keepOffsets: true
        );

        NEnergyCounter? nativeEnergyCounter =
            ui.EnergyCounterContainer
                .GetChildren()
                .OfType<NEnergyCounter>()
                .FirstOrDefault();

        if (nativeEnergyCounter == null ||
            ReferenceEquals(GetParent(), nativeEnergyCounter))
        {
            return;
        }

        Reparent(nativeEnergyCounter, keepGlobalTransform: false);
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

            Position = source.Position == Vector2.Zero
                ? FallbackLocalPosition
                : source.Position;
            Scale = source.Scale;
            PivotOffset = source.PivotOffset;
            CustomMinimumSize =
                source.CustomMinimumSize == Vector2.Zero
                    ? FallbackSize
                    : source.CustomMinimumSize;
            Size = source.Size == Vector2.Zero
                ? FallbackSize
                : source.Size;

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
            GetNodeOrNull<MegaLabel>("Label") ??
            FindChild(
                "Label",
                recursive: true,
                owned: false
            ) as MegaLabel ??
            new MegaLabel();

        if (_amountLabel.GetParent() != null)
        {
            return;
        }

        if (!loadedScene)
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
        _amountLabel.CustomMinimumSize = Size;
        _amountLabel.Size = Size;
        _amountLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        _amountLabel.VerticalAlignment =
            VerticalAlignment.Center;
        _amountLabel.AutoSizeEnabled = true;
        _amountLabel.MinFontSize = 24;
        _amountLabel.MaxFontSize = 44;
        _amountLabel.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            9
        );
        AddChild(_amountLabel);
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
        ) as NParticlesContainer;
        _frontVfx = FindChild(
            "EnergyVfxFront",
            recursive: true,
            owned: false
        ) as NParticlesContainer;
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
