using MegaCrit.Sts2.Core.Models;
using GuZhenRen.Cards.ShaZhao;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>蛊封存堆中一张牌当前被封存的原因。</summary>
public enum GuSealReason
{
    None = 0,
    Training = 1,
    ShaZhaoMaterial = 2,
}

/// <summary>
/// 蛊封存状态的统一入口。牌堆只描述位置；具体规则必须同时检查封存
/// 原因，避免把炼力中的兽力蛊当作杀招材料。
/// </summary>
public static class GuSealSystem
{
    private static readonly SavedAttachedState<CardModel, int>
        SealReasonState = new(
            Entry.ModId + ".gu_seal.reason",
            static () => (int)GuSealReason.None
        );

    public static bool IsSealed(CardModel card) =>
        GetSealReason(card) != GuSealReason.None;

    public static GuSealReason GetSealReason(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not IGuWormCard)
        {
            return GuSealReason.None;
        }

        int stored = SealReasonState[card];
        if (Enum.IsDefined(typeof(GuSealReason), stored) &&
            stored != (int)GuSealReason.None)
        {
            return (GuSealReason)stored;
        }

        // 旧版战斗存档只有杀招绑定字符串，没有通用封存原因。保留这个
        // 只读回退，让旧 QuickSL 在本次战斗中仍能正确识别材料封存。
        return ShaZhaoBindingService.HasMaterialBindingState(card)
            ? GuSealReason.ShaZhaoMaterial
            : GuSealReason.None;
    }

    public static bool IsTrainingSealed(CardModel card) =>
        GetSealReason(card) == GuSealReason.Training;

    public static bool IsShaZhaoMaterialSealed(CardModel card) =>
        GetSealReason(card) == GuSealReason.ShaZhaoMaterial;

    internal static void SealForTraining(CardModel card) =>
        SetSealReason(card, GuSealReason.Training);

    internal static void SealAsShaZhaoMaterial(CardModel card) =>
        SetSealReason(card, GuSealReason.ShaZhaoMaterial);

    internal static void ClearSeal(
        CardModel card,
        GuSealReason expectedReason
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (GetSealReason(card) == expectedReason)
        {
            SealReasonState[card] = (int)GuSealReason.None;
        }
    }

    private static void SetSealReason(
        CardModel card,
        GuSealReason reason
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not IGuWormCard)
        {
            throw new ArgumentException(
                "只有蛊虫牌可以进入蛊封存状态。",
                nameof(card)
            );
        }

        SealReasonState[card] = (int)reason;
    }
}
