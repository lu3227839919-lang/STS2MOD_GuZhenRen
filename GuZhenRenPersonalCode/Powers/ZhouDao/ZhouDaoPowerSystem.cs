using GuZhenRen.Cards;
using GuZhenRen.Cards.ZhouDao;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;


namespace GuZhenRen.Powers.ZhouDao;

public readonly record struct NianHuaGainResult(
    int Requested,
    int Effective,
    int SuiManCount
);

/// <summary>宙道年华与岁满的统一结算入口。</summary>
public static class ZhouDaoPowerSystem
{
    public static bool IsZhouDaoCard(CardModel? card) =>
        card?.Tags.Contains(GuZhenRenTags.ZhouDao) == true;

    public static int GetNianHua(Player player) =>
        player.Creature.GetPower<NianHuaPower>()?.Amount ?? 0;

    public static bool HasSuiManThisTurn(Player player) =>
        player.PlayerCombatState is { } state &&
        player.Creature.GetPower<ZhouDaoTrackerPower>() is { } tracker &&
        tracker.LastSuiManTurn == state.TurnNumber;

    public static bool HasGuRecoveredThisTurn(Player player)
    {
        if (player.PlayerCombatState is not { } state)
        {
            return false;
        }

        int turn = state.TurnNumber;
        return GuCardPileSystem.PileType.GetPile(player).Cards
            .Concat(GuCardPileSystem.StoragePileType.GetPile(player).Cards)
            .Concat(GuCardPileSystem.RecoveryPileType.GetPile(player).Cards)
            .Where(static card => card is IGuWormCard)
            .Any(card =>
                GuCardUsageRules.GetRecoveryCompletedTurn(card) == turn
            );
    }

    public static Task<NianHuaGainResult> GainNianHua(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    ) => GainNianHua(
        choiceContext,
        sourceCard.Owner,
        amount,
        sourceCard
    );

    internal static async Task<NianHuaGainResult> GainNianHua(
        PlayerChoiceContext choiceContext,
        Player player,
        int amount,
        CardModel? sourceCard = null,
        bool allowSanGengBonus = true
    )
    {
        if (amount <= 0 ||
            player.Creature.CombatState == null)
        {
            return new NianHuaGainResult(amount, 0, 0);
        }

        int requested = amount;
        if (player.Creature.GetPower<ZhouDaoTrackerPower>() == null)
        {
            await PowerCmd.Apply<ZhouDaoTrackerPower>(
                choiceContext,
                player.Creature,
                1,
                player.Creature,
                sourceCard,
                silent: true
            );
        }

        if (allowSanGengBonus &&
            player.Creature.GetPower<SanGengPower>() is { Amount: > 0 } sanGeng)
        {
            amount += 1;
            await PowerCmd.ModifyAmount(
                choiceContext,
                sanGeng,
                -1,
                player.Creature,
                sourceCard
            );
        }

        int effective = 0;
        int suiManCount = 0;
        for (int point = 0; point < amount; point++)
        {
            await PowerCmd.Apply<NianHuaPower>(
                choiceContext,
                player.Creature,
                1,
                player.Creature,
                sourceCard
            );
            effective++;

            NianHuaPower? nianHua =
                player.Creature.GetPower<NianHuaPower>();
            if (nianHua == null || nianHua.Amount < NianHuaPower.MaximumAmount)
            {
                continue;
            }

            await PowerCmd.ModifyAmount(
                choiceContext,
                nianHua,
                -nianHua.Amount,
                player.Creature,
                sourceCard
            );
            suiManCount++;
            await ResolveSuiManAsync(
                choiceContext,
                player,
                sourceCard
            );
        }

        return new NianHuaGainResult(
            requested,
            effective,
            suiManCount
        );
    }

    private static async Task ResolveSuiManAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel? sourceCard
    )
    {
        int turnNumber = player.PlayerCombatState?.TurnNumber ?? 1;
        if (player.Creature.GetPower<ZhouDaoTrackerPower>() is { } tracker)
        {
            tracker.LastSuiManTurn = turnNumber;
        }

        CardModel? selected = await SelectRecoveryTargetAsync(
            choiceContext,
            player
        );
        if (selected != null)
        {
            await GuCardPileSystem.AccelerateRecoveryAsync(
                player,
                selected,
                turnNumber,
                turns: 1
            );
        }

        foreach (Creature enemy in player.Creature.CombatState!
                     .Enemies
                     .Where(static enemy => enemy.IsAlive))
        {
            foreach (PowerModel power in enemy.Powers.ToArray())
            {
                if (power is WeakPower or VulnerablePower or FrailPower or
                    HuanBuPower)
                {
                    await PowerCmd.ModifyAmount(
                        choiceContext,
                        power,
                        1,
                        player.Creature,
                        sourceCard
                    );
                }
            }
        }

        // 岁满后的反哺统一在重置年华之后结算，因而自然形成新的溢出进度。
        if (player.Creature.GetPower<NianNianSuiSuiPower>() is { } nianNian)
        {
            await nianNian.OnSuiManAsync(choiceContext, sourceCard);
        }
        if (player.Creature.GetPower<ZhouMaoPower>() is { } zhouMao)
        {
            await zhouMao.OnSuiManAsync(choiceContext, sourceCard);
        }
        if (player.Creature.GetPower<SiShuiLiuNianPower>() is { } siShui)
        {
            await siShui.OnSuiManAsync(choiceContext);
        }
    }

    private static async Task<CardModel?> SelectRecoveryTargetAsync(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        CardModel[] candidates = GuCardPileSystem.RecoveryPileType
            .GetPile(player)
            .Cards
            .Where(card =>
                card is IGuWormCard &&
                GuCardUsageRules.HasRecoverySchedule(card) &&
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card))
            .OrderByDescending(GuCardUsageRules.GetRecoveryReadyTurn)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }
        if (candidates.Length == 1 ||
            choiceContext is ThrowingPlayerChoiceContext)
        {
            return candidates[0];
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_ZHOU_DAO.suiManSelectionPrompt"
        );
        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            player,
            new CardSelectorPrefs(prompt, 1)
            {
                Cancelable = false,
                PretendCardsCanBePlayed = true,
            }
        );
        return selected.FirstOrDefault();
    }

    internal static async Task NotifyGuRecoveredAsync(
        CardModel card,
        bool acceleratedBySuiMan
    )
    {
        if (card.Owner.Creature.CombatState == null ||
            card.Owner.Creature.GetPower<GuangYinRenRanPower>() is not
                { } power)
        {
            return;
        }

        await power.OnGuRecoveredAsync(
            new ThrowingPlayerChoiceContext(),
            acceleratedBySuiMan
        );
    }
}
