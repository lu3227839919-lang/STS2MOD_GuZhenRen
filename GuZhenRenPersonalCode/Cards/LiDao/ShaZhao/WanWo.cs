using GuZhenRen.Cards.LiDao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 万我：把当前兽力虚影和兽力蛊收束为我力虚影。
///
/// 配方：群力蛊 + 我力蛊。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(QunLiGu), typeof(WoLiGu))]
[ShaZhaoRecipe(typeof(WoLiGu), typeof(QunLiGu))]
public sealed class WanWo : AbstractShaZhaoCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Nv", 0m),
        new DynamicVar("Ng", 0m),
        new DynamicVar("WoLiCount", 1m),
        new DynamicVar("TempHpPerShadow", 5m),
        new DynamicVar("FirstManifestChance", 100m),
        new DynamicVar("RepeatManifestChance", 25m),
        new DynamicVar("CopyEffectRatio", 50m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public override bool GainsBlock => false;

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public WanWo()
        : base(
            baseCost: 1,
            type: CardType.Power,
            target: TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        RefreshRankValues();
    }

    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Staged;

    public override int MaxStages => 2;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Player owner = Owner;
        int rank = Math.Clamp(GuRank, 1, 9);

        AbstractLiDaoXuYing[] beastPhantoms =
            LiDaoPhantomSystem.GetPermanentPhantoms(owner)
                .Where(phantom => phantom is not WoLiXuYing)
                .ToArray();

        ILiDaoBeastGuCard[] beastGu = EnumerateGuCards(owner)
            .OfType<ILiDaoBeastGuCard>()
            .ToArray();

        int nv = beastPhantoms.Length;
        int ng = beastGu
            .Select(card => card.GetType())
            .Distinct()
            .Count();
        int shadowCount = Math.Max(1, (nv + ng) / 2 + 1);

        DynamicVars["Nv"].BaseValue = nv;
        DynamicVars["Ng"].BaseValue = ng;
        DynamicVars["WoLiCount"].BaseValue = shadowCount;
        DynamicVars["RepeatManifestChance"].BaseValue =
            RepeatManifestChanceAtRank(rank);

        try
        {
            foreach (AbstractLiDaoXuYing phantom in beastPhantoms)
            {
                await CardPileCmd.RemoveFromCombat(
                    phantom,
                    skipVisuals: false
                );
            }

            foreach (CardModel gu in beastGu)
            {
                await GuCardPileSystem.MoveCardToPileAsync(
                    gu,
                    GuCardPileSystem.GuSealedPileType,
                    skipVisuals: false
                );
            }

            await WoLiPhantomSystem.AddShadowsAsync(
                choiceContext,
                owner,
                rank,
                shadowCount
            );
            await LiDaoPhantomSystem.EnsureControllerAsync(
                choiceContext,
                this
            );
        }
        finally
        {
            await AdvanceLifecycleAsync(choiceContext);
        }
    }

    internal static int RepeatManifestChanceAtRank(int rank) => rank switch
    {
        <= 5 => 0,
        6 => 25,
        7 => 30,
        8 => 35,
        _ => 40,
    };

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        int rank = Math.Clamp(GuRank, 1, 9);
        DynamicVars["RepeatManifestChance"].BaseValue =
            RepeatManifestChanceAtRank(rank);
    }

    private static IEnumerable<CardModel> EnumerateGuCards(Player owner)
    {
        foreach (CardModel card in GuCardPileSystem.PileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in GuCardPileSystem.StoragePileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in GuCardPileSystem.RecoveryPileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in GuCardPileSystem.GuSealedPileType.GetPile(owner).Cards)
        {
            yield return card;
        }
    }
}
