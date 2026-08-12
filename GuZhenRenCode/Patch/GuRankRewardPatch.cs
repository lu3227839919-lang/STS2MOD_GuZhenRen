// Patch/GuRankRewardPatch.cs
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;

using STS2RitsuLib;

namespace GuZhenRen.Cards;

/// <summary>
/// 战斗卡牌奖励生成候选后，为尚未赋阶的蛊卡确定初始转数。
///
/// 不在 SpecialCardReward 构造函数中推进 RNG：构造函数可能在 UI 重建、
/// 反序列化或客户端快照恢复时重复执行。卡牌上的可保存标记还会防止
/// 同一个 CardReward 对象重复 Populate 时再次抽取。
/// </summary>
internal static class GuRankRewardPatch
{
    private const string HarmonyId = Entry.ModId + ".GuRankReward";
    private const string RewardRngStreamId = "reward/gu_rank";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);
        MethodBase? populate = AccessTools.Method(
            typeof(CardReward),
            nameof(CardReward.Populate)
        );

        if (populate == null)
        {
            throw new MissingMethodException(
                "蛊虫转数随机化所需的 CardReward.Populate 不存在。"
            );
        }

        harmony.Patch(
            populate,
            postfix: new HarmonyMethod(
                typeof(GuRankRewardPatch),
                nameof(CardRewardPopulatePostfix)
            )
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void CardRewardPopulatePostfix(
        CardReward __instance
    )
    {
        Player player = __instance.Player;
        Rng rng = RitsuLibFramework.GetModPlayerRng(
            player,
            Entry.ModId,
            RewardRngStreamId
        );

        AssignRandomRanks(__instance.Cards, rng, player);
    }

    private static void AssignRandomRanks(
        IEnumerable<CardModel> cards,
        Rng rng,
        Player player
    )
    {
        foreach (CardModel card in cards)
        {
            if (card is not AbstractGuZhenRenCard guCard ||
                card is not IGuWormCard ||
                !guCard.NeedsInitialGuRankAssignment)
            {
                continue;
            }

            // 只为首次赋阶的蛊卡推进专用随机流。重复 Populate、UI 重建
            // 或快照恢复不会再次消费种子，也不会改变后续奖励的转数。
            uint cardSeed = rng.NextUnsignedInt();

            int maximumRewardRank = guCard.MaxGuRank;

            if (GuZhenRenCardRules.HasSameXianGu(
                    player.RunState,
                    card
                ))
            {
                maximumRewardRank = Math.Min(
                    maximumRewardRank,
                    GuZhenRenCardRules.XianGuRank - 1
                );
            }

            Rng cardRng = new(cardSeed);
            bool assigned = guCard.TryAssignRandomGuRankOnReward(
                cardRng,
                player.RunState.TotalFloor,
                minRank: guCard.MinimumAvailableGuRank,
                maxRank: maximumRewardRank
            );

            if (assigned)
            {
                GuZhenRenCardRules.RegisterXianGuClaim(
                    guCard,
                    player.RunState.TotalFloor
                );
            }
        }
    }
}
