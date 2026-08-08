using System.Text.Json;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 只为“一个普通附魔 + 一个血寄附魔”提供的复合载体。
///
/// 原版 CardModel 只有一个 Enchantment 属性。本模型占用该物理属性，
/// 但把普通附魔和血寄分别保存并转发，因此从玩法上血寄不消耗普通
/// 附魔栏位。实现思路参考 RepeatableEnchantments，并限制在血寄宿主，
/// 不会把全局规则改成任意附魔可重复。
/// </summary>
[RegisterEnchantment]
public sealed class XueDaoCompositeEnchantment : ModEnchantmentTemplate
{
    private List<EnchantmentModel> _innerEnchantments = [];
    private List<EnchantmentModel> _subscribedEnchantments = [];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private string? SavedEnchantmentsJson
    {
        get => _innerEnchantments.Count == 0
            ? null
            : JsonSerializer.Serialize(
                _innerEnchantments
                    .Select(static enchantment => enchantment.ToSerializable())
                    .ToArray()
            );
        set
        {
            UnsubscribeAll();
            _innerEnchantments = [];

            if (!string.IsNullOrWhiteSpace(value))
            {
                SerializableEnchantment[]? serialized =
                    JsonSerializer.Deserialize<SerializableEnchantment[]>(value);
                if (serialized != null)
                {
                    foreach (SerializableEnchantment item in serialized)
                    {
                        _innerEnchantments.Add(
                            EnchantmentModel.FromSerializable(item)
                        );
                    }
                }
            }

            Amount = _innerEnchantments.Count;
            RefreshCompositeStatus();
        }
    }

    public override bool HasExtraCardText => false;
    public override bool ShowAmount => false;

    public override EnchantmentAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoCompositeEnchantment.png"
    );

    public override bool ShouldStartAtBottomOfDrawPile
    {
        get
        {
            EnsureInnerBindings();
            if (HasCard && Card.Keywords.Contains(CardKeyword.Innate))
            {
                return false;
            }

            return _innerEnchantments.Any(
                static enchantment => enchantment.ShouldStartAtBottomOfDrawPile
            );
        }
    }

    public override bool ShouldGlowGold
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments.Any(
                static enchantment => enchantment.ShouldGlowGold
            );
        }
    }

    public override bool ShouldGlowRed
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments.Any(
                static enchantment => enchantment.ShouldGlowRed
            );
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments
                .SelectMany(static enchantment => enchantment.HoverTips)
                .ToList();
        }
    }

    internal IReadOnlyList<EnchantmentModel> InnerEnchantments
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments;
        }
    }

    internal XueDaoParasiteEnchantment? Parasite
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments
                .OfType<XueDaoParasiteEnchantment>()
                .FirstOrDefault();
        }
    }

    internal EnchantmentModel? RegularEnchantment
    {
        get
        {
            EnsureInnerBindings();
            return _innerEnchantments.FirstOrDefault(
                static enchantment =>
                    enchantment is not XueDaoParasiteEnchantment
            );
        }
    }

    public override bool CanEnchant(CardModel card) => false;

    internal void ImportExisting(EnchantmentModel enchantment)
    {
        AssertMutable();
        EnsureCompositeCard();

        if (enchantment.HasCard)
        {
            if (!ReferenceEquals(enchantment.Card, Card))
            {
                enchantment.ClearInternal();
                enchantment.ApplyInternal(Card, enchantment.Amount);
            }
        }
        else
        {
            enchantment.ApplyInternal(Card, enchantment.Amount);
        }

        _innerEnchantments.Add(enchantment);
        Subscribe(enchantment);
        Amount = _innerEnchantments.Count;
        RefreshCompositeStatus();
    }

    internal EnchantmentModel AddOrStackRegular(
        EnchantmentModel enchantment,
        decimal amount
    )
    {
        AssertMutable();
        EnsureCompositeCard();
        EnsureInnerBindings();

        EnchantmentModel? existing = RegularEnchantment;
        if (existing != null)
        {
            if (existing.GetType() != enchantment.GetType() ||
                !enchantment.IsStackable)
            {
                throw new InvalidOperationException(
                    $"Card {Card.Id} already has regular enchantment " +
                    $"{existing.Id}; cannot add {enchantment.Id}."
                );
            }

            existing.Amount += (int)amount;
            existing.RecalculateValues();
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
            RefreshCompositeStatus();
            return existing;
        }

        enchantment.ApplyInternal(Card, amount);
        _innerEnchantments.Insert(0, enchantment);
        Subscribe(enchantment);
        Amount = _innerEnchantments.Count;
        enchantment.ModifyCard();
        RefreshCompositeStatus();
        return enchantment;
    }

    internal XueDaoParasiteEnchantment AddParasite(
        XueDaoParasiteEnchantment parasite,
        decimal amount
    )
    {
        AssertMutable();
        EnsureCompositeCard();
        EnsureInnerBindings();

        if (Parasite is { } existing)
        {
            existing.Amount = (int)amount;
            return existing;
        }

        parasite.ApplyInternal(Card, amount);
        _innerEnchantments.Add(parasite);
        Subscribe(parasite);
        Amount = _innerEnchantments.Count;
        parasite.ModifyCard();
        RefreshCompositeStatus();
        return parasite;
    }

    internal EnchantmentModel? DetachRegular()
    {
        EnchantmentModel? regular = RegularEnchantment;
        if (regular == null)
        {
            return null;
        }

        Unsubscribe(regular);
        _innerEnchantments.Remove(regular);
        Amount = _innerEnchantments.Count;
        RefreshCompositeStatus();
        return regular;
    }

    internal XueDaoParasiteEnchantment? DetachParasite()
    {
        XueDaoParasiteEnchantment? parasite = Parasite;
        if (parasite == null)
        {
            return null;
        }

        Unsubscribe(parasite);
        _innerEnchantments.Remove(parasite);
        Amount = _innerEnchantments.Count;
        RefreshCompositeStatus();
        return parasite;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _innerEnchantments = _innerEnchantments
            .Select(static enchantment =>
                (EnchantmentModel)enchantment.ClonePreservingMutability()
            )
            .ToList();
        _subscribedEnchantments = [];
    }

    protected override void OnEnchant()
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            enchantment.ModifyCard();
        }

        Amount = _innerEnchantments.Count;
        RefreshCompositeStatus();
    }

    public override void RecalculateValues()
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            enchantment.RecalculateValues();
        }

        Amount = _innerEnchantments.Count;
        if (HasCard)
        {
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
        }
        RefreshCompositeStatus();
    }

    public override decimal EnchantBlockAdditive(decimal originalBlock)
    {
        EnsureInnerBindings();
        return CalculateFinalBlock(originalBlock) - originalBlock;
    }

    public override decimal EnchantBlockMultiplicative(decimal originalBlock) => 1m;

    public override decimal EnchantDamageAdditive(
        decimal originalDamage,
        ValueProp props
    )
    {
        EnsureInnerBindings();
        return CalculateFinalDamage(originalDamage, props) - originalDamage;
    }

    public override decimal EnchantDamageMultiplicative(
        decimal originalDamage,
        ValueProp props
    ) => 1m;

    public override int EnchantPlayCount(int originalPlayCount)
    {
        EnsureInnerBindings();
        int current = originalPlayCount;
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            current = enchantment.EnchantPlayCount(current);
        }
        return current;
    }

    public override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay? cardPlay
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            await enchantment.OnPlay(choiceContext, cardPlay);
            enchantment.InvokeExecutionFinished();
        }
        RefreshCompositeStatus();
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            // Goopy writes directly through DeckVersion.Enchantment.Amount;
            // a composite carrier therefore needs the same compatibility path
            // used by the reference repeatable-enchantment implementation.
            if (enchantment is Goopy goopy)
            {
                HandleGoopyAfterCardPlayed(goopy, cardPlay);
                continue;
            }

            await enchantment.AfterCardPlayed(choiceContext, cardPlay);
        }
        RefreshCompositeStatus();
    }

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            await enchantment.AfterCardDrawn(choiceContext, card, fromHandDraw);
        }
        RefreshCompositeStatus();
    }

    public override async Task AfterAutoPrePlayPhaseEntered(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            await enchantment.AfterAutoPrePlayPhaseEntered(choiceContext, player);
        }
        RefreshCompositeStatus();
    }

    public override async Task BeforeFlush(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            await enchantment.BeforeFlush(choiceContext, player);
        }
        RefreshCompositeStatus();
    }

    public override void ModifyShuffleOrder(
        Player player,
        List<CardModel> cards,
        bool isInitialShuffle
    )
    {
        EnsureInnerBindings();
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            enchantment.ModifyShuffleOrder(player, cards, isInitialShuffle);
        }
    }

    private decimal CalculateFinalBlock(decimal originalBlock)
    {
        decimal current = originalBlock;
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            current += enchantment.EnchantBlockAdditive(current);
            current *= enchantment.EnchantBlockMultiplicative(current);
        }
        return current;
    }

    private decimal CalculateFinalDamage(decimal originalDamage, ValueProp props)
    {
        decimal current = originalDamage;
        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            current += enchantment.EnchantDamageAdditive(current, props);
            current *= enchantment.EnchantDamageMultiplicative(current, props);
        }
        return current;
    }

    private void HandleGoopyAfterCardPlayed(Goopy goopy, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, goopy.Card))
        {
            return;
        }

        goopy.Amount++;
        goopy.RecalculateValues();

        if (goopy.Card.DeckVersion?.Enchantment is
            XueDaoCompositeEnchantment deckComposite &&
            deckComposite.RegularEnchantment is Goopy deckGoopy)
        {
            deckGoopy.Amount++;
            deckGoopy.RecalculateValues();
            deckComposite.RefreshCompositeStatus();
        }
        else if (goopy.Card.DeckVersion?.Enchantment is Goopy directDeckGoopy)
        {
            directDeckGoopy.Amount++;
            directDeckGoopy.RecalculateValues();
        }

        goopy.Card.DynamicVars.RecalculateForUpgradeOrEnchant();
    }

    private void EnsureCompositeCard()
    {
        if (!HasCard)
        {
            throw new InvalidOperationException(
                "Composite enchantment must be attached to a card first."
            );
        }
    }

    private void EnsureInnerBindings()
    {
        if (!HasCard)
        {
            return;
        }

        foreach (EnchantmentModel enchantment in _innerEnchantments)
        {
            if (!enchantment.HasCard || !ReferenceEquals(enchantment.Card, Card))
            {
                if (enchantment.HasCard)
                {
                    enchantment.ClearInternal();
                }
                enchantment.ApplyInternal(Card, enchantment.Amount);
            }
            Subscribe(enchantment);
        }
    }

    private void Subscribe(EnchantmentModel enchantment)
    {
        if (_subscribedEnchantments.Contains(enchantment))
        {
            return;
        }

        enchantment.StatusChanged += OnInnerStatusChanged;
        _subscribedEnchantments.Add(enchantment);
    }

    private void Unsubscribe(EnchantmentModel enchantment)
    {
        enchantment.StatusChanged -= OnInnerStatusChanged;
        _subscribedEnchantments.Remove(enchantment);
    }

    private void UnsubscribeAll()
    {
        foreach (EnchantmentModel enchantment in _subscribedEnchantments)
        {
            enchantment.StatusChanged -= OnInnerStatusChanged;
        }
        _subscribedEnchantments.Clear();
    }

    private void OnInnerStatusChanged() => RefreshCompositeStatus();

    internal void RefreshCompositeStatus()
    {
        Status = _innerEnchantments.Any(
            static enchantment => enchantment.Status == EnchantmentStatus.Normal
        )
            ? EnchantmentStatus.Normal
            : EnchantmentStatus.Disabled;
    }
}
