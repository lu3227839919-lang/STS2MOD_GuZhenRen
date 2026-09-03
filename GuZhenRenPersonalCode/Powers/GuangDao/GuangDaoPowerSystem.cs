using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 新版光道的统一折光入口。卡牌只读取这里已经确定的结果，不能自行
/// 推导上一张牌的类型、重复消费聚光或修改真实折光序号。
/// </summary>
public static class GuangDaoPowerSystem
{
    public static RefractionResult GetRefractionResult(
        CardModel card,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (!ReferenceEquals(card, cardPlay.Card) ||
            !cardPlay.IsFirstInSeries)
        {
            return RefractionResult.None;
        }

        return card.Owner.Creature
            .GetPower<ZheGuangPower>()?
            .GetCurrentResult() ?? RefractionResult.None;
    }

    /// <summary>
    /// 返回卡牌本次折光效果的结算次数。只有主动调用该入口的折光效果牌
    /// 才会消费聚光；黄金月等只记录真实折光，不会空耗聚光。
    /// </summary>
    public static async Task<RefractionResult>
        ResolveRefractionEffectAsync(
            PlayerChoiceContext choiceContext,
            CardModel card,
            CardPlay cardPlay
        )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardPlay);

        RefractionResult result = GetRefractionResult(card, cardPlay);
        if (!result.Triggered)
        {
            return result;
        }

        ZheGuangPower? state = card.Owner.Creature
            .GetPower<ZheGuangPower>();
        if (state == null || state.CurrentEffectWasResolved)
        {
            return state?.GetCurrentResult() ?? result;
        }

        state.MarkCurrentEffectResolved();

        JuGuangPower? focus = card.Owner.Creature
            .GetPower<JuGuangPower>();
        if (focus is not { Amount: > 0 })
        {
            return state.GetCurrentResult();
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            focus,
            -1,
            card.Owner.Creature,
            card
        );

        state.MarkCurrentEffectDoubled();
        return state.GetCurrentResult();
    }

    public static int GetTotalRefractionSerial(Player player) =>
        player.Creature.GetPower<ZheGuangPower>()?
            .TotalRefractionSerial ?? 0;

    public static void ForceNextGuangDaoRefraction(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Creature.GetPower<ZheGuangPower>()?
            .ArmForcedRefraction();
    }

    internal static async Task EnsureZheGuang(Player player)
    {
        if (player.Creature.CombatState == null ||
            player.Creature.GetPower<ZheGuangPower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<ZheGuangPower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            1,
            player.Creature,
            cardSource: null,
            silent: true
        );
    }
}
