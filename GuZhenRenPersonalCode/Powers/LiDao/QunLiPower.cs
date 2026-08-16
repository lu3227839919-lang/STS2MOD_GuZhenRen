using System.Reflection;

using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Powers.LiDao;

[RegisterPower]
public sealed class QunLiPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Math.Max(0, Amount);

    public int Rank => DynamicVars["Rank"].IntValue;

    public int GroupChancePercent => QunLiGu.GroupChanceAtRank(Rank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 6m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/QunLiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/QunLiPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 6, 9);
        InvokeDisplayAmountChanged();
    }

    internal bool TryRollRepeat(Player owner)
    {
        if (Amount <= 0 || GroupChancePercent <= 0)
        {
            return false;
        }

        return RitsuLibFramework.GetModPlayerRng(
                owner,
                Entry.ModId,
                "li_dao/qun_li_repeat"
            )
            .NextInt(100) < GroupChancePercent;
    }
}
