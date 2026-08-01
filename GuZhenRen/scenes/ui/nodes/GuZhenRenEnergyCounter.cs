using Godot;

/// <summary>
/// 蛊真人能量表盘。
///
/// 文件名与类名严格一致：GuZhenRenEnergyCounter.cs
/// 旋转、能量变化、回弹和粒子光效全部由 C# 控制。
///
/// 测试按键：
/// Q：消耗 1 点能量
/// E：增加 1 点能量
/// Space：只播放一次光效
/// R：恢复最大能量
/// </summary>
[Tool]
public partial class GuZhenRenEnergyCounter : Control
{
	[ExportGroup("能量设置")]

	[Export(PropertyHint.Range, "1,99,1")]
	public int MaxEnergy { get; set; } = 3;

	[Export(PropertyHint.Range, "0,99,1")]
	public int StartingEnergy { get; set; } = 3;


	[ExportGroup("旋转设置")]

	[Export]
	public bool RotationEnabled { get; set; } = true;

	[Export]
	public float Layer2Speed { get; set; } = 45.0f;

	[Export]
	public float Layer3Speed { get; set; } = -45.0f;

	[Export]
	public float Layer4Speed { get; set; } = 22.5f;

	[Export]
	public float Layer5Speed { get; set; } = -90.0f;

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float EmptyRotationMultiplier { get; set; } = 0.25f;


	[ExportGroup("输入测试")]

	[Export]
	public bool EnableKeyboardTest { get; set; } = true;


	[ExportGroup("颜色")]

	[Export]
	public Color EmptyLabelColor { get; set; } =
		new Color(0.58f, 0.58f, 0.58f, 1.0f);

	[Export]
	public Color ActiveLabelColor { get; set; } = Colors.White;


	[ExportGroup("回弹动画")]

	[Export(PropertyHint.Range, "1.0,1.3,0.01")]
	public float PulseScale { get; set; } = 1.08f;

	[Export(PropertyHint.Range, "0.01,1.0,0.01")]
	public float PulseExpandDuration { get; set; } = 0.08f;

	[Export(PropertyHint.Range, "0.01,1.0,0.01")]
	public float PulseReturnDuration { get; set; } = 0.12f;


	private const string DarkLayer1Path =
		"res://GuZhenRen/images/ui/orb/1d.png";

	private const string DarkLayer2Path =
		"res://GuZhenRen/images/ui/orb/2d.png";

	private const string DarkLayer3Path =
		"res://GuZhenRen/images/ui/orb/3d.png";

	private const string DarkLayer4Path =
		"res://GuZhenRen/images/ui/orb/4d.png";

	private const string DarkLayer5Path =
		"res://GuZhenRen/images/ui/orb/5d.png";


	private TextureRect _layer1 = null!;
	private TextureRect _layer2 = null!;
	private TextureRect _layer3 = null!;
	private TextureRect _layer4 = null!;
	private TextureRect _layer5 = null!;
	private Label _energyLabel = null!;

	private Node? _energyVfxBack;
	private Node? _energyVfxFront;

	private Texture2D? _brightLayer1;
	private Texture2D? _brightLayer2;
	private Texture2D? _brightLayer3;
	private Texture2D? _brightLayer4;
	private Texture2D? _brightLayer5;

	private Texture2D? _darkLayer1;
	private Texture2D? _darkLayer2;
	private Texture2D? _darkLayer3;
	private Texture2D? _darkLayer4;
	private Texture2D? _darkLayer5;

	private int _currentEnergy;
	private bool _initialized;
	private Tween? _pulseTween;


	public int CurrentEnergy
	{
		get { return _currentEnergy; }
	}


	public bool IsInitialized
	{
		get { return _initialized; }
	}


	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetProcess(true);
		SetProcessInput(!Engine.IsEditorHint());

		if (!BindNodes())
		{
			SetProcess(false);
			SetProcessInput(false);
			return;
		}

		SetRotationPivot(_layer2);
		SetRotationPivot(_layer3);
		SetRotationPivot(_layer4);
		SetRotationPivot(_layer5);

		CacheTextures();

		int safeMax = MaxEnergy < 1 ? 1 : MaxEnergy;
		_currentEnergy = ClampInt(StartingEnergy, 0, safeMax);
		_initialized = true;

		UpdateEnergyLabel();
		ApplyEnergyVisualState();
		StopAllParticles();
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

		float frameDelta = (float)delta;

		RotateLayer(
			_layer2,
			Layer2Speed * multiplier,
			frameDelta
		);

		RotateLayer(
			_layer3,
			Layer3Speed * multiplier,
			frameDelta
		);

		RotateLayer(
			_layer4,
			Layer4Speed * multiplier,
			frameDelta
		);

		RotateLayer(
			_layer5,
			Layer5Speed * multiplier,
			frameDelta
		);
	}


	public override void _Input(InputEvent inputEvent)
	{
		if (
			Engine.IsEditorHint()
			|| !EnableKeyboardTest
		)
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
				SpendEnergy(1);
				break;

			case Key.E:
				GainEnergy(1);
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
		ApplyEnergyVisualState();
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

		if (_currentEnergy > 0)
		{
			PlayAllParticles();
		}
	}


	public void StopEnergyEffect()
	{
		StopAllParticles();
	}


	public void SetRotationEnabled(bool enabled)
	{
		RotationEnabled = enabled;
	}


	// =========================================================
	// 节点与资源
	// =========================================================

	private bool BindNodes()
	{
		_layer1 =
			GetNodeOrNull<TextureRect>("Layers/layer1");

		_layer2 =
			GetNodeOrNull<TextureRect>(
                "Layers/RotationLayers/layer2"
			);

		_layer3 =
			GetNodeOrNull<TextureRect>(
                "Layers/RotationLayers/layer3"
			);

		_layer4 =
			GetNodeOrNull<TextureRect>(
                "Layers/RotationLayers/layer4"
			);

		_layer5 =
			GetNodeOrNull<TextureRect>(
                "Layers/RotationLayers/layer5"
			);

		_energyLabel =
			GetNodeOrNull<Label>("Label");

		_energyVfxBack =
			GetNodeOrNull<Node>("EnergyVfxBack");

		_energyVfxFront =
			GetNodeOrNull<Node>("EnergyVfxFront");

		if (
			_layer1 != null
			&& _layer2 != null
			&& _layer3 != null
			&& _layer4 != null
			&& _layer5 != null
			&& _energyLabel != null
		)
		{
			return true;
		}

		GD.PushError(
            "GuZhenRenEnergyCounter：无法绑定 layer1～layer5 或 Label。"
		);

		return false;
	}


	private void CacheTextures()
	{
		_brightLayer1 = _layer1.Texture;
		_brightLayer2 = _layer2.Texture;
		_brightLayer3 = _layer3.Texture;
		_brightLayer4 = _layer4.Texture;
		_brightLayer5 = _layer5.Texture;

		_darkLayer1 =
			LoadTextureOrFallback(
				DarkLayer1Path,
				_brightLayer1
			);

		_darkLayer2 =
			LoadTextureOrFallback(
				DarkLayer2Path,
				_brightLayer2
			);

		_darkLayer3 =
			LoadTextureOrFallback(
				DarkLayer3Path,
				_brightLayer3
			);

		_darkLayer4 =
			LoadTextureOrFallback(
				DarkLayer4Path,
				_brightLayer4
			);

		_darkLayer5 =
			LoadTextureOrFallback(
				DarkLayer5Path,
				_brightLayer5
			);
	}


	private static Texture2D? LoadTextureOrFallback(
		string resourcePath,
		Texture2D? fallback
	)
	{
		if (!ResourceLoader.Exists(resourcePath))
		{
			GD.PushWarning(
				"未找到贴图：" + resourcePath
				+ "，已使用亮色贴图。"
			);

			return fallback;
		}

		Texture2D texture =
			GD.Load<Texture2D>(resourcePath);

		if (texture == null)
		{
			GD.PushWarning(
				"加载贴图失败：" + resourcePath
				+ "，已使用亮色贴图。"
			);

			return fallback;
		}

		return texture;
	}


	private static void SetRotationPivot(Control layer)
	{
		layer.PivotOffset = layer.Size * 0.5f;
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


	// =========================================================
	// 显示、旋转与回弹
	// =========================================================

	private void ApplyEnergyVisualState()
	{
		bool isEmpty = _currentEnergy <= 0;

		_layer1.Texture =
			isEmpty ? _darkLayer1 : _brightLayer1;

		_layer2.Texture =
			isEmpty ? _darkLayer2 : _brightLayer2;

		_layer3.Texture =
			isEmpty ? _darkLayer3 : _brightLayer3;

		_layer4.Texture =
			isEmpty ? _darkLayer4 : _brightLayer4;

		_layer5.Texture =
			isEmpty ? _darkLayer5 : _brightLayer5;

		_energyLabel.Modulate =
			isEmpty
				? EmptyLabelColor
				: ActiveLabelColor;
	}


	private void UpdateEnergyLabel()
	{
		int safeMax = MaxEnergy < 1 ? 1 : MaxEnergy;

		_energyLabel.Text =
			_currentEnergy.ToString()
			+ "/"
			+ safeMax.ToString();
	}


	private static void RotateLayer(
		Control layer,
		float degreesPerSecond,
		float delta
	)
	{
		if (Mathf.IsZeroApprox(degreesPerSecond))
		{
			return;
		}

		float amount =
			Mathf.DegToRad(degreesPerSecond) * delta;

		layer.Rotation =
			Mathf.Wrap(
				layer.Rotation + amount,
				-Mathf.Pi,
				Mathf.Pi
			);
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
	}


	private void StopAllParticles()
	{
		StopParticlesRecursive(_energyVfxBack);
		StopParticlesRecursive(_energyVfxFront);
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
}
