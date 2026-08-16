using Godot;

using GuZhenRen.Cards;
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.LiDao;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
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
        "res://GuZhenRenPersonal/images/ui/recipe_compendium_icon.png";

    private const string BuiltInCompendiumIconPath =
        "res://images/atlases/ui_atlas.sprites/compendium.tres";
    private const string FallbackMapIconPath =
        "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_map.tres";
    private const float TopBarButtonGap = 14f;
    private static readonly Color Cream = new("eee3c7");
    private static readonly Color Gold = new("d0a45e");
    private static readonly Color Cyan = new("69a6a8");
    private static readonly Color Ink = new("e7dfcb");
    private static readonly Color Muted = new("9aa3a1");
    private static readonly Color PanelColor = new("151d21");
    private static readonly Color RowColor = new("202b30");
    private static readonly Color BorderColor = new("73634f");
    private static readonly Color Cinnabar = new("c36d5a");

    private ColorRect _backdrop = null!;
    private PanelContainer _dialog = null!;
    private Button _shaZhaoTab = null!;
    private Button _heLianTab = null!;
    private LineEdit _search = null!;
    private Label _summary = null!;
    private VBoxContainer _recipeRows = null!;
    private VBoxContainer _listView = null!;
    private VBoxContainer _detailView = null!;
    private Control _previewHost = null!;
    private Label _detailTitle = null!;
    private Label _detailMeta = null!;
    private RichTextLabel _detailDescription = null!;
    private HFlowContainer _rankButtons = null!;
    private HFlowContainer _relatedCards = null!;
    private NPreviewCardHolder? _previewCardHolder;
    private Type? _selectedCardType;
    private int _selectedRank = 1;

    private IReadOnlyList<RecipeViewModel> _recipes = [];
    private IReadOnlyDictionary<Type, CardModel> _cardModels =
        new Dictionary<Type, CardModel>();
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

        if (_detailView.Visible) ShowRecipeList();
        else CloseDialog();
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

        TextureRect paperTexture = new()
        {
            Name = "XuanPaperTexture",
            Texture = CreateXuanPaperTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Tile,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.72f),
        };
        _dialog.AddChild(paperTexture);
        paperTexture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

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
        _listView = new VBoxContainer
        {
            Name = "RecipeListView",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _listView.AddThemeConstantOverride("separation", 16);
        body.AddChild(_listView);
        _listView.AddChild(BuildTabsAndSearch());

        _summary = CreateLabel(string.Empty, 18, Muted);
        _summary.Name = "RecipeSummary";
        _listView.AddChild(_summary);

        ScrollContainer scroll = new()
        {
            Name = "RecipeScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode =
                ScrollContainer.ScrollMode.Disabled,
        };
        _listView.AddChild(scroll);

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
        _listView.AddChild(footer);
        _detailView = BuildDetailView();
        _detailView.Visible = false;
        body.AddChild(_detailView);
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
        close.AddThemeColorOverride("font_color", Ink);
        close.AddThemeColorOverride("font_hover_color", Gold);
        close.AddThemeColorOverride("font_pressed_color", Cinnabar);
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
        _search.AddThemeColorOverride("font_color", Ink);
        _search.AddThemeColorOverride("font_placeholder_color", Muted);
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
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateTabStyle(true));
        button.AddThemeStyleboxOverride("focus", CreateTabStyle(true));
        button.Pressed += () => SelectCategory(category);
        return button;
    }

    private VBoxContainer BuildDetailView()
    {
        VBoxContainer detail = new()
        {
            Name = "GuDetailView",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        detail.AddThemeConstantOverride("separation", 14);

        HBoxContainer toolbar = new();
        Button back = CreateInkButton(T("← 返回配方", "← Back to recipes"));
        back.CustomMinimumSize = new Vector2(180f, 44f);
        back.Pressed += ShowRecipeList;
        toolbar.AddChild(back);
        toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        toolbar.AddChild(CreateLabel(
            T("选择转数，卡面与说明将同步变化", "Choose a rank to update the card and text"), 17, Muted));
        detail.AddChild(toolbar);

        HBoxContainer columns = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 34);
        detail.AddChild(columns);
        _previewHost = new Control
        {
            Name = "CardPreviewHost",
            CustomMinimumSize = new Vector2(350f, 500f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        columns.AddChild(_previewHost);

        VBoxContainer copy = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        copy.AddThemeConstantOverride("separation", 12);
        columns.AddChild(copy);
        _detailTitle = CreateLabel(string.Empty, 34, Gold);
        _detailTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        copy.AddChild(_detailTitle);
        _detailMeta = CreateLabel(string.Empty, 18, Cyan);
        _detailMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        copy.AddChild(_detailMeta);
        copy.AddChild(CreateLabel(T("蛊虫介绍", "Gu introduction"), 23, Cinnabar));
        _detailDescription = new RichTextLabel
        {
            Name = "GuDescription",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(0f, 145f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _detailDescription.AddThemeFontSizeOverride("normal_font_size", 20);
        _detailDescription.AddThemeColorOverride("default_color", Ink);
        copy.AddChild(_detailDescription);
        copy.AddChild(CreateLabel(T("预览转数", "Preview rank"), 21, Cinnabar));
        _rankButtons = new HFlowContainer
        {
            Name = "RankSelector",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _rankButtons.AddThemeConstantOverride("h_separation", 7);
        _rankButtons.AddThemeConstantOverride("v_separation", 7);
        copy.AddChild(_rankButtons);

        copy.AddChild(CreateLabel(T("伴生牌与衍生牌", "Companion & generated cards"), 21, Cinnabar));
        _relatedCards = new HFlowContainer
        {
            Name = "RelatedCardPreviews",
            CustomMinimumSize = new Vector2(0f, 205f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _relatedCards.AddThemeConstantOverride("h_separation", 12);
        _relatedCards.AddThemeConstantOverride("v_separation", 10);
        copy.AddChild(_relatedCards);
        return detail;
    }

    private static Button CreateInkButton(string text)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(92f, 40f),
        };
        button.AddThemeFontSizeOverride("font_size", 19);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Cinnabar);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeStyleboxOverride("normal", CreateSmallButtonStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateSmallButtonStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateSmallButtonStyle(true));
        button.AddThemeStyleboxOverride("focus", CreateSmallButtonStyle(true));
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
            (_recipes, _cardModels) = LoadRecipes();
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                "读取配方大全失败：" + exception
            );
            _recipes = [];
            _cardModels = new Dictionary<Type, CardModel>();
        }

        _search.Text = string.Empty;
        ShowRecipeList();
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
        ClearCardPreview();
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
            .ThenBy(recipe => recipe.SearchText, StringComparer.CurrentCulture)
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

        HFlowContainer formula = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        formula.AddThemeConstantOverride("h_separation", 5);
        formula.AddThemeConstantOverride("v_separation", 6);
        contents.AddChild(formula);
        if (recipe.IsGenericBeastRecipe)
        {
            formula.AddChild(CreateLabel(T("任意三种不同兽力蛊", "Any three different beast-strength Gu"), 21, Ink));
        }
        else
        {
            for (int index = 0; index < recipe.MaterialTypes.Count; index++)
            {
                if (index > 0) formula.AddChild(CreateLabel(" + ", 21, Muted));
                formula.AddChild(CreateCardNameControl(recipe.MaterialTypes[index]));
            }
        }
        formula.AddChild(CreateLabel("  →  ", 21, Cinnabar));
        formula.AddChild(CreateCardNameControl(recipe.ResultType));

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

    private Control CreateCardNameControl(Type cardType)
    {
        if (!_cardModels.TryGetValue(cardType, out CardModel? card) || card is not IGuWormCard)
        {
            return CreateLabel(_cardModels.TryGetValue(cardType, out card) ? card.Title : cardType.Name, 21, Ink);
        }
        Button button = CreateInkButton(card.Title);
        button.TooltipText = T("查看蛊虫介绍与各转卡牌", "View Gu details and rank previews");
        button.Pressed += () => ShowCardDetail(cardType);
        return button;
    }

    private void ShowCardDetail(Type cardType)
    {
        if (!_cardModels.TryGetValue(cardType, out CardModel? canonical) ||
            canonical is not AbstractGuZhenRenCard guCard || canonical is not IGuWormCard) return;

        _selectedCardType = cardType;
        _selectedRank = Math.Clamp(guCard.GuRank, 1, guCard.MaxGuRank);
        foreach (Node child in _rankButtons.GetChildren()) { _rankButtons.RemoveChild(child); child.QueueFree(); }
        for (int rank = 1; rank <= guCard.MaxGuRank; rank++)
        {
            int selected = rank;
            Button rankButton = CreateInkButton(T($"{ToChineseRank(rank)}转", $"Rank {rank}"));
            rankButton.ToggleMode = true;
            rankButton.ButtonPressed = rank == _selectedRank;
            rankButton.Pressed += () => SelectPreviewRank(selected);
            _rankButtons.AddChild(rankButton);
        }
        _listView.Visible = false;
        _detailView.Visible = true;
        RefreshCardDetail();
    }

    private void SelectPreviewRank(int rank)
    {
        _selectedRank = rank;
        int currentRank = 1;
        foreach (Node child in _rankButtons.GetChildren())
            if (child is Button button) button.ButtonPressed = currentRank++ == rank;
        RefreshCardDetail();
    }

    private void RefreshCardDetail()
    {
        if (_selectedCardType == null || !_cardModels.TryGetValue(_selectedCardType, out CardModel? canonical) ||
            canonical.ToMutable() is not AbstractGuZhenRenCard preview) return;
        preview.InitializeGuRankFromSource(_selectedRank);
        _detailTitle.Text = preview.Title;
        string dao = preview.CurrentDao is { } value ? GetDaoName(value) : T("无流派", "No path");
        IGuWormCard worm = (IGuWormCard)preview;
        _detailMeta.Text = T(
            $"{ToChineseRank(preview.GuRank)}转 · {dao} · {GetRarityName(preview.Rarity)} · 催动消耗 {worm.YuanQiCost} 元气",
            $"Rank {preview.GuRank} · {dao} · {preview.Rarity} · {worm.YuanQiCost} Yuan Qi");
        _detailDescription.Text = FormatDescriptionBbcode(
            preview.GetDescriptionForPile(PileType.None)
        );

        ClearCardPreview();
        NCard? cardNode = NCard.Create(preview);
        if (cardNode == null) return;
        _previewCardHolder = NPreviewCardHolder.Create(cardNode, true, false);
        if (_previewCardHolder == null) { cardNode.QueueFree(); return; }
        _previewCardHolder.SetCardScale(Vector2.One * 0.72f);
        _previewCardHolder.Position = new Vector2(
            _previewHost.CustomMinimumSize.X * 0.5f,
            _previewHost.CustomMinimumSize.Y * 0.43f
        );
        _previewHost.AddChild(_previewCardHolder);
        cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        RebuildRelatedCardPreviews(preview);
    }

    private void RebuildRelatedCardPreviews(AbstractGuZhenRenCard source)
    {
        ClearRelatedCardPreviews();
        List<(string Kind, CardModel Card)> related = [];
        HashSet<Type> includedTypes = [];

        if (source is ILiDaoBeastGuCard beastGu)
        {
            CardModel companion = ModelDb.GetById<CardModel>(
                ModelDb.GetId(beastGu.CompanionCardType)
            ).ToMutable();
            if (companion is AbstractGuZhenRenCard rankedCompanion)
            {
                rankedCompanion.InitializeGuRankFromSource(source.GuRank);
            }
            related.Add((T("伴生牌", "Companion"), companion));
            includedTypes.Add(companion.GetType());
        }

        if (source is AbstractGuWormCard wormCard)
        {
            foreach (CardModel generated in wormCard.GetCarouselCards())
            {
                if (!includedTypes.Add(generated.GetType())) continue;
                if (generated is AbstractGuZhenRenCard rankedGenerated)
                {
                    rankedGenerated.InitializeGuRankFromSource(source.GuRank);
                }
                related.Add((T("衍生牌", "Generated"), generated));
            }
        }

        if (related.Count == 0)
        {
            _relatedCards.AddChild(CreateLabel(
                T("此转数暂无伴生牌或衍生牌。", "No companion or generated cards at this rank."),
                17,
                Muted
            ));
            return;
        }

        foreach ((string kind, CardModel card) in related)
        {
            _relatedCards.AddChild(BuildRelatedCardPreview(kind, card));
        }
    }

    private Control BuildRelatedCardPreview(string kind, CardModel card)
    {
        PanelContainer panel = new()
        {
            CustomMinimumSize = new Vector2(142f, 205f),
        };
        panel.AddThemeStyleboxOverride("panel", CreateRelatedCardStyle());
        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 2);
        panel.AddChild(body);

        Label kindLabel = CreateLabel(kind, 14, Cyan);
        kindLabel.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(kindLabel);

        Control host = new()
        {
            CustomMinimumSize = new Vector2(138f, 160f),
            ClipContents = true,
        };
        body.AddChild(host);

        NCard? cardNode = NCard.Create(card);
        NPreviewCardHolder? holder = cardNode == null
            ? null
            : NPreviewCardHolder.Create(cardNode, true, false);
        if (holder != null)
        {
            holder.SetCardScale(Vector2.One * 0.34f);
            holder.Position = new Vector2(69f, 79f);
            host.AddChild(holder);
            cardNode!.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        }
        else
        {
            cardNode?.QueueFree();
        }

        Label title = CreateLabel(card.Title, 14, Cream);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.TooltipText = card.Title;
        body.AddChild(title);
        return panel;
    }

    private void ShowRecipeList()
    {
        ClearCardPreview();
        _selectedCardType = null;
        _detailView.Visible = false;
        _listView.Visible = true;
    }

    private void ClearCardPreview()
    {
        if (_previewCardHolder != null && GodotObject.IsInstanceValid(_previewCardHolder))
        {
            _previewHost.RemoveChild(_previewCardHolder);
            _previewCardHolder.QueueFree();
        }
        _previewCardHolder = null;
        ClearRelatedCardPreviews();
    }

    private void ClearRelatedCardPreviews()
    {
        foreach (Node child in _relatedCards.GetChildren())
        {
            _relatedCards.RemoveChild(child);
            child.QueueFree();
        }
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

    private static (IReadOnlyList<RecipeViewModel> Recipes,
        IReadOnlyDictionary<Type, CardModel> Cards) LoadRecipes()
    {
        Dictionary<Type, CardModel> cards = ModelDb
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
                group => group.First()
            );

        string NameOf(Type type) =>
            cards.TryGetValue(type, out CardModel? card)
                ? card.Title
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
                resultType,
                materials,
                1,
                $"{resultName} {materialText}",
                false
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
                resultType,
                materials,
                minimumRank,
                $"{resultName} {materialText} {minimumRank}",
                false
            ));
        }

        return (recipes, cards);
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
            BorderColor = new Color("9a8062"),
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
                ? new Color("314249")
                : new Color("202b30"),
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

    private static StyleBoxFlat CreateSmallButtonStyle(bool highlighted) => new()
    {
        BgColor = highlighted ? new Color("34464c") : new Color("202b30"),
        BorderColor = highlighted ? Gold : new Color("6f6251"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
    };

    private static StyleBoxFlat CreateRelatedCardStyle() => new()
    {
        BgColor = new Color("11181b"),
        BorderColor = new Color("59686a"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
    };

    private static Texture2D CreateXuanPaperTexture()
    {
        const int size = 192;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        Color paper = new("172126");
        Color fiber = new("80715b");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            uint hash = (uint)x * 374761393u + (uint)y * 668265263u;
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            float grain = (hash & 255) / 255f;
            float strand = MathF.Abs(MathF.Sin(y * 0.31f + x * 0.027f));
            float amount = grain > 0.972f ? 0.12f : (strand > 0.994f ? 0.055f : 0.009f * grain);
            image.SetPixel(x, y, paper.Lerp(fiber, amount));
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static string FormatDescriptionBbcode(string text) => text
        .Replace("[gold]", "[color=#d0a45e]", StringComparison.Ordinal)
        .Replace("[/gold]", "[/color]", StringComparison.Ordinal)
        .Replace("[blue]", "[color=#69a6a8]", StringComparison.Ordinal)
        .Replace("[/blue]", "[/color]", StringComparison.Ordinal)
        .Replace("[pink]", "[color=#d58aaa]", StringComparison.Ordinal)
        .Replace("[/pink]", "[/color]", StringComparison.Ordinal)
        .Replace("[purple]", "[color=#aa8bc4]", StringComparison.Ordinal)
        .Replace("[/purple]", "[/color]", StringComparison.Ordinal)
        .Replace("[sine]", string.Empty, StringComparison.Ordinal)
        .Replace("[/sine]", string.Empty, StringComparison.Ordinal);

    private static string GetDaoName(AbstractGuZhenRenCard.Dao dao) => dao switch
    {
        AbstractGuZhenRenCard.Dao.GuangDao => T("光道", "Light Path"),
        AbstractGuZhenRenCard.Dao.YanDao => T("炎道", "Fire Path"),
        AbstractGuZhenRenCard.Dao.LiDao => T("力道", "Strength Path"),
        AbstractGuZhenRenCard.Dao.JinDao => T("金道", "Metal Path"),
        AbstractGuZhenRenCard.Dao.TouDao => T("偷道", "Theft Path"),
        AbstractGuZhenRenCard.Dao.MuDao => T("木道", "Wood Path"),
        AbstractGuZhenRenCard.Dao.ShiDao => T("食道", "Food Path"),
        AbstractGuZhenRenCard.Dao.ShaDao => T("杀道", "Killing Path"),
        AbstractGuZhenRenCard.Dao.GuDao => T("骨道", "Bone Path"),
        AbstractGuZhenRenCard.Dao.LuDao => T("律道", "Rule Path"),
        AbstractGuZhenRenCard.Dao.ZhiDao => T("智道", "Wisdom Path"),
        AbstractGuZhenRenCard.Dao.BianHuaDao => T("变化道", "Transformation Path"),
        AbstractGuZhenRenCard.Dao.YinYangDao => T("阴阳道", "Yin-Yang Path"),
        AbstractGuZhenRenCard.Dao.JianDao => T("剑道", "Sword Path"),
        AbstractGuZhenRenCard.Dao.XueDao => T("血道", "Blood Path"),
        AbstractGuZhenRenCard.Dao.YunDao => T("运道", "Luck Path"),
        AbstractGuZhenRenCard.Dao.FengDao => T("风道", "Wind Path"),
        AbstractGuZhenRenCard.Dao.ZhouDao => T("宙道", "Time Path"),
        AbstractGuZhenRenCard.Dao.TuDao => T("土道", "Earth Path"),
        _ => dao.ToString(),
    };

    private static string GetRarityName(CardRarity rarity) => rarity switch
    {
        CardRarity.Common => T("普通", "Common"),
        CardRarity.Uncommon => T("罕见", "Uncommon"),
        CardRarity.Rare => T("稀有", "Rare"),
        CardRarity.Basic => T("基础", "Basic"),
        _ => rarity.ToString(),
    };

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
        Type ResultType,
        IReadOnlyList<Type> MaterialTypes,
        int MinimumMaterialRank,
        string SearchText,
        bool IsGenericBeastRecipe
    );

}
