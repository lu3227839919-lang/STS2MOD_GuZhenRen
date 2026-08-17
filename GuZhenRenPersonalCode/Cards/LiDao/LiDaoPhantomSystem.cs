using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 新版力道虚影的生成、容量与集中显化结算。
/// 同种虚影可以存在多个；每张虚影独立占用容量并独立判定显化。
/// </summary>
public static class LiDaoPhantomSystem
{
    public static IReadOnlyList<AbstractLiDaoXuYing> GetPermanentPhantoms(
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        return PileType.Hand.GetPile(owner).Cards
            .OfType<AbstractLiDaoXuYing>()
            .OrderBy(GetResolutionOrder)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();
    }

    internal static async Task ActivateBeastGuAsync<TPhantom>(
        PlayerChoiceContext choiceContext,
        AbstractLiDaoBeastGuCard sourceGu
    ) where TPhantom : AbstractLiDaoXuYing
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(sourceGu);

        await EnsureCapacityAsync(choiceContext, sourceGu);

        TPhantom phantom = GuGeneratedCardFactory.Create<TPhantom>(
            sourceGu.Owner,
            sourceGu.GuRank,
            upgraded: false
        );
        await GuGeneratedCardFactory.AddToHandOrDiscard(
            phantom,
            sourceGu.Owner
        );
        await EnsureControllerAsync(choiceContext, sourceGu);
    }

    internal static async Task ResolveAttackAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries ||
            cardPlay.Card.Type != CardType.Attack ||
            cardPlay.Card.Keywords.Contains(GuZhenRenKeywords.XuYing) ||
            cardPlay.Card.Tags.Contains(GuZhenRenTags.XuYingCopy))
        {
            return;
        }

        foreach (AbstractLiDaoXuYing phantom in
                 GetPermanentPhantoms(cardPlay.Player))
        {
            bool executed = await phantom.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: false,
                effectMultiplier: 1m
            );
            if (executed)
            {
                await LiDaoManifestHub.NotifyNaturalManifestAsync(
                    choiceContext,
                    cardPlay.Player,
                    phantom,
                    cardPlay
                );
            }
        }
    }

    private static async Task EnsureCapacityAsync(
        PlayerChoiceContext choiceContext,
        AbstractGuZhenRenCard source
    )
    {
        IReadOnlyList<AbstractLiDaoXuYing> existing =
            GetPermanentPhantoms(source.Owner);
        if (existing.Sum(card => card.PhantomSlotCost) < 4)
        {
            return;
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_CARD_LI_DAO_REPLACE_PHANTOM.selectionScreenPrompt"
        );
        CardSelectorPrefs prefs = new(prompt, 1)
        {
            Cancelable = false,
        };

        CardModel? selected = (
            await CardSelectCmd.FromHand(
                choiceContext,
                source.Owner,
                prefs,
                card => card is AbstractLiDaoXuYing,
                source
            )
        ).FirstOrDefault();

        selected ??= existing.FirstOrDefault();
        if (selected != null)
        {
            await CardPileCmd.RemoveFromCombat(
                selected,
                skipVisuals: false
            );
        }
    }

    internal static async Task EnsureControllerAsync(
        PlayerChoiceContext choiceContext,
        CardModel source
    )
    {
        if (source.Owner.Creature.GetPower<LiDaoBattlePower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<LiDaoBattlePower>(
            choiceContext,
            source.Owner.Creature,
            1,
            source.Owner.Creature,
            source
        );
    }

    private static int GetResolutionOrder(AbstractLiDaoXuYing phantom) =>
        phantom switch
        {
            BaiZhiXuYing => 0,
            EXuYing => 1,
            QingNiuXuYing => 2,
            ShiGuiXuYing => 3,
            FeiXiongXuYing => 4,
            _ => 5,
        };
}
