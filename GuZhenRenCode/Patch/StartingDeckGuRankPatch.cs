using System.Reflection;

using HarmonyLib;

using GuZhenRen.Cards;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Patches;

/// <summary>
/// 将蛊真人角色的初始牌组蛊虫统一设为一转。
///
/// 原版 Player.PopulateStartingDeck 会把 Character.StartingDeck 的规范模型
/// 复制为可变实例并直接加入牌组。蛊虫卡牌的规范模型默认转数为 0，
/// 若不在这里补写，一局新游戏的起始卡会以未定阶状态进入牌组。
///
/// 该补丁只影响蛊真人角色的新开局，不影响存档加载、奖励牌随机转数或
/// 其他角色。
/// </summary>
internal static class StartingDeckGuRankPatch
{
    private const string HarmonyId =
        Entry.ModId + ".StartingDeckGuRank";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? populateStartingDeck =
            AccessTools.DeclaredMethod(
                typeof(Player),
                "PopulateStartingDeck"
            );

        if (populateStartingDeck == null)
        {
            throw new MissingMethodException(
                "初始牌组一转补丁所需的 Player.PopulateStartingDeck 不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            populateStartingDeck,
            postfix: new HarmonyMethod(
                typeof(StartingDeckGuRankPatch),
                nameof(PopulateStartingDeckPostfix)
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

    private static void PopulateStartingDeckPostfix(
        Player __instance
    )
    {
        if (__instance.Character is not
            GuZhenRenCharacter)
        {
            return;
        }

        int updatedCount = 0;

        foreach (AbstractGuZhenRenCard card in __instance
                     .Deck
                     .Cards
                     .OfType<AbstractGuZhenRenCard>())
        {
            if (card.EnsureMinimumGuRank())
            {
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            Entry.Logger.Info(
                $"已将蛊真人初始牌组中的 {updatedCount} 张蛊虫牌设为一转。"
            );
        }
    }
}
