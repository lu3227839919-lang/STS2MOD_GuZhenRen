using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class YueGuangGu
    : AbstractGuWormCard,
      IRefractionEffectCard,
      IRefractionRelevantCard
{
    public override int MinimumAvailableGuRank => 1;

    public override int MaxGuRank => 2;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new DynamicVar("RefractionDamage", 4),
    ];

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public YueGuangGu()
        : base(
            0,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        RefractionResult refraction =
            await GuangDaoPowerSystem.ResolveRefractionEffectAsync(
                choiceContext,
                this,
                cardPlay
            );

        await AttackAsync(
            choiceContext,
            cardPlay,
            target,
            DynamicVars.Damage.BaseValue
        );

        for (int resolution = 0;
             resolution < refraction.EffectResolutionCount &&
             !target.IsDead;
             resolution++)
        {
            await AttackAsync(
                choiceContext,
                cardPlay,
                target,
                DynamicVars["RefractionDamage"].BaseValue
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private async Task AttackAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Creature target,
        decimal damage
    )
    {
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank >= 2 ? 8 : 6;
        DynamicVars["RefractionDamage"].BaseValue =
            GuRank >= 2 ? 5 : 4;
    }
}
