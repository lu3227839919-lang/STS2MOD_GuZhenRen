namespace GuZhenRen.Cards;

/// <summary>
/// 标记“先使用已有闪耀造成攻击，随后再生成新闪耀”的光道攻击牌。
///
/// 闪耀能力会在本次出牌完成后读取出牌前快照，只消耗旧资源，
/// 避免卡牌刚生成的闪耀与额外作用次数被自身追溯消耗。
///
/// 快照完全来自同步战斗状态，不读取本地玩家或客户端 UI 状态。
/// </summary>
internal interface IShanYaoGeneratingAttack
{
    (
        int ShanYaoAmount,
        int ExtraUses
    ) TakeShanYaoStateBeforePlay();
}
