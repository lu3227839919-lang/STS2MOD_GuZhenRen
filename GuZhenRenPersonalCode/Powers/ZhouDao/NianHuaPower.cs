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

[RegisterPower]
public sealed class NianHuaPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/NianHuaPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/NianHuaPower-256x256.png"
    );
    public const int MaximumAmount = 6;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount
    )
    {
        modifiedAmount = amount;
        if (canonicalPower is not NianHuaPower ||
            !ReferenceEquals(target, Owner) ||
            amount <= 0)
        {
            return false;
        }

        modifiedAmount = Math.Min(
            amount,
            Math.Max(0, MaximumAmount - Amount)
        );
        return modifiedAmount != amount;
    }
}
