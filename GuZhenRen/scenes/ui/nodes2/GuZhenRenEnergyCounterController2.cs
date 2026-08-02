#nullable enable

using Godot;

/// <summary>
/// 蛊真人能量表盘控制器。
/// 挂载到 GuZhenRenEnergyCounter 根 Control 节点。
///
/// 当前节点结构：
/// GuZhenRenEnergyCounter
/// ├── EnergyVfxBack          （可选）
/// ├── Layers
/// │   ├── border
/// │   └── RotationLayers
/// │       └── layer1
/// ├── EnergyVfxFront         （可选）
/// ├── BurstBack              （可选）
/// ├── BurstFront             （可选）
/// └── Label                  （可选）
///
/// 脚本不会修改 layer1 的 SelfModulate、Modulate、亮度或透明度。
/// </summary>
public partial class GuZhenRenEnergyCounterController2 : Control
{
	[ExportGroup("能量设置")]

	[Export(PropertyHint.Range, "1,99,1")]
	public int MaxEnergy { get; set; } = 5;

	[Export(PropertyHint.Range, "0,99,1")]
	public int StartingEnergy { get; set; } = 4;


	[ExportGroup("旋转设置")]

	[Export]
	public bool RotationEnabled { get; set; } = true;

	[Export]
	public float RotationSpeedDegrees { get; set; } = 45.0f;

	/// <summary>
	/// 漩涡中心在 layer1 原始纹理中的像素坐标。
	/// 代码会自动考虑 TextureRect 的居中留白、缩放和枢轴。
	/// </summary>
	[Export]
	public Vector2 RotationPivot { get; set; } =
		new Vector2(128.0f, 128.0f);

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float EmptyRotationMultiplier { get; set; } = 0.25f;


	[ExportGroup("回弹动画")]

	[Export(PropertyHint.Range, "1.0,1.3,0.01")]
	public float PulseScale { get; set; } = 1.08f;

	[Export(PropertyHint.Range, "0.01,1.0,0.01")]
	public float PulseExpandDuration { get; set; } = 0.08f;

	[Export(PropertyHint.Range, "0.01,1.0,0.01")]
	public float PulseReturnDuration { get; set; } = 0.12f;


	[ExportGroup("输入测试")]

	[Export]
	public bool EnableKeyboardTest { get; set; } = true;


	private Control _rotationLayers = null!;
	private TextureRect _rotatingLayer = null!;
	private Label? _energyLabel;

	private Node? _energyVfxBack;
	private Node? _energyVfxFront;
	private CpuParticles2D? _burstBack;
	private CpuParticles2D? _burstFront;

	private int _currentEnergy;
	private bool _initialized;
	private Tween? _pulseTween;


	public int CurrentEnergy => _currentEnergy;

	public bool IsInitialized => _initialized;


	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_rotationLayers =
			GetNodeOrNull<Control>("Layers/RotationLayers");

		_rotatingLayer =
			GetNodeOrNull<TextureRect>(
				"Layers/RotationLayers/layer1"
			);

		_energyLabel = GetNodeOrNull<Label>("Label");
		_energyVfxBack = GetNodeOrNull<Node>("EnergyVfxBack");
		_energyVfxFront = GetNodeOrNull<Node>("EnergyVfxFront");
		_burstBack = GetNodeOrNull<CpuParticles2D>("BurstBack");
		_burstFront = GetNodeOrNull<CpuParticles2D>("BurstFront");

		if (_rotationLayers == null || _rotatingLayer == null)
		{
			GD.PushError(
				"GuZhenRenEnergyCounterController2：找不到 "
				+ "Layers/RotationLayers 或其中的 layer1。"
			);

			SetProcess(false);
			SetProcessInput(false);
			return;
		}

		ApplyRotationPivot();
		_rotationLayers.Resized += ApplyRotationPivot;
		_rotatingLayer.Resized += ApplyRotationPivot;

		// Control 的布局在 _Ready 之后仍可能更新一次，
		// 延迟重算可确保最终尺寸与留白已经确定。
		Callable.From(ApplyRotationPivot).CallDeferred();

		int safeMax = MaxEnergy < 1 ? 1 : MaxEnergy;
		_currentEnergy = ClampInt(StartingEnergy, 0, safeMax);
		_initialized = true;

		UpdateEnergyLabel();
		StopAllParticles();

		SetProcess(RotationEnabled);
		SetProcessInput(
			EnableKeyboardTest && !Engine.IsEditorHint()
		);
	}


	public override void _Process(double delta)
	{
		if (!_initialized || !RotationEnabled)
		{
			return;
		}

		float multiplier =
			_currentEnergy <= 0
				? EmptyRotationMultiplier
				: 1.0f;

		float radians =
			Mathf.DegToRad(RotationSpeedDegrees * multiplier)
			* (float)delta;

		_rotationLayers.Rotation = Mathf.Wrap(
			_rotationLayers.Rotation + radians,
			-Mathf.Pi,
			Mathf.Pi
		);
	}


	public override void _Input(InputEvent inputEvent)
	{
		if (!EnableKeyboardTest || Engine.IsEditorHint())
		{
			return;
		}

		if (
			inputEvent is not InputEventKey keyEvent
			|| !keyEvent.Pressed
			|| keyEvent.Echo
		)
		{
			return;
		}

		bool handled = true;

		switch (keyEvent.Keycode)
		{
			case Key.Q:
				SpendEnergy();
				break;

			case Key.E:
				GainEnergy();
				break;

			case Key.Space:
				PlayEnergyEffect();
				break;

			case Key.R:
				SetEnergy(MaxEnergy);
				break;

			default:
				handled = false;
				break;
		}

		if (handled)
		{
			GetViewport().SetInputAsHandled();
		}
	}


	public override void _ExitTree()
	{
		if (_pulseTween != null)
		{
			_pulseTween.Kill();
			_pulseTween = null;
		}
	}


	// =========================================================
	// 对外公开 API
	// =========================================================

	public void SetEnergy(int newEnergy)
	{
		int safeMax = MaxEnergy < 1 ? 1 : MaxEnergy;
		int clampedEnergy = ClampInt(newEnergy, 0, safeMax);

		if (!_initialized)
		{
			StartingEnergy = clampedEnergy;
			_currentEnergy = clampedEnergy;
			return;
		}

		if (clampedEnergy == _currentEnergy)
		{
			return;
		}

		_currentEnergy = clampedEnergy;
		UpdateEnergyLabel();
		PlayPulseAnimation();

		if (_currentEnergy > 0)
		{
			PlayAllParticles();
		}
		else
		{
			StopAllParticles();
		}
	}


	public void GainEnergy(int amount = 1)
	{
		if (amount > 0)
		{
			SetEnergy(_currentEnergy + amount);
		}
	}


	public void SpendEnergy(int amount = 1)
	{
		if (amount > 0)
		{
			SetEnergy(_currentEnergy - amount);
		}
	}


	public void PlayEnergyEffect()
	{
		if (!_initialized)
		{
			return;
		}

		PlayPulseAnimation();
		PlayAllParticles();
	}


	public void StopEnergyEffect()
	{
		StopAllParticles();
	}


	public void SetRotationEnabled(bool enabled)
	{
		RotationEnabled = enabled;
		SetProcess(enabled && _initialized);
	}


	public void SetRotationPivot(Vector2 pivot)
	{
		RotationPivot = pivot;

		ApplyRotationPivot();
	}


	/// <summary>
	/// 把原始纹理中的漩涡中心转换到 RotationLayers 的局部坐标。
	/// 不能直接把纹理像素坐标赋给父 Control 的 PivotOffset，
	/// 因为 TextureRect 可能有 KeepAspectCentered 留白，且 layer1
	/// 还可能带有 Scale、Position 与自己的 PivotOffset。
	/// </summary>
	private void ApplyRotationPivot()
	{
		if (_rotationLayers == null || _rotatingLayer == null)
		{
			return;
		}

		Vector2 layerLocalPoint =
			TexturePixelToLayerLocal(
				_rotatingLayer,
				RotationPivot
			);

		Vector2 globalPoint =
			_rotatingLayer.GetGlobalTransform()
				* layerLocalPoint;

		_rotationLayers.PivotOffset =
			_rotationLayers.GetGlobalTransform()
				.AffineInverse()
				* globalPoint;
	}


	private static Vector2 TexturePixelToLayerLocal(
		TextureRect layer,
		Vector2 texturePixel
	)
	{
		Texture2D? texture = layer.Texture;

		if (texture == null)
		{
			return texturePixel;
		}

		Vector2 textureSize = texture.GetSize();
		Vector2 rectSize = layer.Size;

		if (
			textureSize.X <= 0.0f
			|| textureSize.Y <= 0.0f
			|| rectSize.X <= 0.0f
			|| rectSize.Y <= 0.0f
		)
		{
			return texturePixel;
		}

		int stretchMode = (int)layer.StretchMode;

		// TextureRect.StretchModeEnum 的数值：
		// 0 Scale, 1 Tile, 2 Keep, 3 KeepCentered,
		// 4 KeepAspect, 5 KeepAspectCentered,
		// 6 KeepAspectCovered。
		switch (stretchMode)
		{
			case 0:
				return new Vector2(
					texturePixel.X * rectSize.X / textureSize.X,
					texturePixel.Y * rectSize.Y / textureSize.Y
				);

			case 2:
				return texturePixel;

			case 3:
				return (rectSize - textureSize) * 0.5f
					+ texturePixel;

			case 4:
			{
				float scale = Mathf.Min(
					rectSize.X / textureSize.X,
					rectSize.Y / textureSize.Y
				);

				return texturePixel * scale;
			}

			case 5:
			{
				float scale = Mathf.Min(
					rectSize.X / textureSize.X,
					rectSize.Y / textureSize.Y
				);

				Vector2 drawnSize = textureSize * scale;
				Vector2 offset = (rectSize - drawnSize) * 0.5f;

				return offset + texturePixel * scale;
			}

			case 6:
			{
				float scale = Mathf.Max(
					rectSize.X / textureSize.X,
					rectSize.Y / textureSize.Y
				);

				Vector2 drawnSize = textureSize * scale;
				Vector2 offset = (rectSize - drawnSize) * 0.5f;

				return offset + texturePixel * scale;
			}

			default:
				// Tile 模式无法唯一确定重复纹理中的哪一个点，
				// 保持原坐标作为安全回退。
				return texturePixel;
		}
	}


	// =========================================================
	// 标签与动画
	// =========================================================

	private void UpdateEnergyLabel()
	{
		if (_energyLabel == null)
		{
			return;
		}

		int safeMax = MaxEnergy < 1 ? 1 : MaxEnergy;
		_energyLabel.Text = _currentEnergy + "/" + safeMax;
	}


	private void PlayPulseAnimation()
	{
		if (_pulseTween != null)
		{
			_pulseTween.Kill();
		}

		Scale = Vector2.One;
		_pulseTween = CreateTween();

		_pulseTween
			.TweenProperty(
				this,
				"scale",
				Vector2.One * PulseScale,
				PulseExpandDuration
			)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);

		_pulseTween
			.TweenProperty(
				this,
				"scale",
				Vector2.One,
				PulseReturnDuration
			)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}


	// =========================================================
	// 粒子光效
	// =========================================================

	private void PlayAllParticles()
	{
		RestartParticlesRecursive(_energyVfxBack);
		RestartParticlesRecursive(_energyVfxFront);
		RestartParticlesRecursive(_burstBack);
		RestartParticlesRecursive(_burstFront);
	}


	private void StopAllParticles()
	{
		StopParticlesRecursive(_energyVfxBack);
		StopParticlesRecursive(_energyVfxFront);
		StopParticlesRecursive(_burstBack);
		StopParticlesRecursive(_burstFront);
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

		if (root is GpuParticles2D gpuParticles)
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

		if (root is GpuParticles2D gpuParticles)
		{
			gpuParticles.Emitting = false;
			gpuParticles.Visible = false;
		}

		foreach (Node child in root.GetChildren())
		{
			StopParticlesRecursive(child);
		}
	}


	private static int ClampInt(
		int value,
		int minimum,
		int maximum
	)
	{
		if (value < minimum)
		{
			return minimum;
		}

		if (value > maximum)
		{
			return maximum;
		}

		return value;
	}
}
