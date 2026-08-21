using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.Runtime;

public sealed class TribulationEventRouter(TribulationRegistry registry, TribulationRuntime runtime)
{
    public async Task OnPlayerTurnStartAsync(Player player, int turn)
    {
        TribulationContext? c = runtime.TryBuildActiveContext(player);
        if (c == null) return;
        if (registry.GetRequired(c.Selection.TribulationId) is ITribulationTurnLifecycle x)
            await x.OnPlayerTurnStartAsync(c, turn);
    }

    public async Task OnPlayerTurnEndAsync(Player player, int turn)
    {
        TribulationContext? c = runtime.TryBuildActiveContext(player);
        if (c == null) return;
        if (registry.GetRequired(c.Selection.TribulationId) is ITribulationTurnLifecycle x)
            await x.OnPlayerTurnEndAsync(c, turn);
    }

    public async Task OnCardPlayedAsync(Player player, CardModel card)
    {
        TribulationContext? c = runtime.TryBuildActiveContext(player);
        if (c == null) return;
        if (registry.GetRequired(c.Selection.TribulationId) is ITribulationCardObserver x)
            await x.OnCardPlayedAsync(c, card);
    }

    public async Task OnGuActivatedAsync(Player player, CardModel card)
    {
        TribulationContext? c = runtime.TryBuildActiveContext(player);
        if (c == null) return;
        if (registry.GetRequired(c.Selection.TribulationId) is ITribulationGuObserver x)
            await x.OnGuActivatedAsync(c, card);
    }

    public async Task OnYuanQiSpentAsync(Player player, int amount)
    {
        TribulationContext? c = runtime.TryBuildActiveContext(player);
        if (c == null) return;
        if (registry.GetRequired(c.Selection.TribulationId) is ITribulationResourceObserver x)
            await x.OnYuanQiSpentAsync(c, amount);
    }
}
