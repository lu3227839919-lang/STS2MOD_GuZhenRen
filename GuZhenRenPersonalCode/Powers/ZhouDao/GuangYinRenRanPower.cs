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

/// <summary>光阴荏苒伴生能力：蛊虫完成恢复时反哺年华。</summary>
[RegisterPower]
public sealed class GuangYinRenRanPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/GuangYinRenRanPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/GuangYinRenRanPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(
            Entry.ModId + ".zhou_dao.gyrr.rank",
            static () => 1
        );
    private static readonly SavedAttachedState<PowerModel, int>
        TriggerTurnState = new(
            Entry.ModId + ".zhou_dao.gyrr.trigger_turn",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        TriggerCountState = new(
            Entry.ModId + ".zhou_dao.gyrr.trigger_count",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        AcceleratedTurnState = new(
            Entry.ModId + ".zhou_dao.gyrr.accelerated_turn",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 1, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 1, 9);

    internal async Task OnGuRecoveredAsync(
        PlayerChoiceContext choiceContext,
        bool acceleratedBySuiMan
    )
    {
        Player? player = Owner.Player;
        if (player?.PlayerCombatState is not { } state)
        {
            return;
        }

        int turn = state.TurnNumber;
        if (TriggerTurnState[this] != turn)
        {
            TriggerTurnState[this] = turn;
            TriggerCountState[this] = 0;
        }

        int rank = Rank;
        int limit = rank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            _ => 3,
        };

        if (TriggerCountState[this] < limit)
        {
            TriggerCountState[this]++;
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                1,
                sourceCard: null
            );
        }

        if (!acceleratedBySuiMan || rank < 5 ||
            AcceleratedTurnState[this] == turn)
        {
            return;
        }

        AcceleratedTurnState[this] = turn;
        int bonus = rank >= 8 ? 2 : 1;
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            player,
            bonus,
            sourceCard: null
        );
        if (rank >= 9)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }
}
