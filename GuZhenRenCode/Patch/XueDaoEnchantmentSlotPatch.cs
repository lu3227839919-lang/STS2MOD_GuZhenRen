using System.Reflection;

using Godot;

using HarmonyLib;

using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace GuZhenRen.Patches;

/// <summary>
/// 让血寄使用原生 EnchantmentModel，同时保留一个独立的普通附魔栏位。
///
/// 仅当卡牌带有 XueDaoParasiteEnchantment 时接管 CanEnchant、Enchant、
/// ClearEnchantment、附魔预览和卡面页签；其他卡牌完全走原版逻辑。
/// </summary>
internal static class XueDaoEnchantmentSlotPatch
{
    private const string HarmonyId = Entry.ModId + ".XueDaoEnchantmentSlot";
    private const string ExtraTabPrefix = "GuZhenRenBloodParasiteTab";

    private static readonly StringName TintHue = new("h");
    private static readonly StringName TintSaturation = new("s");
    private static readonly StringName TintValue = new("v");

    private static readonly FieldInfo NCardIconField = RequireField(
        typeof(NCard),
        "_enchantmentIcon"
    );
    private static readonly FieldInfo NCardLabelField = RequireField(
        typeof(NCard),
        "_enchantmentLabel"
    );
    private static readonly FieldInfo NCardDefaultPositionField = RequireField(
        typeof(NCard),
        "_defaultEnchantmentPosition"
    );
    private static readonly FieldInfo EnchantPreviewBeforeField = RequireField(
        typeof(NEnchantPreview),
        "_before"
    );
    private static readonly FieldInfo EnchantPreviewAfterField = RequireField(
        typeof(NEnchantPreview),
        "_after"
    );
    private static readonly MethodInfo EnchantPreviewRemoveCardsMethod =
        RequireMethod(
            typeof(NEnchantPreview),
            "RemoveExistingCards",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
    private static readonly FieldInfo EnchantVfxCardNodeField = RequireField(
        typeof(NCardEnchantVfx),
        "_cardNode"
    );
    private static readonly FieldInfo EnchantVfxIconField = RequireField(
        typeof(NCardEnchantVfx),
        "_enchantmentIcon"
    );
    private static readonly FieldInfo EnchantVfxLabelField = RequireField(
        typeof(NCardEnchantVfx),
        "_enchantmentLabel"
    );
    private static readonly FieldInfo EnchantVfxCardModelField = RequireField(
        typeof(NCardEnchantVfx),
        "_cardModel"
    );
    private static readonly PropertyInfo RestSiteOwnerProperty =
        RequireProperty(
            typeof(RestSiteOption),
            "Owner",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static Func<CardModel, bool>[]? _originalStealPriorities;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            RequireMethod(
                typeof(EnchantmentModel),
                nameof(EnchantmentModel.CanEnchant),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(CardModel)
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(CanEnchantPrefix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(CardCmd),
                nameof(CardCmd.Enchant),
                BindingFlags.Static | BindingFlags.Public,
                typeof(EnchantmentModel),
                typeof(CardModel),
                typeof(decimal)
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(EnchantPrefix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(CardCmd),
                nameof(CardCmd.ClearEnchantment),
                BindingFlags.Static | BindingFlags.Public,
                typeof(CardModel)
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(ClearEnchantmentPrefix)
            )
        );
        harmony.Patch(
            AccessTools.PropertyGetter(
                typeof(EnchantmentModel),
                nameof(EnchantmentModel.HoverTips)
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(HoverTipsPostfix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(CardModel),
                nameof(CardModel.GetDescriptionForPile),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(PileType),
                typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature)
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(DescriptionPostfix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(CardModel),
                nameof(CardModel.GetDescriptionForUpgradePreview),
                BindingFlags.Instance | BindingFlags.Public
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(DescriptionPostfix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(NCard),
                "UpdateEnchantmentVisuals",
                BindingFlags.Instance | BindingFlags.NonPublic
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(UpdateEnchantmentVisualsPrefix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(NEnchantPreview),
                nameof(NEnchantPreview.Init),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(CardModel),
                typeof(EnchantmentModel),
                typeof(int)
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(EnchantPreviewPrefix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(NCardEnchantVfx),
                nameof(NCardEnchantVfx._Ready),
                BindingFlags.Instance | BindingFlags.Public
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(EnchantVfxReadyPostfix)
            )
        );
        harmony.Patch(
            RequireMethod(
                typeof(CloneRestSiteOption),
                nameof(CloneRestSiteOption.OnSelect),
                BindingFlags.Instance | BindingFlags.Public
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoEnchantmentSlotPatch),
                nameof(CloneRestSitePrefix)
            )
        );

        PatchThievingHopperPriorities();
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
            RestoreThievingHopperPriorities();
        }
        finally
        {
            _initialized = false;
        }
    }

    internal static bool HasParasiteCarrier(CardModel? card) =>
        TryGetParasite(card) != null;

    internal static XueDaoParasiteEnchantment? TryGetParasite(CardModel? card)
    {
        return card?.Enchantment switch
        {
            XueDaoParasiteEnchantment parasite => parasite,
            XueDaoCompositeEnchantment composite => composite.Parasite,
            _ => null,
        };
    }

    internal static EnchantmentModel? TryGetRegular(CardModel? card)
    {
        return card?.Enchantment switch
        {
            XueDaoParasiteEnchantment => null,
            XueDaoCompositeEnchantment composite =>
                composite.RegularEnchantment,
            EnchantmentModel regular => regular,
            _ => null,
        };
    }

    internal static XueDaoParasiteEnchantment AttachOrRefreshParasite(
        CardModel card,
        int rank
    )
    {
        card.AssertMutable();

        if (TryGetParasite(card) is { } existing)
        {
            existing.Amount = Math.Max(1, rank);
            return existing;
        }

        XueDaoParasiteEnchantment parasite =
            (XueDaoParasiteEnchantment)
            ModelDb.Enchantment<XueDaoParasiteEnchantment>().ToMutable();

        switch (card.Enchantment)
        {
            case null:
                card.EnchantInternal(parasite, Math.Max(1, rank));
                parasite.ModifyCard();
                break;

            case XueDaoCompositeEnchantment composite:
                parasite = composite.AddParasite(
                    parasite,
                    Math.Max(1, rank)
                );
                break;

            case EnchantmentModel regular:
                XueDaoCompositeEnchantment converted =
                    ConvertToComposite(card, regular);
                parasite = converted.AddParasite(
                    parasite,
                    Math.Max(1, rank)
                );
                break;
        }

        card.FinalizeUpgradeInternal();
        return parasite;
    }

    internal static void RemoveParasite(CardModel card)
    {
        card.AssertMutable();

        switch (card.Enchantment)
        {
            case XueDaoParasiteEnchantment:
                card.ClearEnchantmentInternal();
                break;

            case XueDaoCompositeEnchantment composite:
            {
                XueDaoParasiteEnchantment? parasite =
                    composite.DetachParasite();
                EnchantmentModel? regular = composite.DetachRegular();

                card.ClearEnchantmentInternal();
                parasite?.ClearInternal();

                if (regular != null)
                {
                    regular.ClearInternal();
                    card.EnchantInternal(regular, regular.Amount);
                }
                break;
            }
        }

        card.DynamicVars.RecalculateForUpgradeOrEnchant();
        card.FinalizeUpgradeInternal();
    }

    private static bool CanEnchantPrefix(
        EnchantmentModel __instance,
        CardModel card,
        ref bool __result
    )
    {
        if (!HasParasiteCarrier(card))
        {
            return true;
        }

        if (__instance is XueDaoParasiteEnchantment or
            XueDaoCompositeEnchantment)
        {
            __result = false;
            return false;
        }

        if (card.Type is CardType.Status or CardType.Curse or CardType.Quest ||
            !__instance.CanEnchantCardType(card.Type) ||
            (card.Pile?.Type == PileType.Deck &&
             card.Keywords.Contains(CardKeyword.Unplayable)))
        {
            __result = false;
            return false;
        }

        EnchantmentModel? regular = TryGetRegular(card);
        __result = regular == null ||
            (regular.GetType() == __instance.GetType() &&
             __instance.IsStackable);
        return false;
    }

    private static bool EnchantPrefix(
        EnchantmentModel enchantment,
        CardModel card,
        decimal amount,
        ref EnchantmentModel? __result
    )
    {
        if (!HasParasiteCarrier(card) ||
            enchantment is XueDaoParasiteEnchantment or
                XueDaoCompositeEnchantment)
        {
            return true;
        }

        enchantment.AssertMutable();
        if (!enchantment.CanEnchant(card))
        {
            throw new InvalidOperationException(
                $"Cannot enchant {card.Id} with {enchantment.Id}."
            );
        }

        XueDaoCompositeEnchantment composite =
            card.Enchantment as XueDaoCompositeEnchantment ??
            ConvertToComposite(card, card.Enchantment!);

        __result = composite.AddOrStackRegular(enchantment, amount);
        card.FinalizeUpgradeInternal();
        RecordEnchantmentHistory(card, enchantment.Id);
        return false;
    }

    private static bool ClearEnchantmentPrefix(CardModel card)
    {
        switch (card.Enchantment)
        {
            // 血寄不属于普通附魔栏位；清空普通附魔时保留血寄。
            case XueDaoParasiteEnchantment:
                return false;

            case XueDaoCompositeEnchantment composite:
            {
                EnchantmentModel? regular = composite.DetachRegular();
                if (regular == null)
                {
                    return false;
                }

                XueDaoParasiteEnchantment? parasite =
                    composite.DetachParasite();
                card.ClearEnchantmentInternal();
                regular.ClearInternal();

                if (parasite != null)
                {
                    parasite.ClearInternal();
                    card.EnchantInternal(parasite, parasite.Amount);
                }

                card.DynamicVars.RecalculateForUpgradeOrEnchant();
                card.FinalizeUpgradeInternal();
                return false;
            }

            default:
                return true;
        }
    }

    private static void HoverTipsPostfix(
        EnchantmentModel __instance,
        ref IEnumerable<IHoverTip> __result
    )
    {
        if (__instance is XueDaoParasiteEnchantment parasite &&
            parasite.HasCard &&
            XueDaoParasiteSystem.GetHostCardDynamicText(parasite.Card) is
                { Length: > 0 } dynamicText)
        {
            // EnchantmentModel.DynamicDescription 只会读取固定的
            // enchantments.json。血寄的实际数值取决于附魔存档状态和
            // 宿主，因此在原生附魔页签/悬浮提示处替换成实时说明。
            __result = new IHoverTip[]
            {
                new HoverTip(
                    parasite.Title,
                    NormalizeParasiteText(dynamicText),
                    parasite.Icon
                ),
            };
            return;
        }

        if (__instance is XueDaoCompositeEnchantment composite)
        {
            __result = composite.InnerEnchantments
                .SelectMany(static enchantment => enchantment.HoverTips)
                .ToList();
        }
    }

    private static void DescriptionPostfix(
        CardModel __instance,
        ref string __result
    )
    {
        // 复合附魔时，原版只会追加外层载体的 extraCardText；这里保留
        // 普通附魔的原有效果行。
        if (__instance.Enchantment is XueDaoCompositeEnchantment composite &&
            composite.RegularEnchantment?.DynamicExtraCardText is
                { } extraText)
        {
            AppendDescriptionLine(
                ref __result,
                extraText.GetFormattedText()
            );
        }

        // 直接血寄与复合血寄都走这里，确保卡面、牌堆、升级预览等
        // CardModel 描述路径稳定显示种类/转数/宿主类型/阶段对应的实时效果。
        try
        {
            if (XueDaoParasiteSystem.GetHostCardDynamicText(__instance) is
                { Length: > 0 } parasiteText)
            {
                AppendDescriptionLine(ref __result, parasiteText);
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Error(
                $"[血寄] DescriptionPostfix 动态正文异常：" +
                $"{exception}"
            );
        }
    }

    private static void AppendDescriptionLine(
        ref string description,
        string? text
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string line = NormalizeParasiteText(text);
        if (description.EndsWith(line, StringComparison.Ordinal))
        {
            return;
        }

        description = string.IsNullOrWhiteSpace(description)
            ? line
            : $"{description}\n{line}";
    }

    private static string NormalizeParasiteText(string text)
    {
        text = text
            .Replace(
                "[color=#B388FF]",
                "[purple]",
                StringComparison.OrdinalIgnoreCase
            )
            .Replace(
                "[/color]",
                "[/purple]",
                StringComparison.OrdinalIgnoreCase
            );
        return text.Contains("[purple]", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"[purple]{text}[/purple]";
    }

    private static bool UpdateEnchantmentVisualsPrefix(NCard __instance)
    {
        ClearExtraTabs(__instance);
        if (__instance.Model?.Enchantment is not
            XueDaoCompositeEnchantment composite)
        {
            return true;
        }

        EnchantmentModel? regular = composite.RegularEnchantment;
        XueDaoParasiteEnchantment? parasite = composite.Parasite;
        EnchantmentModel? lead = regular ?? parasite;

        Control tab = __instance.EnchantmentTab;
        if (lead == null)
        {
            tab.Visible = false;
            return false;
        }

        TextureRect icon = (TextureRect)NCardIconField.GetValue(__instance)!;
        MegaLabel label = (MegaLabel)NCardLabelField.GetValue(__instance)!;
        Vector2 defaultPosition =
            (Vector2)NCardDefaultPositionField.GetValue(__instance)!;
        Vector2 basePosition = __instance.Model.HasStarCostX ||
            __instance.Model.CurrentStarCost >= 0
                ? defaultPosition
                : defaultPosition + Vector2.Up * 45f;

        tab.Position = basePosition;
        ConfigureTab(tab, icon, label, lead);

        if (regular != null && parasite != null)
        {
            float spacing = MathF.Max(
                54f,
                (tab.Size.Y > 0f ? tab.Size.Y : 46f) + 6f
            );
            CreateExtraTab(
                __instance,
                tab,
                basePosition + Vector2.Down * spacing,
                parasite
            );
        }

        return false;
    }

    private static bool EnchantPreviewPrefix(
        NEnchantPreview __instance,
        CardModel card,
        EnchantmentModel canonicalEnchantment,
        int amount
    )
    {
        if (!HasParasiteCarrier(card) ||
            canonicalEnchantment is XueDaoParasiteEnchantment or
                XueDaoCompositeEnchantment)
        {
            return true;
        }

        canonicalEnchantment.AssertCanonical();
        EnchantPreviewRemoveCardsMethod.Invoke(__instance, null);

        NCard beforeCard = NCard.Create(card) ??
            throw new InvalidOperationException(
                "Failed to create before-enchantment preview card."
            );
        NPreviewCardHolder beforeHolder = NPreviewCardHolder.Create(
            beforeCard,
            showHoverTips: true,
            scaleOnHover: false
        ) ?? throw new InvalidOperationException(
            "Failed to create before-enchantment preview holder."
        );
        Control before =
            (Control)EnchantPreviewBeforeField.GetValue(__instance)!;
        before.AddChildSafely(beforeHolder);
        beforeHolder.CardNode!.UpdateVisuals(
            card.Pile?.Type ?? PileType.None,
            CardPreviewMode.Normal
        );

        CardModel previewCard = card.CardScope!.CloneCard(card);
        previewCard.IsEnchantmentPreview = true;
        EnchantmentModel previewEnchantment = canonicalEnchantment.ToMutable();
        ApplyRegularEnchantment(previewCard, previewEnchantment, amount, false);

        NCard afterCard = NCard.Create(previewCard) ??
            throw new InvalidOperationException(
                "Failed to create after-enchantment preview card."
            );
        NPreviewCardHolder afterHolder = NPreviewCardHolder.Create(
            afterCard,
            showHoverTips: true,
            scaleOnHover: false
        ) ?? throw new InvalidOperationException(
            "Failed to create after-enchantment preview holder."
        );
        Control after =
            (Control)EnchantPreviewAfterField.GetValue(__instance)!;
        after.AddChildSafely(afterHolder);
        afterHolder.CardNode!.UpdateVisuals(
            PileType.None,
            CardPreviewMode.Normal
        );
        return false;
    }

    private static void EnchantVfxReadyPostfix(NCardEnchantVfx __instance)
    {
        CardModel card =
            (CardModel)EnchantVfxCardModelField.GetValue(__instance)!;
        if (card.Enchantment is not XueDaoCompositeEnchantment composite)
        {
            return;
        }

        EnchantmentModel? displayed =
            composite.RegularEnchantment ?? composite.Parasite;
        if (displayed == null)
        {
            return;
        }

        NCard cardNode =
            (NCard)EnchantVfxCardNodeField.GetValue(__instance)!;
        ClearExtraTabs(cardNode);

        TextureRect icon =
            (TextureRect)EnchantVfxIconField.GetValue(__instance)!;
        MegaLabel label =
            (MegaLabel)EnchantVfxLabelField.GetValue(__instance)!;
        icon.Texture = displayed.Icon;
        label.SetTextAutoSize(displayed.DisplayAmount.ToString());
        label.Visible = displayed.ShowAmount;
    }

    private static bool CloneRestSitePrefix(
        CloneRestSiteOption __instance,
        ref Task<bool> __result
    )
    {
        Player owner = (Player)(RestSiteOwnerProperty.GetValue(__instance) ??
            throw new InvalidOperationException(
                "Could not read rest-site option owner."
            ));
        if (!owner.Deck.Cards.Any(
                static card => HasRegularType<Clone>(card)
            ))
        {
            return true;
        }

        // 只在原版筛选会漏掉“复合载体里的 Clone”时接管。
        if (!owner.Deck.Cards.Any(
                static card =>
                    card.Enchantment is XueDaoCompositeEnchantment &&
                    HasRegularType<Clone>(card)
            ))
        {
            return true;
        }

        __result = CloneCardsAsync(owner);
        return false;
    }

    private static async Task<bool> CloneCardsAsync(Player owner)
    {
        List<CardPileAddResult> results = [];
        foreach (CardModel source in owner.Deck.Cards
                     .Where(static card => HasRegularType<Clone>(card))
                     .ToList())
        {
            CardModel clone = owner.RunState.CloneCard(source);
            results.Add(await CardPileCmd.Add(clone, PileType.Deck));
        }

        CardCmd.PreviewCardPileAdd(
            results,
            1.2f,
            CardPreviewStyle.MessyLayout
        );
        return true;
    }

    private static EnchantmentModel ApplyRegularEnchantment(
        CardModel card,
        EnchantmentModel enchantment,
        decimal amount,
        bool recordHistory
    )
    {
        XueDaoCompositeEnchantment composite =
            card.Enchantment as XueDaoCompositeEnchantment ??
            ConvertToComposite(card, card.Enchantment!);
        EnchantmentModel applied =
            composite.AddOrStackRegular(enchantment, amount);
        card.FinalizeUpgradeInternal();

        if (recordHistory)
        {
            RecordEnchantmentHistory(card, enchantment.Id);
        }
        return applied;
    }

    private static XueDaoCompositeEnchantment ConvertToComposite(
        CardModel card,
        EnchantmentModel existing
    )
    {
        XueDaoCompositeEnchantment composite =
            (XueDaoCompositeEnchantment)
            ModelDb.Enchantment<XueDaoCompositeEnchantment>().ToMutable();

        card.ClearEnchantmentInternal();
        card.EnchantInternal(composite, 1m);
        composite.ImportExisting(existing);
        return composite;
    }

    private static bool HasRegularType<T>(CardModel card)
        where T : EnchantmentModel =>
        TryGetRegular(card) is T;

    private static void RecordEnchantmentHistory(
        CardModel card,
        ModelId enchantmentId
    )
    {
        if (card.Pile?.Type == PileType.Deck)
        {
            card.Owner.RunState.CurrentMapPointHistoryEntry?
                .GetEntry(card.Owner.NetId)
                .CardsEnchanted.Add(
                    new MegaCrit.Sts2.Core.Runs.History.CardEnchantmentHistoryEntry(
                        card,
                        enchantmentId
                    )
                );
        }
    }

    private static void PatchThievingHopperPriorities()
    {
        FieldInfo field = RequireField(
            typeof(ThievingHopper),
            "_stealPriorities",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        if (field.GetValue(null) is not Func<CardModel, bool>[] priorities ||
            priorities.Length != 4)
        {
            throw new InvalidOperationException(
                "Could not read ThievingHopper steal priorities."
            );
        }

        _originalStealPriorities = (Func<CardModel, bool>[])priorities.Clone();
        priorities[0] = static card =>
            !HasRegularType<Imbued>(card) &&
            card.Rarity == CardRarity.Uncommon;
        priorities[1] = static card =>
            !HasRegularType<Imbued>(card) &&
            card.Rarity is CardRarity.Common or CardRarity.Rare or
                CardRarity.Event;
        priorities[2] = static card =>
            !HasRegularType<Imbued>(card) &&
            card.Rarity is CardRarity.Basic or CardRarity.Quest;
        priorities[3] = static card =>
            card.Rarity == CardRarity.Ancient ||
            HasRegularType<Imbued>(card);
    }

    private static void RestoreThievingHopperPriorities()
    {
        if (_originalStealPriorities == null)
        {
            return;
        }

        FieldInfo field = RequireField(
            typeof(ThievingHopper),
            "_stealPriorities",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        if (field.GetValue(null) is Func<CardModel, bool>[] priorities &&
            priorities.Length == _originalStealPriorities.Length)
        {
            Array.Copy(
                _originalStealPriorities,
                priorities,
                priorities.Length
            );
        }
        _originalStealPriorities = null;
    }

    private static void CreateExtraTab(
        NCard cardNode,
        Control sourceTab,
        Vector2 position,
        EnchantmentModel enchantment
    )
    {
        if (sourceTab.GetParent() is not Node parent ||
            sourceTab.Duplicate() is not Control duplicate)
        {
            return;
        }

        duplicate.Name = ExtraTabPrefix;
        duplicate.Material = duplicate.Material?.Duplicate() as Material;
        duplicate.Position = position;
        parent.AddChildSafely(duplicate);

        TextureRect? icon = duplicate.GetNodeOrNull<TextureRect>("Icon") ??
            duplicate.FindChild("Icon", true, false) as TextureRect;
        MegaLabel? label = duplicate.GetNodeOrNull<MegaLabel>("Label") ??
            duplicate.FindChild("Label", true, false) as MegaLabel;
        if (icon == null || label == null)
        {
            parent.RemoveChildSafely(duplicate);
            duplicate.QueueFreeSafely();
            return;
        }

        ConfigureTab(duplicate, icon, label, enchantment);
    }

    private static void ClearExtraTabs(NCard cardNode)
    {
        Node? parent = cardNode.EnchantmentTab.GetParent();
        if (parent == null)
        {
            return;
        }

        foreach (Node child in parent.GetChildren())
        {
            if (!child.Name.ToString().StartsWith(
                    ExtraTabPrefix,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            parent.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }
    }

    private static void ConfigureTab(
        Control tab,
        TextureRect icon,
        MegaLabel label,
        EnchantmentModel enchantment
    )
    {
        tab.Visible = true;
        icon.Texture = enchantment.Icon;
        label.SetTextAutoSize(enchantment.DisplayAmount.ToString());
        label.Visible = enchantment.ShowAmount;
        ApplyStatus(tab, icon, label, enchantment.Status);
    }

    private static void ApplyStatus(
        Control tab,
        TextureRect icon,
        MegaLabel label,
        EnchantmentStatus status
    )
    {
        bool disabled = status == EnchantmentStatus.Disabled;
        tab.Modulate = disabled
            ? new Color(1f, 1f, 1f, 0.9f)
            : Colors.White;

        if (tab.Material is ShaderMaterial material)
        {
            material.SetShaderParameter(TintHue, 0.25);
            material.SetShaderParameter(
                TintSaturation,
                disabled ? 0.1 : 0.4
            );
            material.SetShaderParameter(TintValue, 0.6);
        }

        icon.UseParentMaterial = disabled;
        label.SelfModulate = disabled ? StsColors.gray : Colors.White;
    }

    private static FieldInfo RequireField(
        Type type,
        string name,
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic
    )
    {
        return type.GetField(name, flags) ??
            throw new MissingFieldException(type.FullName, name);
    }

    private static MethodInfo RequireMethod(
        Type type,
        string name,
        BindingFlags flags,
        params Type[] parameters
    )
    {
        return type.GetMethod(
            name,
            flags,
            binder: null,
            parameters,
            modifiers: null
        ) ?? throw new MissingMethodException(type.FullName, name);
    }

    private static PropertyInfo RequireProperty(
        Type type,
        string name,
        BindingFlags flags
    )
    {
        return type.GetProperty(name, flags) ??
            throw new MissingMemberException(type.FullName, name);
    }
}
