using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招材料绑定的唯一领域服务。负责封存、解绑、冷却重排，以及
/// 异常移出和战斗结束的兜底；推演选择与支付逻辑不在此处处理。
/// </summary>
internal static class ShaZhaoBindingService
{
    private static readonly SavedAttachedState<CardModel, string>
        MaterialBoundShaZhaoState = new(
            "lu_gu_zhen_ren.sha_zhao.material_bound_sha_zhao",
            static () => string.Empty
        );

    internal enum FinalizeReason
    {
        Completed,
        Dismantled,
        AbnormalRemoval,
        CombatEnd,
    }

    internal static bool IsMaterialSealed(CardModel card) =>
        GuSealSystem.IsShaZhaoMaterialSealed(card);

    /// <summary>
    /// 仅供 GuSealSystem 兼容旧版 QuickSL；不得作为通用封存判断入口。
    /// </summary>
    internal static bool HasMaterialBindingState(CardModel card) =>
        card is IGuWormCard &&
        MaterialBoundShaZhaoState[card].Length > 0;

    internal static string GetMaterialBindingTitle(CardModel material)
    {
        string boundId = MaterialBoundShaZhaoState[material];
        if (boundId.Length == 0)
        {
            return string.Empty;
        }

        AbstractShaZhaoCard? shaZhao = material.Owner?
            .PlayerCombatState?
            .AllCards
            .OfType<AbstractShaZhaoCard>()
            .FirstOrDefault(card => string.Equals(
                card.Id.ToString(),
                boundId,
                StringComparison.Ordinal
            ));
        return shaZhao?.Title ?? boundId;
    }

    internal static async Task MarkMaterialSealedAsync(
        CardModel material,
        CardModel shaZhao
    )
    {
        if (GuSealSystem.IsSealed(material))
        {
            throw new InvalidOperationException(
                $"蛊虫 {material.Id} 已因 " +
                $"{GuSealSystem.GetSealReason(material)} 封存，" +
                "不能再次作为杀招材料。"
            );
        }

        MaterialBoundShaZhaoState[material] = shaZhao.Id.ToString();
        GuSealSystem.SealAsShaZhaoMaterial(material);

        Player player = material.Owner;
        CardPile materialPile =
            GuCardPileSystem.GuSealedPileType.GetPile(player);
        if (!ReferenceEquals(material.Pile, materialPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.GuSealedPileType,
                skipVisuals: false
            );
        }
    }

    /// <summary>
    /// 正常用完、主动解体或异常移出会让材料从零开始完整冷却并额外
    /// 延后一回合；战斗结束只清理战斗期绑定状态。
    /// </summary>
    internal static async Task FinalizeAsync(
        AbstractShaZhaoCard shaZhao,
        Player player,
        FinalizeReason reason
    )
    {
        if (!shaZhao.HasBoundMaterials ||
            shaZhao.MaterialsSealedPermanently)
        {
            return;
        }

        IReadOnlyList<CardModel> materials = shaZhao.BoundMaterials;
        if (reason == FinalizeReason.CombatEnd)
        {
            foreach (CardModel material in materials)
            {
                ClearMaterialBinding(material);
            }
            shaZhao.ClearBoundMaterials();
            return;
        }

        foreach (CardModel material in materials)
        {
            await UnsealMaterialAsync(material, player);
        }
        shaZhao.ClearBoundMaterials();
    }

    internal static void RemoveFromCombatPostfix(
        object[] __args,
        ref Task __result
    )
    {
        AbstractShaZhaoCard? shaZhao = __args
            .OfType<AbstractShaZhaoCard>()
            .FirstOrDefault();
        if (shaZhao == null || !shaZhao.HasBoundMaterials)
        {
            return;
        }

        __result = AwaitRemovalAndFinalizeAsync(
            __result,
            shaZhao,
            shaZhao.Owner
        );
    }

    internal static void AfterCombatEndPrefix(
        IRunState runState,
        CombatRoom room
    )
    {
        foreach (Player player in runState.Players)
        {
            FinalizeAllForCombatEnd(player);
        }
    }

    private static async Task AwaitRemovalAndFinalizeAsync(
        Task removalTask,
        AbstractShaZhaoCard shaZhao,
        Player player
    )
    {
        await removalTask;
        if (shaZhao.HasBoundMaterials)
        {
            await FinalizeAsync(
                shaZhao,
                player,
                FinalizeReason.AbnormalRemoval
            );
        }
    }

    private static void FinalizeAllForCombatEnd(Player player)
    {
        AbstractShaZhaoCard[] shaZhaoCards = player.PlayerCombatState?
            .AllCards
            .OfType<AbstractShaZhaoCard>()
            .Where(static card =>
                card.HasBoundMaterials &&
                !card.MaterialsSealedPermanently
            )
            .ToArray() ?? [];

        foreach (AbstractShaZhaoCard shaZhao in shaZhaoCards)
        {
            foreach (CardModel material in shaZhao.BoundMaterials)
            {
                ClearMaterialBinding(material);
            }
            shaZhao.ClearBoundMaterials();
        }
    }

    private static async Task UnsealMaterialAsync(
        CardModel material,
        Player player
    )
    {
        ClearMaterialBinding(material);

        CardPile recoveryPile =
            GuCardPileSystem.RecoveryPileType.GetPile(player);
        if (!ReferenceEquals(material.Pile, recoveryPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.RecoveryPileType,
                skipVisuals: false
            );
        }

        int currentTurn = player.PlayerCombatState?.TurnNumber ?? 0;
        GuCardUsageRules.ResetRecovery(
            material,
            currentTurn,
            extraTurns: 1
        );
    }

    private static void ClearMaterialBinding(CardModel material)
    {
        MaterialBoundShaZhaoState[material] = string.Empty;
        GuSealSystem.ClearSeal(
            material,
            GuSealReason.ShaZhaoMaterial
        );
    }
}
