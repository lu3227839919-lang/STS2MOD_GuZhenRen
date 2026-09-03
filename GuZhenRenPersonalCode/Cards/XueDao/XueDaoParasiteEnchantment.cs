using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterEnchantment]
public sealed class XueDaoParasiteEnchantment : ModEnchantmentTemplate
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedKind { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedRank { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedStage { get; set; }

    public override bool ShowAmount => true;

    public override int DisplayAmount => Rank;

    public override bool HasExtraCardText => false;

    public override EnchantmentAssetProfile AssetProfile =>
        Kind switch
        {
            XueDaoParasiteSystem.ParasiteKind.BloodMoon => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodMoonEnchantment.png"
            ),
            XueDaoParasiteSystem.ParasiteKind.BloodSeed => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodFetusEnchantment.png"
            ),
            _ => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodQiEnchantment.png"
            ),
        };

    internal XueDaoParasiteSystem.ParasiteKind Kind => SavedKind switch
    {
        1 => XueDaoParasiteSystem.ParasiteKind.Ordinary,
        3 => XueDaoParasiteSystem.ParasiteKind.BloodMoon,
        5 => XueDaoParasiteSystem.ParasiteKind.BloodSeed,
        _ => XueDaoParasiteSystem.ParasiteKind.None,
    };

    internal int Rank => Math.Max(1, SavedRank > 0 ? SavedRank : Amount);

    internal int Stage => Math.Max(0, SavedStage);

    internal void Configure(
        XueDaoParasiteSystem.ParasiteKind kind,
        int rank,
        int stage
    )
    {
        AssertMutable();
        SavedKind = (int)kind;
        SavedRank = Math.Clamp(rank, 1, 6);
        SavedStage = Math.Max(0, stage);
        Amount = SavedRank;
    }

    internal void AdvanceTo(int stage)
    {
        AssertMutable();
        SavedStage = Math.Clamp(stage, 1, 3);
    }

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) &&
        XueDaoParasiteSystem.IsEligibleHost(card) &&
        !XueDaoEnchantmentSlotPatch.HasParasiteCarrier(card);
}
