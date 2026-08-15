using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class RiYun : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TotalZhaoPo", 6m),
        new DynamicVar("TargetCap", 4m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public RiYun() : base(2, CardType.Skill, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? primary = cardPlay.Target;
        if (primary == null || !IsValidTarget(primary) ||
            Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => !enemy.IsDead)
            .ToArray();

        int remaining = DynamicVars["TotalZhaoPo"].IntValue;
        int primaryAmount = Math.Min(
            remaining,
            DynamicVars["TargetCap"].IntValue
        );
        await GuangDaoPowerSystem.ApplyZhaoPo(
            choiceContext,
            this,
            primary,
            primaryAmount
        );
        remaining -= primaryAmount;

        Creature[] secondaryTargets = targets
            .Where(enemy => !ReferenceEquals(enemy, primary))
            .ToArray();

        // 剩余额度按确定性顺序轮流分配；只有一个目标时，
        // 受单体上限约束而未使用的额度会自然舍弃。
        for (int index = 0; remaining > 0 && secondaryTargets.Length > 0; index++)
        {
            Creature enemy = secondaryTargets[index % secondaryTargets.Length];
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                enemy,
                1
            );
            remaining--;
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["TotalZhaoPo"].UpgradeValueBy(2m);
    }
}
