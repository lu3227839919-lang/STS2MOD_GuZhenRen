using System.Runtime.CompilerServices;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class ShiGuiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.ShiGui;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("NoBlockBonus", 0m),
    ];

    public ShiGuiXuYing() : base(CardType.Skill, TargetType.Self) =>
        RefreshRankValues();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "NoBlockTotal",
            DynamicVars.Block.IntValue + DynamicVars["NoBlockBonus"].IntValue
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.ShiGui,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(ShiGuiLiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Block.BaseValue = ShiGuiLiGu.BlockAtRank(GuRank);
        DynamicVars["NoBlockBonus"].BaseValue =
            ShiGuiLiGu.NoBlockBonusAtRank(GuRank);
    }
}
