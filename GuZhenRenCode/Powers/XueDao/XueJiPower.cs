using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血寄：记录当前玩家牌组中尚未触发的寄生数量，并负责在宿主牌的
/// 最后一段 CardPlay 后统一触发寄生，避免 Replay 重复结算。
/// </summary>
[RegisterPower]
public sealed class XueJiPower : ModPowerTemplate
{
    private sealed class TriggerState
    {
        public CardModel? ActiveCard { get; set; }
        public List<uint> EnemiesAliveBefore { get; } = [];
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/XueJiPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/XueJiPower-256x256.png"
    );

    protected override object InitInternalData() => new TriggerState();

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner) ||
            !XueDaoParasiteSystem.HasParasite(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        if (Owner.CombatState is not { } combatState)
        {
            return Task.CompletedTask;
        }

        TriggerState state = GetInternalData<TriggerState>();
        state.ActiveCard = cardPlay.Card;
        state.EnemiesAliveBefore.Clear();
        state.EnemiesAliveBefore.AddRange(
            combatState.HittableEnemies
                .Where(enemy => enemy.IsAlive)
                .Select(enemy => enemy.CombatId)
                .OfType<uint>()
        );

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsLastInSeries ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner))
        {
            return;
        }

        TriggerState state = GetInternalData<TriggerState>();
        if (!ReferenceEquals(state.ActiveCard, cardPlay.Card) ||
            !XueDaoParasiteSystem.HasParasite(cardPlay.Card))
        {
            return;
        }

        uint[] enemiesAliveBefore = state.EnemiesAliveBefore.ToArray();
        state.ActiveCard = null;
        state.EnemiesAliveBefore.Clear();

        await XueDaoParasiteSystem.TriggerFromCardPlayAsync(
            choiceContext,
            cardPlay,
            enemiesAliveBefore
        );
    }
}
