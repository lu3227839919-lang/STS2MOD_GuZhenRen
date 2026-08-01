using System;
using System.Threading.Tasks;

using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;

using GuZhenRen.Cards;
using GuZhenRen.Cards.Basic;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 闪耀。
///
/// 每层使光道卡牌的正常攻击伤害提高 50%。
///
/// 若同时拥有光道道痕，还会把光道道痕每层 25% 的倍率
/// 合并到最终乘区中。
///
/// 使用光道攻击牌后，按顺序处理：
///
/// - 有小光蛊提供的额外作用次数：消耗 1 次，保留闪耀；
/// - 否则有日光：消耗 1 层日光，保留闪耀；
/// - 否则移除全部闪耀。
/// </summary>
[RegisterPower]
public sealed class ShanYaoPower
    : ModPowerTemplate
{
    public const decimal MultiplierPerStack =
        0.50m;

    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-256x256.png"
        );

    /// <summary>
    /// 闪耀和光道道痕的合并伤害倍率。
    /// </summary>
    public override decimal
        ModifyDamageMultiplicative(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay
        )
    {
        if (!ReferenceEquals(
                dealer,
                Owner
            ) ||
            !props.IsPoweredAttack() ||
            cardSource is not
                GuCard guCard ||
            guCard.CurrentDao !=
                GuCard.Dao
                    .GuangDao)
        {
            return 1m;
        }

        int daoHenAmount =
            Owner.GetPowerAmount<
                GuangDaoDaoHenPower
            >();

        return 1m +
               Amount *
               MultiplierPerStack +
               daoHenAmount *
               GuangDaoDaoHenPower
                   .MultiplierPerStack;
    }

    /// <summary>
    /// 使用光道攻击牌后消耗额外作用次数、日光或闪耀。
    ///
    /// 部分光道攻击牌会先造成伤害，随后才生成新的闪耀与额外作用次数。
    /// 对实现 IShanYaoGeneratingAttack 的牌，本方法只消耗其打出前已有
    /// 的资源，避免新生成的资源被同一张牌追溯消耗。
    /// </summary>
    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay
        );

        if (!cardPlay.IsFirstInSeries ||
            cardPlay.Card.Type !=
                CardType.Attack ||
            cardPlay.Card is not
                GuCard guCard ||
            guCard.CurrentDao !=
                GuCard.Dao
                    .GuangDao ||
            !ReferenceEquals(
                guCard.Owner.Creature,
                Owner
            ))
        {
            return;
        }

        IShanYaoGeneratingAttack?
            generatingAttack =
                guCard as
                    IShanYaoGeneratingAttack;

        int shanYaoBeforePlay =
            Amount;

        int extraUsesBeforePlay =
            Owner.GetPowerAmount<
                XiaoGuangGuShanYaoUsesPower
            >();

        if (generatingAttack != null)
        {
            (
                shanYaoBeforePlay,
                extraUsesBeforePlay
            ) =
                generatingAttack
                    .TakeShanYaoStateBeforePlay();

            // 打出前没有闪耀时，本次攻击没有使用闪耀。
            // 本牌刚获得的闪耀和额外作用次数全部保留。
            if (shanYaoBeforePlay <= 0)
            {
                return;
            }
        }

        XiaoGuangGuShanYaoUsesPower?
            protectedUses =
                Owner.GetPower<
                    XiaoGuangGuShanYaoUsesPower
                >();

        // 普通光道攻击牌可以使用当前全部额外次数。
        // 小光蛊只能使用它打出前已经存在的次数，
        // 不能让本牌新增加的次数追溯保护自身。
        bool canUseProtectedUse =
            protectedUses != null &&
            protectedUses.Amount > 0 &&
            (
                generatingAttack == null ||
                extraUsesBeforePlay > 0
            );

        if (canUseProtectedUse)
        {
            protectedUses!
                .FlashForConsumption();

            await PowerCmd.Decrement(
                protectedUses
            );

            return;
        }

        RiGuangPower? riGuang =
            Owner.GetPower<RiGuangPower>();

        if (riGuang != null)
        {
            riGuang.FlashForConsumption();

            await PowerCmd.Decrement(
                riGuang
            );

            return;
        }

        // 普通光道攻击牌会消耗全部闪耀。
        if (generatingAttack == null)
        {
            Flash();

            await PowerCmd.Remove(
                this
            );

            return;
        }

        // 生成闪耀的光道攻击牌只消耗打出前已有的闪耀，
        // 保留本牌在伤害结算后新获得的闪耀。
        int amountToConsume =
            Math.Min(
                Math.Max(
                    0,
                    shanYaoBeforePlay
                ),
                Math.Max(
                    0,
                    Amount
                )
            );

        if (amountToConsume <= 0)
        {
            return;
        }

        Flash();

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -amountToConsume,
            Owner,
            guCard,
            silent: false
        );
    }

    /// <summary>
    /// 每次正向获得闪耀时，增加本场战斗累计计数。
    /// 初次施加与后续叠加都会经过此钩子。
    /// </summary>
    public override async Task
        AfterPowerAmountChanged(
            PlayerChoiceContext choiceContext,
            PowerModel power,
            decimal amount,
            Creature? applier,
            CardModel? cardSource
        )
    {
        if (!ReferenceEquals(
                power,
                this
            ) ||
            amount <= 0m)
        {
            return;
        }

        ShanYaoGainedTrackerPower? tracker =
            Owner.GetPower<
                ShanYaoGainedTrackerPower
            >();

        if (tracker == null)
        {
            await PowerCmd.Apply<
                ShanYaoGainedTrackerPower
            >(
                choiceContext,
                Owner,
                amount,
                applier,
                cardSource,
                silent: true
            );
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            tracker,
            amount,
            applier,
            cardSource,
            silent: true
        );
    }
}
