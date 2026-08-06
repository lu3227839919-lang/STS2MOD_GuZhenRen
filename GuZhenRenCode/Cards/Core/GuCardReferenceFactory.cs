using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace GuZhenRen.Cards;

/// <summary>
/// 为卡牌悬停引用创建与来源牌当前转数、升级状态一致的预览模型。
/// 预览模型只用于 UI，不会进入牌组或战斗状态。
/// </summary>
internal static class GuCardReferenceFactory
{
    internal static T Create<T>(
        AbstractGuZhenRenCard source,
        bool upgraded = false
    ) where T : AbstractGuZhenRenCard
    {
        CardModel mutable = ModelDb.Card<T>().ToMutable();
        if (mutable is not T preview)
        {
            throw new InvalidOperationException(
                $"Unable to create card reference preview for {typeof(T).FullName}."
            );
        }

        preview.InitializeGuRankFromSource(source.GuRank);

        if (upgraded &&
            preview is not AbstractGuWormCard &&
            !preview.IsUpgraded)
        {
            CardCmd.Upgrade(preview, CardPreviewStyle.None);
        }

        return preview;
    }
}
