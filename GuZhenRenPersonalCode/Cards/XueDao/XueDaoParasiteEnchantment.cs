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

    // 旧快照字段继续注册，保证原网络属性 ID 可以读取。
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private bool SavedSourceWasUpgraded { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedStage { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedTriggersRemaining { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedTriggersCompleted { get; set; }

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

    internal XueDaoParasiteSystem.ParasiteKind Kind =>
        XueDaoParasiteSystem.NormalizePersistedKind(SavedKind);

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
        SavedSourceWasUpgraded = false;
        SavedStage = Math.Max(0, stage);
        SavedTriggersCompleted =
            kind == XueDaoParasiteSystem.ParasiteKind.Ordinary
                ? Math.Clamp(SavedStage - 1, 0, 2)
                : 0;
        SavedTriggersRemaining =
            kind == XueDaoParasiteSystem.ParasiteKind.Ordinary
                ? Math.Clamp(4 - SavedStage, 0, 3)
                : 0;
        Amount = SavedRank;
    }

    internal void AdvanceTo(int stage)
    {
        AssertMutable();
        SavedStage = Math.Clamp(stage, 1, 3);
        SavedTriggersCompleted = SavedStage - 1;
        SavedTriggersRemaining = 4 - SavedStage;
    }

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) &&
        XueDaoParasiteSystem.IsEligibleHost(card) &&
        !XueDaoEnchantmentSlotPatch.HasParasiteCarrier(card);
}
