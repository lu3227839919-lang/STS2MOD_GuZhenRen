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
public sealed class EXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.E;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
        new DynamicVar("SecondHitBonus", 0m),
    ];

    public EXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "SecondHitDamage",
            DynamicVars.Damage.IntValue + DynamicVars["SecondHitBonus"].IntValue
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.E,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(ELiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = ELiGu.DamageAtRank(GuRank);
        DynamicVars["Hits"].BaseValue = 2m;
        DynamicVars["SecondHitBonus"].BaseValue =
            ELiGu.HitBonusAtRank(GuRank, 1);
    }
}
