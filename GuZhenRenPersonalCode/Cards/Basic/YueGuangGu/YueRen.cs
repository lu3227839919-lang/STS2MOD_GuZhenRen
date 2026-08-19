using GuZhenRen.Characters;
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
public sealed class YueRen : AbstractGuZhenRenGeneratedCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("ZhaoPoBonus", 4m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [
            CardKeyword.Exhaust,
            GuZhenRenKeywords.ZhaoXi,
        ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public YueRen()
        : base(
            baseCost: 1,
            type: CardType.Attack,
            rarity: CardRarity.Token,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "MarkedDamage",
            DynamicVars.Damage.IntValue +
                DynamicVars["ZhaoPoBonus"].IntValue
        );
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

        bool hadZhaoPo = target.GetPower<ZhaoPoPower>() is
            { Amount: > 0 };
        decimal damage = DynamicVars.Damage.BaseValue +
            (hadZhaoPo
                ? DynamicVars["ZhaoPoBonus"].BaseValue
                : 0);

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (GuRank >= 7 &&
            hadZhaoPo &&
            cardPlay.PlayIndex == 0)
        {
            CanYue crescent =
                GuGeneratedCardFactory.Create<CanYue>(
                    Owner,
                    GuRank
                );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                crescent,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["ZhaoPoBonus"].UpgradeValueBy(1m);
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
            <= 3 => 8,
            4 => 9,
            5 => 11,
            6 => 12,
            7 => 13,
            8 => 15,
            _ => 18,
        };
        DynamicVars["ZhaoPoBonus"].BaseValue = GuRank >= 8
            ? 6
            : 4;
    }
}


