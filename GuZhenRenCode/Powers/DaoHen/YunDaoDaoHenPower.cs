using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 运道道痕。
///
/// 每层使所属玩家的概率卡牌触发率增加 3%。
///
/// 例如：
///
/// - 飞熊虚影基础概率为 25%；
/// - 拥有 2 层运道道痕；
/// - 最终概率为 31%。
///
/// 最终概率由概率卡牌自行限制在 0%～100%。
/// </summary>
[RegisterPower]
public sealed class YunDaoDaoHenPower
    : AbstractDaoHenPower,
      IProbabilityModifier
{

    /// <summary>
    /// 当前能力使用的图标资源。
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

/// <summary>
    /// 每层增加 3%。
    /// </summary>
    private const float ProbabilityPerStack =
        0.03f;

    /// <summary>
    /// 对所属玩家的概率卡牌增加触发率。
    /// </summary>
    public float GetAdditiveProbability(
        CardModel card
    )
    {
        if (!card.IsMutable ||
            card.Owner == null ||
            !ReferenceEquals(
                card.Owner.Creature,
                Owner
            ))
        {
            return 0f;
        }

        return Amount *
               ProbabilityPerStack;
    }
}
