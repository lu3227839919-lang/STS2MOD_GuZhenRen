namespace GuZhenRen.Cards;

/// <summary>
/// Marker for Blood Path Gu attack cards whose individual hits may each
/// consume one Blood Mark. Cards without this marker can trigger Blood Mark
/// at most once per CardPlay, regardless of hit count.
/// </summary>
public interface IBloodMarkPerHitTrigger
{
}
