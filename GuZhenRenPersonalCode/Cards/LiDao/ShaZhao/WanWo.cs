using GuZhenRen.Cards;
using GuZhenRen.Cards.LiDao;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 万我：力道 Rare 能力杀招，固定配方为群力蛊 + 我力蛊。
///
/// 推演万我时，群力蛊与我力蛊作为配方材料永久封存进蛊封存堆
/// （不返还、不进入消耗堆）；手牌中生成 1 张万我。
///
/// 打出万我时：
/// 1. 快照 Nv（当前兽力虚影数量，不包含我力虚影）与 Ng（不同兽力蛊种类数）。
/// 2. 仅消耗所有兽力虚影；已有我力虚影保留。将所有兽力蛊移入蛊封存堆。
/// 3. 我力虚影数量 = 1 + Nv + ⌊max(0, Ng − Nv) ÷ 2⌋。
/// 4. 每个我力虚影提供 5 点临时生命，首次显化必定成功，
///    之后按转数 25/30/35/40% 显化，显化时复制动作 50% 效果。
///
/// 万我本体为能力牌，使用原版能力牌逻辑：不消耗、不返还材料。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(QunLiGu), typeof(WoLiGu))]
public sealed class WanWo : AbstractShaZhaoCard
{
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Staged;

    public override int MaxStages => 2;

    /// <summary>
    /// 配方材料（群力蛊/我力蛊）在推演时即永久封存进蛊封存堆，
    /// 不参与后续返还、不进入消耗堆。
    /// </summary>
    public override bool MaterialsSealedPermanently =>
        true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BeastPhantomCount", 0m),
        new DynamicVar("BeastGuTypeCount", 0m),
        new DynamicVar("WoLiPhantomCount", 0m),
        new DynamicVar("WoLiPhantomTempHp", 5m),
        new DynamicVar("FirstManifestChance", 100m),
        new DynamicVar("RepeatManifestChance", 25m),
        new DynamicVar("CopyEffectRatio", 50m),
    ];

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

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("WoLiPhantomTempHp", 5);
        description.Add(
            "RepeatManifestChance",
            RepeatManifestChanceAtRank(GuRank)
        );
        description.Add("CopyEffectRatio", 50);

        int shadowCount = 1;
        if (CombatState != null)
        {
            CardModel[] beastGu = EnumerateGuCards(Owner)
                .Where(card => card is ILiDaoBeastGuCard)
                .ToArray();
            AbstractLiDaoXuYing[] beastPhantoms =
                LiDaoPhantomSystem.GetPermanentPhantoms(Owner)
                    .Where(phantom => phantom.BeastKind is not null)
                    .ToArray();

            int nv = beastPhantoms.Length;
            int ng = beastGu
                .Select(card => card.GetType())
                .Distinct()
                .Count();
            shadowCount = 1 + nv + Math.Max(0, ng - nv) / 2;
        }
        description.Add("CurrentWoLiPhantomCount", shadowCount);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Player owner = Owner;
        int rank = Math.Clamp(GuRank, 1, 9);

        CardModel[] beastGu = EnumerateGuCards(owner)
            .Where(card => card is ILiDaoBeastGuCard)
            .ToArray();
        AbstractLiDaoXuYing[] beastPhantoms =
            LiDaoPhantomSystem.GetPermanentPhantoms(owner)
                .Where(phantom => phantom.BeastKind is not null)
                .ToArray();

        int nv = beastPhantoms.Length;
        int ng = beastGu
            .Select(card => card.GetType())
            .Distinct()
            .Count();
        int shadowCount = 1 + nv + Math.Max(0, ng - nv) / 2;

        DynamicVars["BeastPhantomCount"].BaseValue = nv;
        DynamicVars["BeastGuTypeCount"].BaseValue = ng;
        DynamicVars["WoLiPhantomCount"].BaseValue = shadowCount;
        DynamicVars["RepeatManifestChance"].BaseValue =
            RepeatManifestChanceAtRank(rank);

        try
        {
            // 仅消耗兽力虚影；我力虚影 BeastKind == null，因此保留。
            foreach (AbstractLiDaoXuYing phantom in beastPhantoms)
            {
                await CardPileCmd.RemoveFromCombat(
                    phantom,
                    skipVisuals: false
                );
            }

            // 所有兽力蛊移入蛊封存堆（按蛊封存堆系统规则处理）。
            foreach (CardModel gu in beastGu)
            {
                await GuCardPileSystem.MoveCardToPileAsync(
                    gu,
                    GuCardPileSystem.GuSealedPileType,
                    skipVisuals: false
                );
            }
            await GuCardPileSystem.RefillGuHandAsync(owner);

            // 按新公式生成我力虚影，并叠加独立临时生命来源。
            await WoLiPhantomSystem.AddShadowsAsync(
                choiceContext,
                owner,
                rank,
                shadowCount
            );
        }
        finally
        {
            // 配方材料（群力蛊/我力蛊）永久封存、不返还：
            // 清除本杀招的材料绑定，使战斗结束兜底不再归还材料。
            ClearBoundMaterials();
        }
    }

    internal static int RepeatManifestChanceAtRank(int rank) =>
        rank switch
        {
            >= 9 => 40,
            8 => 35,
            7 => 30,
            _ => 25,
        };

    private void RefreshRankValues()
    {
        DynamicVars["RepeatManifestChance"].BaseValue =
            RepeatManifestChanceAtRank(GuRank);
    }

    private static IEnumerable<CardModel> EnumerateGuCards(Player owner)
    {
        foreach (CardModel card in
                 GuCardPileSystem.PileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in
                 GuCardPileSystem.StoragePileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in
                 GuCardPileSystem.RecoveryPileType.GetPile(owner).Cards)
        {
            yield return card;
        }
        foreach (CardModel card in
                 GuCardPileSystem.GuSealedPileType.GetPile(owner).Cards)
        {
            yield return card;
        }
    }
}
