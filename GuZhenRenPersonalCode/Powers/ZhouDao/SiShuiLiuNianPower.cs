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

/// <summary>似水流年伴生能力：每回合第一次岁满生成一张年流。</summary>
[RegisterPower]
public sealed class SiShuiLiuNianPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/SiShuiLiuNianPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/SiShuiLiuNianPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.sishui.rank", static () => 8);
    private static readonly SavedAttachedState<PowerModel, int>
        LastTriggerTurnState = new(
            Entry.ModId + ".zhou_dao.sishui.last_turn",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 8, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 8, 9);

    internal async Task OnSuiManAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is not { } player ||
            player.PlayerCombatState is not { } state ||
            LastTriggerTurnState[this] == state.TurnNumber)
        {
            return;
        }

        LastTriggerTurnState[this] = state.TurnNumber;
        AbstractGuZhenRenCard token = Rank >= 9
            ? GuGeneratedCardFactory.Create<NianLiuPlus>(player, 9)
            : GuGeneratedCardFactory.Create<NianLiu>(player, 8);
        await GuGeneratedCardFactory.AddToHandOrDiscard(token, player);
    }
}
