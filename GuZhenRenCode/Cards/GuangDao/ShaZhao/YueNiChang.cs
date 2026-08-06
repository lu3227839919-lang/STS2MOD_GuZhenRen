using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Cards.Interfaces;
using GuZhenRen.Cards.TuDao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 月霓裳是由月光蛊与玉皮蛊共同推演的光道防御杀招，
/// 不属于蛊虫，也不占用蛊虫牌组容量。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(YueGuangGu), typeof(YuPiGu))]
[ShaZhaoRecipe(typeof(YuPiGu), typeof(YueGuangGu))]
public sealed class YueNiChang
    : AbstractShaZhaoCard,
      ICarouselCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("GuangHui", 1m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public YueNiChang()
        : base(
            baseCost: 1,
            type: CardType.Skill,
            target: TargetType.Self
        )
    {
        SetDao(Dao.GuangDao);
        RefreshRankValues();
    }

    /// <summary>
    /// 次数型防御杀招：每场最多使用 2 次，用尽后消耗并返还材料。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Charged;

    public override int ShaZhaoMaxUses => 2;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                DynamicVars.Block,
                cardPlay
            );

            await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                this,
                DynamicVars["GuangHui"].IntValue
            );

            if (GuRank < 3 || cardPlay.PlayIndex != 0)
            {
                return;
            }

            AbstractGuZhenRenCard generated = GuRank >= 9
                ? GuGeneratedCardFactory.Create<ManYueRen>(
                    Owner,
                    GuRank,
                    upgraded: IsUpgraded
                )
                : GuGeneratedCardFactory.Create<YueRen>(
                    Owner,
                    GuRank,
                    upgraded: IsUpgraded || GuRank >= 6
                );

            await GuGeneratedCardFactory.AddToHandOrDiscard(
                generated,
                Owner
            );
        }
        finally
        {
            await AdvanceLifecycleAsync(choiceContext);
        }
    }

    public IReadOnlyList<CardModel> GetCarouselCards()
    {
        if (GuRank < 3)
        {
            return [];
        }

        if (GuRank >= 9)
        {
            return
            [
                GuCardReferenceFactory.Create<ManYueRen>(
                    this,
                    IsUpgraded
                ),
            ];
        }

        List<CardModel> cards =
        [
            GuCardReferenceFactory.Create<YueRen>(
                this,
                IsUpgraded || GuRank >= 6
            ),
        ];

        if (GuRank >= 7)
        {
            cards.Add(
                GuCardReferenceFactory.Create<CanYue>(this)
            );
        }

        return cards;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Block.UpgradeValueBy(4m);
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    protected override void OnShaZhaoStateLoaded()
    {
        base.OnShaZhaoStateLoaded();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 1 => 10,
            2 => 12,
            3 => 14,
            4 => 16,
            5 => 18,
            6 => 22,
            7 => 25,
            8 => 28,
            _ => 32,
        };
        DynamicVars["GuangHui"].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 8 => 2,
            _ => 3,
        };
    }
}
