using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XueFuWang : AbstractBloodBatToken
{
    private const string ConsumedRemainsVar = "ConsumedRemains";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Concat(
            [new DynamicVar(ConsumedRemainsVar, 0m)]
        );

    protected override int ExtraBaseHits =>
        DynamicVars[ConsumedRemainsVar].IntValue * 2;

    protected override bool TransfersOnKill =>
        DynamicVars[ConsumedRemainsVar].IntValue >= 2;

    public XueFuWang() : base(2)
    {
    }

    internal void ConfigureConsumedRemains(int amount)
    {
        DynamicVars[ConsumedRemainsVar].BaseValue =
            Math.Clamp(amount, 0, 2);
    }
}
