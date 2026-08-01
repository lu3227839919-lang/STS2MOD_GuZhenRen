using System.Reflection;

using Godot;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace GuZhenRen.Patches;

/// <summary>
/// Collapses duplicate Gu cards in the activation selector and decorates each
/// representative with a stack-count badge.
///
/// Cards are grouped by their stable model ID rather than localized title so
/// multiplayer clients using different languages still build identical lists.
/// A group whose cards have different upgrade/enchantment states opens a
/// second selector, allowing the player to choose the exact strengthened
/// version. Within every group/version, the highest-rank card is preferred.
/// </summary>
internal static class GuCardStackSelectionPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuCardStackSelection";

    private const string BadgeNodeName =
        "GuZhenRenStackCountBadge";

    private static readonly object BadgeSync = new();

    private static readonly Dictionary<CardModel, int> BadgeCounts =
        new(ReferenceEqualityComparer.Instance);

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo create =
            AccessTools.DeclaredMethod(
                typeof(NGridCardHolder),
                nameof(NGridCardHolder.Create),
                [typeof(NCard)]
            ) ?? throw new MissingMethodException(
                typeof(NGridCardHolder).FullName,
                "Create(NCard)"
            );

        MethodInfo reassigned =
            AccessTools.DeclaredMethod(
                typeof(NGridCardHolder),
                "OnCardReassigned"
            ) ?? throw new MissingMethodException(
                typeof(NGridCardHolder).FullName,
                "OnCardReassigned()"
            );

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                create,
                postfix: new HarmonyMethod(
                    typeof(GuCardStackSelectionPatch),
                    nameof(CreatePostfix)
                )
            );

            harmony.Patch(
                reassigned,
                postfix: new HarmonyMethod(
                    typeof(GuCardStackSelectionPatch),
                    nameof(CardReassignedPostfix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            ClearBadgeCounts();
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            ClearBadgeCounts();
            _initialized = false;
        }
    }

    /// <summary>
    /// Selects one Gu card through a compact first page and, only when needed,
    /// a strengthened-version detail page.
    /// </summary>
    internal static async Task<CardModel?> SelectOne(
        PlayerChoiceContext choiceContext,
        CardPile pile,
        Player player,
        CardSelectorPrefs prefs,
        Func<CardModel, bool> filter
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(pile);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(filter);

        List<CardModel> validCards =
            pile.Cards.Where(filter).ToList();

        if (validCards.Count == 0)
        {
            return null;
        }

        List<CardStack> nameStacks =
            validCards
                .GroupBy(CardNameKey, StringComparer.Ordinal)
                .Select(group => CreateStack(group.ToList()))
                .OrderByDescending(stack => GetGuRank(stack.Representative))
                .ThenBy(stack => CardNameKey(stack.Representative), StringComparer.Ordinal)
                .ToList();

        CardModel? selectedRepresentative;

        using (BeginBadgeScope(nameStacks))
        {
            selectedRepresentative =
                (
                    await CardSelectCmd.FromSimpleGrid(
                        choiceContext,
                        nameStacks
                            .Select(stack => stack.Representative)
                            .ToList(),
                        player,
                        prefs
                    )
                ).FirstOrDefault();
        }

        if (selectedRepresentative == null)
        {
            return null;
        }

        CardStack selectedNameStack =
            nameStacks.First(
                stack => ReferenceEquals(
                    stack.Representative,
                    selectedRepresentative
                )
            );

        List<CardStack> strengthenedVersions =
            selectedNameStack.Cards
                .GroupBy(GetStrengtheningKey)
                .Select(group => CreateStack(group.ToList()))
                .OrderByDescending(stack => GetGuRank(stack.Representative))
                .ThenBy(
                    stack => GetStrengtheningKey(stack.Representative)
                )
                .ToList();

        // Identical copies require no second click: the highest-rank copy is
        // already the representative shown on the first page.
        if (strengthenedVersions.Count <= 1)
        {
            return selectedNameStack.Representative;
        }

        using (BeginBadgeScope(strengthenedVersions))
        {
            return (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    strengthenedVersions
                        .Select(stack => stack.Representative)
                        .ToList(),
                    player,
                    prefs
                )
            ).FirstOrDefault();
        }
    }

    private static CardStack CreateStack(List<CardModel> cards)
    {
        CardModel representative =
            cards
                .OrderByDescending(GetGuRank)
                .ThenBy(GetStrengtheningKey)
                .First();

        return new CardStack(representative, cards);
    }

    private static string CardNameKey(CardModel card) =>
        card.Id.ToString();

    private static StrengtheningKey GetStrengtheningKey(CardModel card)
    {
        EnchantmentModel? enchantment = card.Enchantment;

        return new StrengtheningKey(
            card.CurrentUpgradeLevel,
            enchantment?.Id.ToString() ?? string.Empty,
            enchantment?.Amount ?? 0,
            enchantment?.Status.ToString() ?? string.Empty
        );
    }

    private static int GetGuRank(CardModel card) =>
        card is IGuRankProvider provider
            ? provider.GuRank
            : 0;

    private static IDisposable BeginBadgeScope(
        IEnumerable<CardStack> stacks
    )
    {
        Dictionary<CardModel, int> counts =
            stacks
                .Where(stack => stack.Cards.Count > 1)
                .ToDictionary<CardStack, CardModel, int>(
                    stack => stack.Representative,
                    stack => stack.Cards.Count,
                    ReferenceEqualityComparer.Instance
                );

        lock (BadgeSync)
        {
            foreach ((CardModel card, int count) in counts)
            {
                BadgeCounts[card] = count;
            }
        }

        return new BadgeScope(counts.Keys);
    }

    private static void CreatePostfix(
        ref NGridCardHolder? __result
    )
    {
        if (__result != null)
        {
            UpdateBadge(__result);
        }
    }

    private static void CardReassignedPostfix(
        NGridCardHolder __instance
    )
    {
        UpdateBadge(__instance);
    }

    private static void UpdateBadge(NGridCardHolder holder)
    {
        CardModel? card = holder.CardModel;
        int count = 0;

        if (card != null)
        {
            lock (BadgeSync)
            {
                BadgeCounts.TryGetValue(card, out count);
            }
        }

        Label? existingBadge =
            holder.GetNodeOrNull<Label>(BadgeNodeName);

        if (count <= 1)
        {
            if (existingBadge != null)
            {
                existingBadge.Text = string.Empty;
                existingBadge.Visible = false;
            }

            return;
        }

        Label badge = existingBadge ?? GetOrCreateBadge(holder);

        badge.Text = $"脳{count}";
        badge.Visible = true;
        holder.MoveChild(badge, holder.GetChildCount() - 1);
    }

    private static Label GetOrCreateBadge(NGridCardHolder holder)
    {
        Label? badge = holder.GetNodeOrNull<Label>(BadgeNodeName);

        if (badge != null)
        {
            return badge;
        }

        StyleBoxFlat background = new()
        {
            BgColor = new Color(0.08f, 0.06f, 0.04f, 0.94f),
            BorderColor = new Color(0.93f, 0.73f, 0.22f, 1f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusBottomLeft = 18,
        };

        badge = new Label
        {
            Name = BadgeNodeName,
            Position = new Vector2(67f, -205f),
            Size = new Vector2(88f, 52f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100,
        };

        badge.AddThemeStyleboxOverride("normal", background);
        badge.AddThemeFontSizeOverride("font_size", 30);
        badge.AddThemeColorOverride(
            "font_color",
            new Color(1f, 0.91f, 0.56f, 1f)
        );
        badge.AddThemeColorOverride(
            "font_outline_color",
            new Color(0.05f, 0.03f, 0.02f, 1f)
        );
        badge.AddThemeConstantOverride("outline_size", 6);

        holder.AddChild(badge);
        return badge;
    }

    private static void ClearBadgeCounts()
    {
        lock (BadgeSync)
        {
            BadgeCounts.Clear();
        }
    }

    private readonly record struct CardStack(
        CardModel Representative,
        List<CardModel> Cards
    );

    private readonly record struct StrengtheningKey(
        int UpgradeLevel,
        string EnchantmentId,
        int EnchantmentAmount,
        string EnchantmentStatus
    ) : IComparable<StrengtheningKey>
    {
        public int CompareTo(StrengtheningKey other)
        {
            int comparison =
                UpgradeLevel.CompareTo(other.UpgradeLevel);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                EnchantmentId,
                other.EnchantmentId,
                StringComparison.Ordinal
            );

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                EnchantmentAmount.CompareTo(other.EnchantmentAmount);

            return comparison != 0
                ? comparison
                : string.Compare(
                    EnchantmentStatus,
                    other.EnchantmentStatus,
                    StringComparison.Ordinal
                );
        }
    }

    private sealed class BadgeScope(
        IEnumerable<CardModel> cards
    ) : IDisposable
    {
        private readonly CardModel[] _cards = cards.ToArray();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (BadgeSync)
            {
                foreach (CardModel card in _cards)
                {
                    BadgeCounts.Remove(card);
                }
            }

            _disposed = true;
        }
    }
}
