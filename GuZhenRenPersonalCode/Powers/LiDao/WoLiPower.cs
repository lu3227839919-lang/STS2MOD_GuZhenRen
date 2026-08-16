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
public sealed class WoLiPower : ModPowerTemplate
{
    private const string TriggerCountVar = "TriggerCount";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => TriggerCount;

    public int Rank => DynamicVars["Rank"].IntValue;

    public int TriggerCount => Math.Clamp(
        DynamicVars[TriggerCountVar].IntValue,
        0,
        1
    );

    public bool StrengthApplies => Amount > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Rank", 6m),
        new DynamicVar(TriggerCountVar, 0m),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/WoLiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/WoLiPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 6, 9);
        InvokeDisplayAmountChanged();
    }

    internal async Task RecordPhantomTriggerAsync(
        PlayerChoiceContext choiceContext,
        AbstractLiDaoXuYing phantom
    )
    {
        if (Amount <= 0 ||
            phantom is QuanLiXuYing or WoLiXuYing)
        {
            return;
        }

        int next = TriggerCount + 1;
        DynamicVars[TriggerCountVar].BaseValue = next % 2;
        InvokeDisplayAmountChanged();
        if (next < 2)
        {
            return;
        }

        Creature owner = Owner;
        StrengthPower? strength = owner.GetPower<StrengthPower>();
        if (strength == null)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                owner,
                1,
                owner,
                phantom
            );
        }
        else
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                strength,
                1,
                owner,
                phantom
            );
        }
    }
}
