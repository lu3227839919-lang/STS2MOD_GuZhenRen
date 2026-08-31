using GuZhenRen.Cards;

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
/// 隐藏的玩家级折光战斗状态。所有会影响结算的字段均保存在 Power 的
/// DynamicVars 中，使读档、克隆与多人快照使用同一份战斗真值。
/// </summary>
[RegisterPower]
public sealed class ZheGuangPower : ModPowerTemplate
{
    private const string PreviousTypeKey = "PreviousCardType";
    private const string TotalSerialKey = "TotalRefractionSerial";
    private const string ForceNextKey = "ForceNextGuangDaoRefraction";
    private const string CurrentTriggeredKey = "CurrentTriggered";
    private const string CurrentEffectCountKey = "CurrentEffectCount";
    private const string CurrentEffectResolvedKey = "CurrentEffectResolved";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PreviousTypeKey, (int)CardType.None),
        new DynamicVar(TotalSerialKey, 0),
        new DynamicVar(ForceNextKey, 0),
        new DynamicVar(CurrentTriggeredKey, 0),
        new DynamicVar(CurrentEffectCountKey, 0),
        new DynamicVar(CurrentEffectResolvedKey, 0),
    ];

    public CardType PreviousCardType =>
        (CardType)DynamicVars[PreviousTypeKey].IntValue;

    public int TotalRefractionSerial =>
        Math.Max(0, DynamicVars[TotalSerialKey].IntValue);

    internal bool CurrentEffectWasResolved =>
        DynamicVars[CurrentEffectResolvedKey].IntValue != 0;

    public override Task AfterEnergyReset(Player player)
    {
        if (ReferenceEquals(player, Owner.Player))
        {
            DynamicVars[PreviousTypeKey].BaseValue =
                (int)CardType.None;
            ClearCurrentResult();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.IsAutoPlay ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner) ||
            !IsStandardType(cardPlay.Card.Type))
        {
            return Task.CompletedTask;
        }

        ClearCurrentResult();
        if (!cardPlay.IsFirstInSeries)
        {
            return Task.CompletedTask;
        }

        CardModel card = cardPlay.Card;
        bool isGuangDao = GuangDaoPowerSystem.IsGuangDaoCard(card);
        bool forced = isGuangDao &&
            DynamicVars[ForceNextKey].IntValue != 0;

        // 下一张光道牌处理到统一入口时即消费标记。自然折光与强制
        // 折光同时成立也只产生一个真实折光事件。
        if (forced)
        {
            DynamicVars[ForceNextKey].BaseValue = 0;
        }

        bool natural = isGuangDao &&
            PreviousCardType != CardType.None &&
            PreviousCardType != card.Type;
        bool triggered = natural || forced;
        if (!triggered)
        {
            return Task.CompletedTask;
        }

        DynamicVars[CurrentTriggeredKey].BaseValue = 1;
        DynamicVars[CurrentEffectCountKey].BaseValue = 1;
        DynamicVars[TotalSerialKey].BaseValue =
            TotalRefractionSerial + 1;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsAutoPlay &&
            ReferenceEquals(cardPlay.Player.Creature, Owner) &&
            cardPlay.IsLastInSeries &&
            IsStandardType(cardPlay.Card.Type))
        {
            // Replay 的全部段落完成后才提交真实出牌历史。
            DynamicVars[PreviousTypeKey].BaseValue =
                (int)cardPlay.Card.Type;
            ClearCurrentResult();
        }

        return Task.CompletedTask;
    }

    internal RefractionResult GetCurrentResult() => new(
        DynamicVars[CurrentTriggeredKey].IntValue != 0,
        Math.Max(0, DynamicVars[CurrentEffectCountKey].IntValue)
    );

    internal void MarkCurrentEffectResolved()
    {
        DynamicVars[CurrentEffectResolvedKey].BaseValue = 1;
    }

    internal void MarkCurrentEffectDoubled()
    {
        DynamicVars[CurrentEffectCountKey].BaseValue = 2;
    }

    internal void ArmForcedRefraction()
    {
        DynamicVars[ForceNextKey].BaseValue = 1;
    }

    private void ClearCurrentResult()
    {
        DynamicVars[CurrentTriggeredKey].BaseValue = 0;
        DynamicVars[CurrentEffectCountKey].BaseValue = 0;
        DynamicVars[CurrentEffectResolvedKey].BaseValue = 0;
    }

    private static bool IsStandardType(CardType type) => type is
        CardType.Attack or CardType.Skill or CardType.Power;
}
