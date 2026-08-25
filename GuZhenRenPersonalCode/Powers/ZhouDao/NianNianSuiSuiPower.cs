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

/// <summary>年年岁岁伴生能力：按回合稳定推动年华。</summary>
[RegisterPower]
public sealed class NianNianSuiSuiPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/NianNianSuiSuiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/NianNianSuiSuiPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.nnss.rank", static () => 1);
    private static readonly SavedAttachedState<PowerModel, int>
        TurnCounterState = new(
            Entry.ModId + ".zhou_dao.nnss.turn_counter",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        SuiManCounterState = new(
            Entry.ModId + ".zhou_dao.nnss.sui_man_counter",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 1, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 1, 9);

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (Owner.Player is not { } player || side != Owner.Side)
        {
            return;
        }

        int rank = Rank;
        int counter = ++TurnCounterState[this];
        int gain = rank switch
        {
            1 when counter % 3 == 0 => 1,
            2 or 3 when counter % 2 == 0 => 1,
            4 or 5 or 6 or 7 => 1,
            >= 8 => 2,
            _ => 0,
        };

        if (gain > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                gain,
                sourceCard: null
            );
        }
    }

    internal async Task OnSuiManAsync(
        PlayerChoiceContext choiceContext,
        CardModel? sourceCard
    )
    {
        if (Owner.Player is not { } player)
        {
            return;
        }

        int rank = Rank;
        int count = ++SuiManCounterState[this];
        bool gain = rank switch
        {
            5 => count == 1,
            6 => count % 2 == 0,
            7 or 9 => true,
            _ => false,
        };
        if (gain)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                1,
                sourceCard
            );
        }
    }
}
