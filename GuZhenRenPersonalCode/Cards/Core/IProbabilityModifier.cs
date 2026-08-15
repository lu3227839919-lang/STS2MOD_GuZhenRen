using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 可以修改概率卡牌触发率的模型接口。
///
/// 当前由运道道痕等能力实现。
/// 返回值为加法概率，例如：
///
/// 0.03f = 增加 3%
/// -0.10f = 降低 10%
/// </summary>
public interface IProbabilityModifier
{
    /// <summary>
    /// 取得对指定卡牌的加法概率修正。
    /// </summary>
    /// <param name="card">
    /// 正在进行概率判定的卡牌。
    /// </param>
    float GetAdditiveProbability(
        CardModel card
    );
}
