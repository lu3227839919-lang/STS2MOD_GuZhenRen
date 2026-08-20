using System.Reflection;

using GuZhenRen.Cards;
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.LiDao;
using GuZhenRen.Cards.ZhouDao;
using GuZhenRen.Characters;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace GuZhenRen.Patches;

/// <summary>
/// 让原版攻击、技能、能力与锻造药水识别蛊虫专用卡池和蛊手牌。
/// </summary>
internal static class GuPotionCompatibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuPotionCompatibility";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);
        PatchPotionPrefix(
            harmony,
            typeof(AttackPotion),
            nameof(AttackPotionOnUsePrefix)
        );
        PatchPotionPrefix(
            harmony,
            typeof(SkillPotion),
            nameof(SkillPotionOnUsePrefix)
        );
        PatchPotionPrefix(
            harmony,
            typeof(PowerPotion),
            nameof(PowerPotionOnUsePrefix)
        );

        MethodInfo? forgeOnUse = AccessTools.DeclaredMethod(
            typeof(BlessingOfTheForge),
            "OnUse"
        );
        if (forgeOnUse == null)
        {
            throw new MissingMethodException(
                "锻造药水兼容所需的 BlessingOfTheForge.OnUse 不存在。"
            );
        }

        harmony.Patch(
            forgeOnUse,
            postfix: new HarmonyMethod(
                typeof(GuPotionCompatibilityPatch),
                nameof(BlessingOfTheForgeOnUsePostfix)
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

    private static void PatchPotionPrefix(
        Harmony harmony,
        Type potionType,
        string prefixName
    )
    {
        MethodInfo? onUse = AccessTools.DeclaredMethod(
            potionType,
            "OnUse"
        );
        if (onUse == null)
        {
            throw new MissingMethodException(
                $"蛊药水兼容所需的 {potionType.Name}.OnUse 不存在。"
            );
        }

        harmony.Patch(
            onUse,
            prefix: new HarmonyMethod(
                typeof(GuPotionCompatibilityPatch),
                prefixName
            )
        );
    }

    private static bool AttackPotionOnUsePrefix(
        PotionModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        ref Task __result
    )
    {
        if (!IsGuPlayerTarget(target))
        {
            return true;
        }

        __result = ChooseAndGrantRandomGuAsync(
            __instance,
            choiceContext,
            target!,
            requiredType: CardType.Attack,
            bypassCapacity: false
        );
        return false;
    }

    private static bool SkillPotionOnUsePrefix(
        PotionModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        ref Task __result
    )
    {
        if (!IsGuPlayerTarget(target))
        {
            return true;
        }

        __result = ChooseAndGrantRandomGuAsync(
            __instance,
            choiceContext,
            target!,
            requiredType: CardType.Skill,
            bypassCapacity: false
        );
        return false;
    }

    private static bool PowerPotionOnUsePrefix(
        PotionModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        ref Task __result
    )
    {
        if (!IsGuPlayerTarget(target))
        {
            return true;
        }

        __result = ChooseAndGrantRandomGuAsync(
            __instance,
            choiceContext,
            target!,
            requiredType: null,
            bypassCapacity: false
        );
        return false;
    }

    private static async Task ChooseAndGrantRandomGuAsync(
        PotionModel potion,
        PlayerChoiceContext choiceContext,
        Creature target,
        CardType? requiredType,
        bool bypassCapacity
    )
    {
        if (target.Player is not { } player ||
            player.Creature.CombatState is not { } combatState)
        {
            return;
        }

        List<CardModel> candidatePool = ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint
            )
            .Where(card =>
                card is IGuWormCard &&
                (!requiredType.HasValue ||
                    card.Type == requiredType.Value) &&
                GuZhenRenCardRules.CanAppearInCardReward(
                    player,
                    card
                )
            )
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToList();

        if (candidatePool.Count == 0)
        {
            Entry.Logger.Warn(
                $"[{potion.Id}] 没有符合条件的临时蛊虫候选。"
            );
            return;
        }

        List<CardModel> choices = [];
        while (choices.Count < 3 && candidatePool.Count > 0)
        {
            CardModel? canonical = player
                .RunState
                .Rng
                .CombatCardGeneration
                .NextItem(candidatePool);
            if (canonical == null)
            {
                break;
            }

            candidatePool.Remove(canonical);
            CardModel generated = combatState.CreateCard(
                canonical,
                player
            );
            choices.Add(generated);
        }

        if (choices.Count == 0)
        {
            Entry.Logger.Warn(
                $"[{potion.Id}] 未能创建临时蛊虫候选。"
            );
            return;
        }

        GuRankRewardPatch.AssignRandomRanksLikeReward(
            choices,
            player,
            forceAssignment: true
        );

        CardModel? selected = null;
        bool selectedWasAdded = false;
        try
        {
            selected = await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                choices,
                player
            );
            if (selected == null)
            {
                return;
            }

            selectedWasAdded =
                await GuCardPileSystem.AddTemporaryGuToCombat(
                    selected,
                    player,
                    potion.Owner,
                    bypassCapacity
                );
            if (!selectedWasAdded)
            {
                return;
            }

            await GrantTemporaryCompanionAsync(selected, player);
        }
        finally
        {
            foreach (CardModel choice in choices)
            {
                if (selectedWasAdded &&
                    ReferenceEquals(choice, selected))
                {
                    continue;
                }

                choice.RemoveFromState();
            }
        }
    }

    private static async Task GrantTemporaryCompanionAsync(
        CardModel guCard,
        Player owner
    )
    {
        Type? companionType =
            guCard is ICompanionSourceGuCard companionSource
                ? companionSource.CompanionCardType
                : null;
        if (companionType == null ||
            owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        CardModel? canonical = ModelDb
            .CardPool<GuZhenRenCardPool>()
            .AllCards
            .SingleOrDefault(card =>
                card.GetType() == companionType
            );
        if (canonical == null)
        {
            Entry.Logger.Warn(
                $"临时蛊 {guCard.Id} 的伴生牌 " +
                $"{companionType.Name} 未在角色卡池注册。"
            );
            return;
        }

        CardModel companion = combatState.CreateCard(
            canonical,
            owner
        );
        if (guCard is AbstractGuZhenRenCard sourceGu &&
            companion is AbstractGuZhenRenCard rankedCompanion)
        {
            rankedCompanion.InitializeGuRankFromSource(
                sourceGu.GuRank
            );
        }

        // 药水生成的力道伴生牌是明确允许炼力的临时牌；普通临时
        // 伴生牌没有此实例标记，仍不会推进炼力。
        LiDaoBeastTrainingSystem.AllowCompanionTraining(companion);

        await GuGeneratedCardFactory.AddToHandOrDiscard(
            companion,
            owner
        );
    }

    private static void BlessingOfTheForgeOnUsePostfix(
        Creature? target
    )
    {
        if (!IsGuPlayerTarget(target))
        {
            return;
        }

        Player? player = target!.Player;
        if (player == null)
        {
            return;
        }

        foreach (CardModel card in GuCardPileSystem
            .PileType
            .GetPile(player)
            .Cards
            .Where(static card => card is IGuWormCard)
            .ToArray())
        {
            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
        }
    }

    private static bool IsGuPlayerTarget(Creature? target) =>
        target?.Player is { } player &&
        player.Character is GuZhenRenCharacter;
}
