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

/// <summary>宙锚伴生能力：岁满后保留一部分年华进度。</summary>
[RegisterPower]
public sealed class ZhouMaoPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/ZhouMaoPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/ZhouMaoPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.zhou_mao.rank", static () => 6);
    private static readonly SavedAttachedState<PowerModel, int>
        SuiManCounterState = new(
            Entry.ModId + ".zhou_dao.zhou_mao.counter",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 6, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 6, 9);

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
        int gain = rank switch
        {
            6 => 1,
            7 => 2,
            8 => 2,
            _ => 3,
        };
        int count = ++SuiManCounterState[this];
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            player,
            gain,
            sourceCard
        );
        if (rank >= 8 && count % 2 == 0)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }
}
