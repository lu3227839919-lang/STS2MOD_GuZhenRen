namespace GuZhenRen.Cards;

/// <summary>
/// 允许蛊虫在进入恢复堆、恢复期间与恢复完成时产生分转效果。
/// 这些回调只负责战斗内临时效果，不改变永久牌组。
/// </summary>
public interface IGuRecoveryEffectSource
{
    void ResetRecoveryEffectState();

    Task OnEnteredRecoveryAsync();

    Task OnRecoveryTurnStartAsync(int turnNumber);

    Task OnRecoveredAsync();
}
