using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(YueGuangGu),
    typeof(XiaoGuangGu),
    typeof(XiaoGuangGu),
    MinimumMaterialRank = 2
)]
public sealed class YueMangGu
    : AbstractHeLianGuCard,
      IRefractionEffectCard,
      IRefractionRelevantCard,
      IJuGuangCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 6;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1, ValueProp.Move),
        new RepeatVar("BaseHits", 5),
        new DynamicVar("RefractionBonusHits", 3),
        new DynamicVar("JuGuang", 1),
    ];

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public YueMangGu()
        : base(
            0,
            CardType.Attack,
            CardRarity.Uncommon,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        SetGuRank(3);
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
        int hitCount = DynamicVars["BaseHits"].IntValue +
            DynamicVars["RefractionBonusHits"].IntValue *
            refraction.EffectResolutionCount;

        for (int hit = 0; hit < hitCount && !target.IsDead; hit++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (refraction.Triggered)
        {
            await PowerCmd.Apply<JuGuangPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["JuGuang"].IntValue,
                Owner.Creature,
                this
            );
        }
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count != 3 ||
            materials.OfType<IGuRankProvider>()
                .Any(provider => provider.GuRank < 2))
        {
            throw new InvalidOperationException(
                "月芒蛊需要三只至少二转的指定合练材料。"
            );
        }

        return 3;
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 5 => 2,
            _ => 3,
        };
        DynamicVars["BaseHits"].BaseValue = 5;
        DynamicVars["RefractionBonusHits"].BaseValue = 3;
    }
}
