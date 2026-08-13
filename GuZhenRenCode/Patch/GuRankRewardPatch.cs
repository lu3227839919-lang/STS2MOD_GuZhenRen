// Patch/GuRankRewardPatch.cs
using System.Collections.Generic;
using System.Reflection;

using GuZhenRen.Cards.LiDao;

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
        AssignRandomRanksLikeReward(
            __instance.Cards,
            player,
            forceAssignment: false
        );
    }

    /// <summary>
    /// 战斗内随机给予蛊虫时复用与卡牌奖励完全相同的随机流、楼层
    /// 分布和仙蛊上限。forceAssignment 用于百兽力蛊等构造时已写入
    /// 默认转数、但本次仍必须重新按奖励规则随机赋阶的卡牌。
    /// </summary>
    internal static void AssignRandomRanksLikeReward(
        IEnumerable<CardModel> cards,
        Player player,
        bool forceAssignment
    )
    {
        Rng rng = RitsuLibFramework.GetModPlayerRng(
            player,
            Entry.ModId,
            RewardRngStreamId
        );

        foreach (CardModel card in cards)
        {
            if (card is not AbstractGuZhenRenCard guCard ||
                card is not IGuWormCard ||
                (!forceAssignment &&
                    !guCard.NeedsInitialGuRankAssignment))
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
            bool assigned;
            if (forceAssignment)
            {
                guCard.AssignRandomGuRankOnReward(
                    cardRng,
                    player.RunState.TotalFloor,
                    minRank: guCard.MinimumAvailableGuRank,
                    maxRank: maximumRewardRank
                );
                assigned = true;
            }
            else
            {
                assigned = guCard.TryAssignRandomGuRankOnReward(
                    cardRng,
                    player.RunState.TotalFloor,
                    minRank: guCard.MinimumAvailableGuRank,
                    maxRank: maximumRewardRank
                );
            }

            if (card is BaiShouLiGu baiShouLiGu)
            {
                baiShouLiGu.AssignRandomComposition(cardRng);
            }

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
