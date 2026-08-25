using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class WoLiXuYing : AbstractLiDaoXuYing
{
    private static readonly SavedAttachedState<CardModel, bool>
        HasManifestedState = new(
            Entry.ModId + ".li_dao.wo_li_phantom.manifested",
            static () => false
        );

    public override LiDaoBeastKind? BeastKind => null;

    public override int PhantomSlotCost => 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("RepeatChance", 25m)];

    public WoLiXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        SetDao(Dao.LiDao);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "RepeatChance",
            WanWo.RepeatManifestChanceAtRank(GuRank)
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        HasManifestedState[this] = true;
        SetBaseChance(
            WanWo.RepeatManifestChanceAtRank(GuRank) / 100f
        );
        return WoLiPhantomSystem.ExecuteCopyAsync(
            this,
            choiceContext,
            triggeringPlay,
            target
        );
    }

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    protected override void OnXuYingGuRankLoaded() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(
            HasManifestedState[this]
                ? WanWo.RepeatManifestChanceAtRank(GuRank) / 100f
                : 1f
        );
        DynamicVars["RepeatChance"].BaseValue =
            WanWo.RepeatManifestChanceAtRank(GuRank);
    }
}
