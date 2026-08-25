using GuZhenRen.Cards;
using GuZhenRen.Cards.ZhouDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Powers.ZhouDao;

/// <summary>月蛊安排在下一回合开始兑现的年华。</summary>
[RegisterPower]
public sealed class YueGuDelayPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/YueGuDelayPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/YueGuDelayPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        SecondPayoutState = new(
            Entry.ModId + ".zhou_dao.yue_gu.second_payout",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    internal static async Task ScheduleAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int nextTurn,
        int followingTurn,
        CardModel sourceCard
    )
    {
        if (nextTurn <= 0 && followingTurn <= 0)
        {
            return;
        }

        YueGuDelayPower? power = player.Creature.GetPower<YueGuDelayPower>();
        if (power == null)
        {
            power = await PowerCmd.Apply<YueGuDelayPower>(
                choiceContext,
                player.Creature,
                Math.Max(0, nextTurn),
                player.Creature,
                sourceCard
            );
        }
        else if (nextTurn > 0)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                nextTurn,
                player.Creature,
                sourceCard
            );
        }

        if (power != null && followingTurn > 0)
        {
            SecondPayoutState[power] += followingTurn;
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        int first = Math.Max(0, Amount);
        int second = Math.Max(0, SecondPayoutState[this]);
        await PowerCmd.Remove(this);

        if (first > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                new ThrowingPlayerChoiceContext(),
                player,
                first,
                sourceCard: null
            );
        }

        if (second > 0)
        {
            await PowerCmd.Apply<YueGuDelayPower>(
                new ThrowingPlayerChoiceContext(),
                player.Creature,
                second,
                player.Creature,
                cardSource: null,
                silent: true
            );
        }
    }
}
