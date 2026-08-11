using Godot;

using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Ui;

/// <summary>
/// 顶栏配方大全按钮与只读配方浏览器（仅在冒险进行中显示）。
///
/// 按钮直接挂到原生地图按钮所在的顶栏节点中，并复用地图按钮的
/// 控件树、材质与动效。顶栏重建时会自动重新挂载，不依赖外部图片。
/// </summary>
internal sealed partial class RecipeCompendiumOverlay : CanvasLayer
{
    internal const string IconPath =
        "res://GuZhenRen/images/ui/recipe_compendium_icon.png";

    private const string BuiltInCompendiumIconPath =
        "res://images/atlases/ui_atlas.sprites/compendium.tres";
    private const string FallbackMapIconPath =
        "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_map.tres";
    private const float TopBarButtonGap = 14f;
    private static readonly Color Cream = StsColors.cream;
    private static readonly Color Gold = StsColors.gold;
    private static readonly Color Cyan = StsColors.blue;
    private static readonly Color Muted = new("b8c0c0");
    private static readonly Color PanelColor = new("263a42");
    private static readonly Color RowColor = new("344950");
    private static readonly Color BorderColor = new("8d9b98");

    private ColorRect _backdrop = null!;
    private PanelContainer _dialog = null!;
    private Button _shaZhaoTab = null!;
    private Button _heLianTab = null!;
    private LineEdit _search = null!;
    private Label _summary = null!;
    private VBoxContainer _recipeRows = null!;

    private IReadOnlyList<RecipeViewModel> _recipes = [];
    private RecipeCategory _selectedCategory = RecipeCategory.ShaZhao;
    private RecipeCompendiumTopBarButton? _topBarButton;
    private NTopBarMapButton? _mapButton;
    private NodePath _originalMapFocusNeighborLeft = new();
    private double _anchorScanCountdown;

    internal bool IsDialogOpen => _backdrop.Visible;

    public override void _Ready()
    {
        Layer = 92;
        ProcessMode = ProcessModeEnum.Always;

        BuildDialog();

        _anchorScanCountdown = 0d;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Process(double delta)
    {
        _anchorScanCountdown -= delta;
        if (_anchorScanCountdown > 0d)
        {
            return;
        }

        _anchorScanCountdown = 0.25d;
        RefreshTopBarButton();

        if (_backdrop.Visible &&
            NModalContainer.Instance?.OpenModal != null)
        {
            CloseDialog();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_backdrop.Visible ||
            @event is not InputEventKey
            {
                Pressed: true,
                Echo: false,
                Keycode: Key.Escape,
            })
        {
            return;
        }

        CloseDialog();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        RemoveTopBarButton();
    }

    private void BuildDialog()
    {
        _backdrop = new ColorRect
        {
            Name = "RecipeCompendiumBackdrop",
            Color = StsColors.screenBackdrop,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        AddChild(_backdrop);
        _backdrop.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect
        );
        _backdrop.GuiInput += OnBackdropGuiInput;

        _dialog = new PanelContainer
        {
            Name = "RecipeCompendiumDialog",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0.15f,
            AnchorTop = 0.10f,
            AnchorRight = 0.85f,
            AnchorBottom = 0.91f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };
        _dialog.AddThemeStyleboxOverride(
            "panel",
            CreatePanelStyle()
        );
        _backdrop.AddChild(_dialog);

        MarginContainer margin = new()
        {
            Name = "DialogMargin",
        };
        margin.AddThemeConstantOverride("margin_left", 34);
        margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        _dialog.AddChild(margin);

        VBoxContainer body = new()
        {
            Name = "DialogBody",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 16);
        margin.AddChild(body);

        body.AddChild(BuildHeader());
        body.AddChild(BuildTabsAndSearch());

        _summary = CreateLabel(string.Empty, 18, Muted);
        _summary.Name = "RecipeSummary";
        body.AddChild(_summary);

        ScrollContainer scroll = new()
        {
            Name = "RecipeScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode =
                ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(scroll);

        _recipeRows = new VBoxContainer
        {
            Name = "RecipeRows",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _recipeRows.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_recipeRows);

        Label footer = CreateLabel(
            T(
                "内容直接来自配方注册表，新增配方会自动收录。",
                "Entries come directly from the recipe registries and update automatically."
            ),
            16,
            Muted
        );
        footer.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(footer);
    }

    private Control BuildHeader()
    {
        HBoxContainer header = new()
        {
            Name = "Header",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        header.AddThemeConstantOverride("separation", 18);

        TextureRect icon = new()
        {
            Name = "HeaderIcon",
            CustomMinimumSize = new Vector2(70f, 58f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.Texture = LoadCompendiumTexture();
        header.AddChild(icon);

        VBoxContainer titles = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        titles.AddThemeConstantOverride("separation", 2);

        Label title = CreateLabel(
            T("配方大全", "Recipe Compendium"),
            34,
            Gold
        );
        title.AddThemeColorOverride(
            "font_outline_color",
            new Color(0.02f, 0.05f, 0.06f, 1f)
        );
        title.AddThemeConstantOverride("outline_size", 5);
        titles.AddChild(title);

        titles.AddChild(CreateLabel(
            T(
                "杀招推演与蛊虫合练的完整配方录",
                "Complete killer-move and Gu-refinement recipes"
            ),
            18,
            Cyan
        ));
        header.AddChild(titles);

        Button close = new()
        {
            Name = "CloseButton",
            Text = T("返回", "Back"),
            Flat = true,
            CustomMinimumSize = new Vector2(100f, 48f),
            FocusMode = Control.FocusModeEnum.All,
            TooltipText = T("关闭", "Close"),
        };
        close.AddThemeFontSizeOverride("font_size", 22);
        close.AddThemeColorOverride("font_color", Cream);
        close.AddThemeColorOverride("font_hover_color", Gold);
        close.AddThemeColorOverride("font_pressed_color", StsColors.lightGray);
        close.Pressed += CloseDialog;
        header.AddChild(close);

        return header;
    }

    private Control BuildTabsAndSearch()
    {
        HBoxContainer row = new()
        {
            Name = "TabsAndSearch",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 12);

        _shaZhaoTab = CreateTabButton(
            T("杀招配方", "Killer Moves"),
            RecipeCategory.ShaZhao
        );
        row.AddChild(_shaZhaoTab);

        _heLianTab = CreateTabButton(
            T("合练蛊配方", "Gu Refinement"),
            RecipeCategory.HeLian
        );
        row.AddChild(_heLianTab);

        Control spacer = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(spacer);

        _search = new LineEdit
        {
            Name = "RecipeSearch",
            CustomMinimumSize = new Vector2(330f, 46f),
            PlaceholderText = T(
                "搜索结果或材料……",
                "Search results or materials…"
            ),
            ClearButtonEnabled = true,
            FocusMode = Control.FocusModeEnum.All,
        };
        _search.AddThemeFontSizeOverride("font_size", 19);
        _search.TextChanged += _ => RebuildVisibleRows();
        row.AddChild(_search);

        return row;
    }

    private Button CreateTabButton(
        string text,
        RecipeCategory category
    )
    {
        Button button = new()
        {
            Text = text,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(190f, 48f),
            FocusMode = Control.FocusModeEnum.All,
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        button.AddThemeColorOverride("font_color", Cream);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateTabStyle(true));
        button.AddThemeStyleboxOverride("focus", CreateTabStyle(true));
        button.Pressed += () => SelectCategory(category);
        return button;
    }

    private void OpenDialog()
    {
        if (!IsRunUiAvailable() ||
            _backdrop.Visible ||
            NModalContainer.Instance?.OpenModal != null)
        {
            return;
        }

        try
        {
            _recipes = LoadRecipes();
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                "读取配方大全失败：" + exception
            );
            _recipes = [];
        }

        _search.Text = string.Empty;
        _backdrop.Visible = true;
        SelectCategory(_selectedCategory);
        _topBarButton?.RefreshOpenState();
        _topBarButton?.ReleaseFocus();
        _search.GrabFocus();
    }

    private void CloseDialog()
    {
        if (!_backdrop.Visible)
        {
            return;
        }

        _backdrop.Visible = false;
        _topBarButton?.RefreshOpenState();
        _anchorScanCountdown = 0d;
    }

    internal void ToggleDialog()
    {
        if (_backdrop.Visible)
        {
            CloseDialog();
        }
        else
        {
            OpenDialog();
        }
    }

    private void SelectCategory(RecipeCategory category)
    {
        _selectedCategory = category;
        _shaZhaoTab.ButtonPressed =
            category == RecipeCategory.ShaZhao;
        _heLianTab.ButtonPressed =
            category == RecipeCategory.HeLian;
        RebuildVisibleRows();
    }

    private void RebuildVisibleRows()
    {
        foreach (Node child in _recipeRows.GetChildren())
        {
            _recipeRows.RemoveChild(child);
            child.QueueFree();
        }

        string query = _search.Text.Trim();
        RecipeViewModel[] visible = _recipes
            .Where(recipe =>
                recipe.Category == _selectedCategory &&
                (query.Length == 0 ||
                 recipe.SearchText.Contains(
                     query,
                     StringComparison.CurrentCultureIgnoreCase
                 ))
            )
            .OrderBy(recipe => recipe.ResultName, StringComparer.CurrentCulture)
            .ThenBy(recipe => recipe.Formula, StringComparer.CurrentCulture)
            .ToArray();

        int categoryCount = _recipes.Count(recipe =>
            recipe.Category == _selectedCategory
        );
        _summary.Text = query.Length == 0
            ? T(
                $"共收录 {categoryCount} 条配方",
                $"{categoryCount} recipes"
            )
            : T(
                $"找到 {visible.Length} / {categoryCount} 条配方",
                $"Showing {visible.Length} of {categoryCount} recipes"
            );

        if (visible.Length == 0)
        {
            Label empty = CreateLabel(
                _recipes.Count == 0
                    ? T(
                        "配方数据尚未就绪，请稍后重新打开。",
                        "Recipe data is not ready yet. Please reopen this screen shortly."
                    )
                    : T(
                        "没有符合条件的配方。",
                        "No matching recipes."
                    ),
                22,
                Muted
            );
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(0f, 120f);
            _recipeRows.AddChild(empty);
            return;
        }

        foreach (RecipeViewModel recipe in visible)
        {
            _recipeRows.AddChild(BuildRecipeRow(recipe));
        }
    }

    private Control BuildRecipeRow(RecipeViewModel recipe)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            CreateRowStyle()
        );

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        VBoxContainer contents = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        contents.AddThemeConstantOverride("separation", 6);
        margin.AddChild(contents);

        Label result = CreateLabel(recipe.ResultName, 25, Gold);
        result.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        contents.AddChild(result);

        Label formula = CreateLabel(recipe.Formula, 21, Cream);
        formula.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        formula.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contents.AddChild(formula);

        if (recipe.MinimumMaterialRank > 1)
        {
            contents.AddChild(CreateLabel(
                T(
                    $"每张材料至少 {ToChineseRank(recipe.MinimumMaterialRank)}转",
                    $"Each material must be rank {recipe.MinimumMaterialRank}+"
                ),
                17,
                Cyan
            ));
        }

        return panel;
    }

    private void OnBackdropGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
            } mouse)
        {
            return;
        }

        if (!_dialog.GetGlobalRect().HasPoint(mouse.GlobalPosition))
        {
            CloseDialog();
            _backdrop.AcceptEvent();
        }
    }

    private void RefreshTopBarButton()
    {
        if (!IsRunUiAvailable())
        {
            RemoveTopBarButton();

            if (_backdrop.Visible)
            {
                CloseDialog();
            }

            return;
        }

        NTopBarMapButton? mapButton =
            NRun.Instance?.GlobalUi?.TopBar?.Map;
        if (mapButton == null ||
            !GodotObject.IsInstanceValid(mapButton) ||
            mapButton.GetParent() == null)
        {
            RemoveTopBarButton();
            return;
        }

        if (_topBarButton != null &&
            GodotObject.IsInstanceValid(_topBarButton) &&
            _mapButton == mapButton &&
            _topBarButton.GetParent() == mapButton.GetParent())
        {
            _topBarButton.Visible = mapButton.Visible;
            return;
        }

        RemoveTopBarButton();
        AttachTopBarButton(mapButton);
    }

    private void AttachTopBarButton(NTopBarMapButton mapButton)
    {
        Node? parent = mapButton.GetParent();
        if (parent == null)
        {
            return;
        }

        RecipeCompendiumTopBarButton button;
        try
        {
            button = RecipeCompendiumTopBarButton.Create(
                mapButton,
                this
            );
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                "创建原生顶栏配方大全按钮失败：" + exception
            );
            return;
        }

        int mapIndex = mapButton.GetIndex();
        parent.AddChild(button);
        parent.MoveChild(button, mapIndex);

        CopyMapButtonLayout(
            mapButton,
            button,
            parent is Container
                ? 0f
                : -(mapButton.Size.X + TopBarButtonGap)
        );

        _originalMapFocusNeighborLeft =
            mapButton.FocusNeighborLeft;
        button.FocusNeighborLeft =
            _originalMapFocusNeighborLeft;
        button.FocusNeighborRight = mapButton.GetPath();
        button.FocusNeighborTop = mapButton.FocusNeighborTop;
        button.FocusNeighborBottom = mapButton.FocusNeighborBottom;
        mapButton.FocusNeighborLeft = button.GetPath();

        _mapButton = mapButton;
        _topBarButton = button;
    }

    private static void CopyMapButtonLayout(
        NTopBarMapButton source,
        RecipeCompendiumTopBarButton target,
        float horizontalOffset
    )
    {
        target.AnchorLeft = source.AnchorLeft;
        target.AnchorTop = source.AnchorTop;
        target.AnchorRight = source.AnchorRight;
        target.AnchorBottom = source.AnchorBottom;
        target.OffsetLeft = source.OffsetLeft + horizontalOffset;
        target.OffsetTop = source.OffsetTop;
        target.OffsetRight = source.OffsetRight + horizontalOffset;
        target.OffsetBottom = source.OffsetBottom;
        target.CustomMinimumSize = source.CustomMinimumSize;
        target.SizeFlagsHorizontal = source.SizeFlagsHorizontal;
        target.SizeFlagsVertical = source.SizeFlagsVertical;
        target.GrowHorizontal = source.GrowHorizontal;
        target.GrowVertical = source.GrowVertical;
        target.PivotOffset = source.PivotOffset;
        target.ZIndex = source.ZIndex;
        target.Visible = source.Visible;
    }

    private void RemoveTopBarButton()
    {
        RecipeCompendiumTopBarButton? button = _topBarButton;
        NTopBarMapButton? mapButton = _mapButton;

        _topBarButton = null;
        _mapButton = null;

        if (button != null &&
            GodotObject.IsInstanceValid(button) &&
            mapButton != null &&
            GodotObject.IsInstanceValid(mapButton) &&
            mapButton.FocusNeighborLeft == button.GetPath())
        {
            mapButton.FocusNeighborLeft =
                _originalMapFocusNeighborLeft;
        }

        if (button != null && GodotObject.IsInstanceValid(button))
        {
            button.QueueFree();
        }

        _originalMapFocusNeighborLeft = new NodePath();
    }

    private static bool IsRunUiAvailable()
    {
        RunManager runManager = RunManager.Instance;
        return runManager.IsInProgress && !runManager.IsCleaningUp;
    }

    internal static Texture2D? LoadCompendiumTexture()
    {
        HashSet<string> attemptedPaths = [];
        string[] paths =
        [
            IconPath,
            BuiltInCompendiumIconPath,
            FallbackMapIconPath,
        ];

        foreach (string path in paths)
        {
            if (!attemptedPaths.Add(path))
            {
                continue;
            }

            try
            {
                Texture2D texture =
                    PreloadManager.Cache.GetTexture2D(path);
                if (GodotObject.IsInstanceValid(texture))
                {
                    return texture;
                }
            }
            catch (Exception exception)
            {
                Entry.Logger.Warn(
                    $"配方大全图标加载失败（{path}）：{exception.Message}"
                );
            }
        }

        Entry.Logger.Warn(
            "配方大全图标及地图图标兜底均无法加载。"
        );
        return null;
    }

    private static IReadOnlyList<RecipeViewModel> LoadRecipes()
    {
        Dictionary<Type, string> cardNames = ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .AllCards
            .Concat(
                ModelDb
                    .CardPool<GuZhenRenShaZhaoCardPool>()
                    .AllCards
            )
            .GroupBy(card => card.GetType())
            .ToDictionary(
                group => group.Key,
                group => group.First().Title
            );

        string NameOf(Type type) =>
            cardNames.TryGetValue(type, out string? name)
                ? name
                : type.Name;

        List<RecipeViewModel> recipes = [];

        foreach ((Type resultType, IReadOnlyList<Type> materials) in
                 ShaZhaoRecipeRegistry.GetRecipes())
        {
            string resultName = NameOf(resultType);
            string materialText = FormatMaterials(materials, NameOf);
            recipes.Add(new RecipeViewModel(
                RecipeCategory.ShaZhao,
                resultName,
                $"{materialText}  →  {resultName}",
                1,
                $"{resultName} {materialText}"
            ));
        }

        foreach ((
                     Type resultType,
                     IReadOnlyList<Type> materials,
                     int minimumRank
                 ) in HeLianRecipeRegistry.GetRecipeDetails())
        {
            string resultName = NameOf(resultType);
            string materialText = FormatMaterials(materials, NameOf);
            recipes.Add(new RecipeViewModel(
                RecipeCategory.HeLian,
                resultName,
                $"{materialText}  →  {resultName}",
                minimumRank,
                $"{resultName} {materialText} {minimumRank}"
            ));
        }

        return recipes;
    }

    private static string FormatMaterials(
        IEnumerable<Type> materialTypes,
        Func<Type, string> nameOf
    )
    {
        return string.Join(
            " + ",
            materialTypes
                .GroupBy(type => type)
                .OrderBy(
                    group => nameOf(group.Key),
                    StringComparer.CurrentCulture
                )
                .Select(group =>
                {
                    string name = nameOf(group.Key);
                    int count = group.Count();
                    return count == 1 ? name : $"{name} ×{count}";
                })
        );
    }

    private static Label CreateLabel(
        string text,
        int fontSize,
        Color color
    )
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderColor = BorderColor,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.65f),
            ShadowSize = 18,
        };
    }

    private static StyleBoxFlat CreateRowStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = RowColor,
            BorderColor = new Color("71888c"),
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 2,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };
    }

    private static StyleBoxFlat CreateTabStyle(bool highlighted)
    {
        Color border = highlighted ? Gold : BorderColor;
        return new StyleBoxFlat
        {
            BgColor = highlighted
                ? new Color("40545a")
                : new Color("1d3037"),
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
    }

    private static string T(string zhs, string eng)
    {
        return string.Equals(
            LocManager.Instance.Language,
            "zhs",
            StringComparison.OrdinalIgnoreCase
        ) ? zhs : eng;
    }

    private static string ToChineseRank(int rank) => rank switch
    {
        1 => "一",
        2 => "二",
        3 => "三",
        4 => "四",
        5 => "五",
        6 => "六",
        7 => "七",
        8 => "八",
        9 => "九",
        _ => rank.ToString(),
    };

    private enum RecipeCategory
    {
        ShaZhao,
        HeLian,
    }

    private sealed record RecipeViewModel(
        RecipeCategory Category,
        string ResultName,
        string Formula,
        int MinimumMaterialRank,
        string SearchText
    );

}
