using System.Runtime.CompilerServices;

using GuZhenRen.Cards;
using GuZhenRen.Cards.Basic;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 折光：追踪同一玩家本回合打出的上一张标准类型卡牌。
/// 当前牌为光道牌且类型改变时获得 1 点光辉，每回合最多获得 3 点。
/// 上一张牌可以来自任意流派。
/// </summary>
[RegisterPower]
public sealed class ZheGuangPower : ModPowerTemplate
{
    private sealed class EarlyResolutionState
    {
        public int PlayIndex = -1;
        public bool Resolved;
    }

    private static readonly ConditionalWeakTable<
        CardModel,
        EarlyResolutionState
    > EarlyResolutionStates = new();

    private const string PreviousTypeKey = "PreviousCardType";
    private const string GainedThisTurnKey = "GainedThisTurn";
    private const int MaximumGainPerTurn = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    // 内部光道状态：保留战斗钩子，但不产生任何 Power 展示。
    protected override bool IsVisibleInternal => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PreviousTypeKey, (int)CardType.None),
        new DynamicVar(GainedThisTurnKey, 0),
    ];

    public CardType PreviousCardType =>
        (CardType)(int)DynamicVars[PreviousTypeKey].BaseValue;

    public int GuangHuiGainedThisTurn =>
        (int)DynamicVars[GainedThisTurnKey].BaseValue;

    public bool PreviousCardWas(CardType type) =>
        PreviousCardType == type;

    public override Task AfterEnergyReset(Player player)
    {
        if (ReferenceEquals(player, Owner.Player))
        {
            DynamicVars[PreviousTypeKey].BaseValue =
                (int)CardType.None;
            DynamicVars[GainedThisTurnKey].BaseValue = 0;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 自动支付型光道牌会在自己的 OnPlay 前调用此入口，提前结算
    /// “本张牌触发的折光”。这样刚好达到光辉阈值时也能立即耀化。
    /// </summary>
    internal async Task ResolveBeforeAutoSpend(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries ||
            !TryGetTrackedCard(cardPlay, out CardModel card))
        {
            return;
        }

        EarlyResolutionState state =
            EarlyResolutionStates.GetValue(
                card,
                static _ => new EarlyResolutionState()
            );

        if (state.Resolved && state.PlayIndex == cardPlay.PlayIndex)
        {
            return;
        }

        // 即使已经达到光辉上限、实际获得量为 0，也要标记为已提前
        // 结算；否则自动支付把光辉降下来后，AfterCardPlayed 会错误地
        // 再补发一次本应在支付前被上限截断的折光光辉。
        state.PlayIndex = cardPlay.PlayIndex;
        state.Resolved = true;

        await TryGainRefractionGuangHui(choiceContext, card);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries ||
            !TryGetTrackedCard(cardPlay, out CardModel card))
        {
            return;
        }

        bool resolvedEarly =
            EarlyResolutionStates.TryGetValue(
                card,
                out EarlyResolutionState? state
            ) &&
            state.Resolved &&
            state.PlayIndex == cardPlay.PlayIndex;

        if (!resolvedEarly)
        {
            await TryGainRefractionGuangHui(choiceContext, card);
        }
        else
        {
            state!.Resolved = false;
            state.PlayIndex = -1;
        }

        // 无论上一张牌属于哪个流派，都作为下一次折光的类型参照。
        DynamicVars[PreviousTypeKey].BaseValue = (int)card.Type;
    }

    private async Task TryGainRefractionGuangHui(
        PlayerChoiceContext choiceContext,
        CardModel card
    )
    {
        CardType previous = PreviousCardType;
        int gainedThisTurn = GuangHuiGainedThisTurn;

        if (previous == CardType.None ||
            previous == card.Type ||
            gainedThisTurn >= MaximumGainPerTurn ||
            !GuangDaoPowerSystem.IsGuangDaoCard(card))
        {
            return;
        }

        int gained = await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            card,
            1
        );

        if (gained <= 0)
        {
            return;
        }

        DynamicVars[GainedThisTurnKey].BaseValue += gained;
        Flash();
    }

    private bool TryGetTrackedCard(
        CardPlay cardPlay,
        out CardModel card
    )
    {
        card = cardPlay.Card;

        // 记录打出的标准类型牌（攻击/技能/能力），用于折光判定。
        if (!ReferenceEquals(card.Owner, Owner.Player) ||
            card.Type is not (
                CardType.Attack or
                CardType.Skill or
                CardType.Power
            ))
        {
            card = null!;
            return false;
        }

        return true;
    }
}
