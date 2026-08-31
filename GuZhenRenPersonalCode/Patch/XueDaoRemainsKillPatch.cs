using System.Runtime.CompilerServices;

using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 只为血寄自身造成的致命伤害生成遗骸。计数绑定到 CardPlay，避免把
/// 宿主攻击的本体伤害误判成血道伤害，并将同一出牌系列限制为最多2张。
/// </summary>
internal static class XueDaoRemainsKillPatch
{
    private const int MaxRemainsPerPlay = 2;

    private sealed class GrantState
    {
        internal int Granted { get; set; }
    }

    private static ConditionalWeakTable<CardPlay, GrantState> _grants = new();

    internal static void Initialize()
    {
    }

    internal static void Uninitialize()
    {
        _grants = new ConditionalWeakTable<CardPlay, GrantState>();
    }

    internal static async Task GrantForBloodDamageKillAsync(
        CardPlay? cardPlay,
        CardModel sourceCard,
        Creature target,
        bool wasAlive
    )
    {
        if (cardPlay == null ||
            cardPlay.IsAutoPlay ||
            !wasAlive ||
            !target.IsDead ||
            !target.IsEnemy)
        {
            return;
        }

        GrantState state = _grants.GetOrCreateValue(cardPlay);
        if (state.Granted >= MaxRemainsPerPlay)
        {
            return;
        }

        // 先占用配额，再进入异步牌堆命令，避免同一结算重入时越过上限。
        state.Granted++;
        await XueDaoCardSystem.AddRemains(sourceCard.Owner, 1);
    }
}
