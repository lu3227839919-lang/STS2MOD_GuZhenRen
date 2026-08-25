using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class GuZhenRenStrike
    : ModCardTemplate, ICardRewardExcluded
{
    protected override HashSet<CardTag> CanonicalTags =>
        [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4m, ValueProp.Move)];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    // GuZhenRenCardPool is an auxiliary pool rather than the character's
    // primary pool, so CardModel.Pool cannot discover it through
    // ModelDb.AllCardPools. Return it explicitly to keep rendering and
    // multiplayer combat from failing when this starter card is inspected.
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public GuZhenRenStrike()
        : base(
            baseCost: 1,
            type: CardType.Attack,
            rarity: CardRarity.Basic,
            target: TargetType.AnyEnemy
        )
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);

        Creature? target = cardPlay.Target;

        // 使用行动队列中已经同步的目标。直接取敌人集合首项会忽略玩家
        // 实际点击的敌人，并可能在多人端集合顺序不一致时造成分歧。
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
