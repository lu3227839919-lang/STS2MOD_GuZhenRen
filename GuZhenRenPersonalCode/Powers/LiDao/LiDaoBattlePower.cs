using System.Numerics;

using GuZhenRen.Cards;
using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Combat;
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

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

[RegisterPower]
public sealed class LiDaoBattlePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ReferenceEquals(cardPlay.Player.Creature, Owner)
        ? LiDaoPhantomSystem.ResolveAttackAsync(choiceContext, cardPlay)
        : Task.CompletedTask;
}
