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
public sealed class BaiZhiXuYing : AbstractLiDaoXuYing
{
    public override LiDaoBeastKind? BeastKind => LiDaoBeastKind.BaiZhi;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("FirstBonus", 0m),
    ];

    public BaiZhiXuYing() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "FirstManifestDamage",
            DynamicVars.Damage.IntValue + DynamicVars["FirstBonus"].IntValue
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    ) => LiDaoBeastEffectExecutor.ExecuteAsync(
        this,
        LiDaoBeastKind.BaiZhi,
        GuRank,
        choiceContext,
        target
    );

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(BaiZhiGu.ChanceAtRank(GuRank) / 100f);
        DynamicVars.Damage.BaseValue = BaiZhiGu.DamageAtRank(GuRank);
        DynamicVars["FirstBonus"].BaseValue =
            BaiZhiGu.FirstManifestBonusAtRank(GuRank);
    }
}
