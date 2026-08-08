using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 血道寄生的原生附魔模型。
///
/// 种类、来源转数、阶段和触发次数全部作为 SavedProperty 跟随附魔
/// 存档、克隆和多人序列化。普通附魔与血寄共存时，由
/// XueDaoCompositeEnchantment 承载，因此血寄不会占用玩家可用的
/// 普通附魔栏位。
/// </summary>
[RegisterEnchantment]
public sealed class XueDaoParasiteEnchantment : ModEnchantmentTemplate
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedKind { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedRank { get; set; }

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

    // 血寄正文由 XueDaoParasiteSystem 按阶段动态生成；这里关闭静态
    // extraCardText，避免直接附魔与复合附魔路径重复显示。
    public override bool HasExtraCardText => false;

    public override EnchantmentAssetProfile AssetProfile =>
        Kind switch
        {
            XueDaoParasiteSystem.ParasiteKind.BloodMoon => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodMoonEnchantment.png"
            ),
            XueDaoParasiteSystem.ParasiteKind.BloodFetus => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodFetusEnchantment.png"
            ),
            _ => new(
                IconPath: $"{Entry.ResPath}/images/enchantments/XueDaoParasiteBloodQiEnchantment.png"
            ),
        };

    internal XueDaoParasiteSystem.ParasiteKind Kind
    {
        get
        {
            int value = SavedKind;
            return Enum.IsDefined(
                typeof(XueDaoParasiteSystem.ParasiteKind),
                value
            )
                ? (XueDaoParasiteSystem.ParasiteKind)value
                : XueDaoParasiteSystem.ParasiteKind.None;
        }
    }

    internal int Rank => Math.Max(0, SavedRank > 0 ? SavedRank : Amount);

    internal bool SourceWasUpgraded => SavedSourceWasUpgraded;

    internal int Stage => Math.Max(0, SavedStage);

    internal int TriggersRemaining => Math.Max(0, SavedTriggersRemaining);

    internal int TriggersCompleted => Math.Max(0, SavedTriggersCompleted);

    internal void Configure(
        XueDaoParasiteSystem.ParasiteKind kind,
        int rank,
        bool sourceWasUpgraded,
        int stage,
        int triggersRemaining,
        int triggersCompleted
    )
    {
        AssertMutable();

        SavedKind = (int)kind;
        SavedRank = Math.Max(1, rank);
        SavedSourceWasUpgraded = sourceWasUpgraded;
        SavedStage = Math.Max(0, stage);
        SavedTriggersRemaining = Math.Max(0, triggersRemaining);
        SavedTriggersCompleted = Math.Max(0, triggersCompleted);
        Amount = SavedRank;
    }

    internal void Advance(int completed, int totalStages)
    {
        AssertMutable();

        SavedTriggersCompleted = Math.Clamp(completed, 0, totalStages);
        SavedStage = SavedTriggersCompleted;
        SavedTriggersRemaining = Math.Max(
            0,
            totalStages - SavedTriggersCompleted
        );
    }

    public override bool CanEnchant(CardModel card)
    {
        if (!base.CanEnchant(card))
        {
            return false;
        }

        return XueDaoParasiteSystem.IsEligibleHost(card) &&
            !XueDaoEnchantmentSlotPatch.HasParasiteCarrier(card);
    }
}
