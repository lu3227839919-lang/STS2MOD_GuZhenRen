using System.Runtime.CompilerServices;

using GuZhenRen.Multiplayer;
using GuZhenRen.Patches;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.XueDao;

public static class XueDaoParasiteSystem
{
    public enum ParasiteKind
    {
        None = 0,
        Ordinary = 1,
        LegacyCrescentMoon = 2,
        BloodMoon = 3,
        LegacyBloodFetus = 4,
        BloodSeed = 5,
    }

    // 旧版旁路键字符串不得更换。
    private static readonly SavedAttachedState<CardModel, int> LegacyKindState =
        new("lu_gu_zhen_ren.xue_dao.parasite_kind", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> LegacyRankState =
        new("lu_gu_zhen_ren.xue_dao.parasite_rank", static () => 0);
    private static readonly SavedAttachedState<CardModel, bool> LegacyUpgradedState =
        new("lu_gu_zhen_ren.xue_dao.parasite_upgraded", static () => false);
    private static readonly SavedAttachedState<CardModel, int> LegacyStageState =
        new("lu_gu_zhen_ren.xue_dao.parasite_stage", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> LegacyTriggersRemainingState =
        new("lu_gu_zhen_ren.xue_dao.parasite_triggers_remaining", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> LegacyTriggersCompletedState =
        new("lu_gu_zhen_ren.xue_dao.parasite_triggers_completed", static () => 0);
    private static readonly SavedAttachedState<CardModel, bool> LegacyResolvingState =
        new("lu_gu_zhen_ren.xue_dao.parasite_resolving", static () => false);

    private sealed class ResolvingFlag
    {
        internal bool Value { get; set; }
    }

    private static readonly ConditionalWeakTable<CardModel, ResolvingFlag>
        ResolvingStates = new();

    internal static ParasiteKind NormalizePersistedKind(int rawKind) =>
        rawKind switch
        {
            1 => ParasiteKind.Ordinary,
            2 or 3 => ParasiteKind.BloodMoon,
            4 => ParasiteKind.Ordinary,
            5 => ParasiteKind.BloodSeed,
            _ => ParasiteKind.None,
        };

    public static bool HasParasite(CardModel? card) =>
        card != null && GetKind(card) != ParasiteKind.None;

    public static bool HasTriggeringParasite(CardModel? card) =>
        card != null &&
        GetKind(card) is ParasiteKind.Ordinary or ParasiteKind.BloodMoon;

    public static ParasiteKind GetKind(CardModel card) =>
        GetParasiteEnchantment(card)?.Kind ?? ParasiteKind.None;

    public static int GetRank(CardModel card) =>
        GetParasiteEnchantment(card)?.Rank ?? 0;

    public static int GetStage(CardModel card) =>
        GetParasiteEnchantment(card)?.Stage ?? 0;

    public static bool IsEligibleHost(CardModel card) =>
        !card.IsCanonical &&
        card.Type is CardType.Attack or CardType.Skill &&
        card is not YiHai;

    public static bool CanAttach(CardModel card, ParasiteKind incomingKind) =>
        incomingKind is ParasiteKind.Ordinary or ParasiteKind.BloodMoon &&
        IsEligibleHost(card) &&
        !HasParasite(card);

    public static async Task AttachAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        ParasiteKind kind,
        int rank,
        CardModel sourceCard
    )
    {
        if (!CanAttach(host, kind))
        {
            return;
        }

        XueDaoParasiteEnchantment parasite =
            XueDaoEnchantmentSlotPatch.AttachOrRefreshParasite(host, rank);
        parasite.Configure(
            kind,
            Math.Clamp(rank, 1, 6),
            kind == ParasiteKind.Ordinary ? 1 : 0
        );
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        ClearLegacyState(host);

        await PowerCmd.Apply<XueJiPower>(
            choiceContext,
            sourceCard.Owner.Creature,
            1,
            sourceCard.Owner.Creature,
            sourceCard
        );
    }

    public static void AddOrdinaryDescriptionArgs(
        LocString description,
        int rank
    )
    {
        description.Add("ParasiteI", GetStageValue(rank, 1));
        description.Add("ParasiteII", GetStageValue(rank, 2));
        description.Add("ParasiteIII", GetStageValue(rank, 3));
        description.Add("HatchValue", GetHatchValue(rank));
        description.Add("BleedII", GetBleed(rank, 2));
        description.Add("BleedIII", GetBleed(rank, 3));
        description.Add("HatchBleed", GetHatchBleed(rank));
    }

    public static int GetStageValue(int rank, int stage)
    {
        int first = Math.Clamp(rank, 1, 6) switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            5 or 6 => 6,
            _ => 2,
        };
        return first + Math.Clamp(stage, 1, 3) - 1;
    }

    public static int GetHatchValue(int rank) =>
        Math.Clamp(rank, 1, 6) switch
        {
            1 => 6,
            2 => 8,
            3 => 10,
            4 => 12,
            5 => 14,
            6 => 15,
            _ => 6,
        };

    public static int GetBleed(int rank, int stage)
    {
        rank = Math.Clamp(rank, 1, 6);
        return Math.Clamp(stage, 1, 3) switch
        {
            1 => 0,
            2 => rank >= 5 ? 2 : 1,
            3 => rank >= 3 ? 2 : 1,
            _ => 0,
        };
    }

    public static int GetHatchBleed(int rank) =>
        Math.Clamp(rank, 1, 6) switch
        {
            1 => 1,
            2 or 3 => 2,
            _ => 3,
        };

    public static string? GetHostCardDynamicText(CardModel host)
    {
        ParasiteKind kind = GetKind(host);
        if (kind == ParasiteKind.None)
        {
            return null;
        }

        string entry = kind switch
        {
            ParasiteKind.BloodMoon =>
                "GU_ZHEN_REN_PERSONAL_CARD_PARASITE_BLOOD_MOON.cardText",
            ParasiteKind.BloodSeed =>
                "GU_ZHEN_REN_PERSONAL_CARD_PARASITE_BLOOD_SEED.cardText",
            _ when host.Type == CardType.Attack =>
                "GU_ZHEN_REN_PERSONAL_CARD_PARASITE_ORDINARY_ATTACK.cardText",
            _ =>
                "GU_ZHEN_REN_PERSONAL_CARD_PARASITE_ORDINARY_SKILL.cardText",
        };

        int rank = Math.Clamp(GetRank(host), 1, 6);
        int stage = Math.Clamp(GetStage(host), 1, 3);
        LocString text = new("cards", entry);
        text.Add("Rank", rank);
        text.Add("Stage", stage);
        text.Add("ParasiteValue", GetStageValue(rank, stage));
        text.Add("ParasiteBleed", GetBleed(rank, stage));
        text.Add("WillHatch", stage == 3 ? 1 : 0);
        text.Add("HatchValue", GetHatchValue(rank));
        text.Add("HatchBleed", GetHatchBleed(rank));
        return text.GetFormattedText();
    }

    internal static void MarkResolving(CardModel card, bool resolving)
    {
        LegacyResolvingState[card] = false;
        if (resolving)
        {
            ResolvingStates.GetOrCreateValue(card).Value = true;
        }
        else
        {
            ResolvingStates.Remove(card);
        }
    }

    internal static bool IsResolving(CardModel card) =>
        ResolvingStates.TryGetValue(card, out ResolvingFlag? flag) &&
        flag.Value;

    internal static async Task ClearIfExhaustedAsync(
        PlayerChoiceContext choiceContext,
        CardModel host
    )
    {
        if (HasParasite(host) &&
            !IsResolving(host) &&
            host.Pile?.Type == PileType.Exhaust)
        {
            await ClearAsync(choiceContext, host, host);
        }
    }

    public static async Task<bool> TriggerFromCardPlayAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (cardPlay.IsAutoPlay || !HasParasite(cardPlay.Card))
        {
            return false;
        }

        CardModel host = cardPlay.Card;
        XueDaoParasiteEnchantment? original = GetParasiteEnchantment(host);
        ParasiteKind originalKind = GetKind(host);
        bool succeeded = await TriggerOnceAsync(
            choiceContext,
            host,
            host,
            cardPlay
        );
        if (!succeeded)
        {
            return false;
        }

        XueChiPower? pool = host.Owner.Creature.GetPower<XueChiPower>();
        int extraTriggers = Math.Max(0, pool?.Amount ?? 0);
        if (pool == null || extraTriggers == 0)
        {
            return true;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            pool,
            -extraTriggers,
            host.Owner.Creature,
            host
        );

        for (int index = 0; index < extraTriggers; index++)
        {
            if (!ReferenceEquals(GetParasiteEnchantment(host), original) ||
                GetKind(host) != originalKind)
            {
                break;
            }

            if (!await TriggerOnceAsync(
                    choiceContext,
                    host,
                    host,
                    cardPlay
                ))
            {
                break;
            }
        }

        return true;
    }

    private static async Task<bool> TriggerOnceAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        CardPlay? cardPlay
    )
    {
        return GetKind(host) switch
        {
            ParasiteKind.Ordinary => await TriggerOrdinaryAsync(
                choiceContext,
                host,
                effectSource,
                cardPlay
            ),
            ParasiteKind.BloodMoon => await TriggerBloodMoonAsync(
                choiceContext,
                host,
                effectSource,
                cardPlay
            ),
            ParasiteKind.BloodSeed => WakeBloodSeed(host),
            _ => false,
        };
    }

    private static bool WakeBloodSeed(CardModel host)
    {
        XueDaoParasiteEnchantment? parasite = GetParasiteEnchantment(host);
        if (parasite == null)
        {
            return false;
        }

        parasite.Configure(ParasiteKind.Ordinary, 6, 1);
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        // 唤醒不算触发，本次不结算血寄Ⅰ，也不消费血池。
        return false;
    }

    private static async Task<bool> TriggerOrdinaryAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        CardPlay? cardPlay
    )
    {
        int rank = Math.Clamp(GetRank(host), 1, 6);
        int stage = Math.Clamp(GetStage(host), 1, 3);

        if (host.Type == CardType.Attack)
        {
            await ResolveAttackEffectAsync(
                choiceContext,
                effectSource,
                cardPlay,
                GetStageValue(rank, stage),
                GetBleed(rank, stage)
            );
        }
        else
        {
            await CreatureCmd.GainBlock(
                host.Owner.Creature,
                GetStageValue(rank, stage),
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );
        }

        XueDaoParasiteEnchantment? parasite = GetParasiteEnchantment(host);
        if (parasite == null)
        {
            return true;
        }

        if (stage < 3)
        {
            parasite.AdvanceTo(stage + 1);
            XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
            return true;
        }

        if (host.Type == CardType.Attack)
        {
            await ResolveAttackEffectAsync(
                choiceContext,
                effectSource,
                cardPlay,
                GetHatchValue(rank),
                GetHatchBleed(rank)
            );
        }
        else
        {
            await CreatureCmd.GainBlock(
                host.Owner.Creature,
                GetHatchValue(rank),
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );
            await CardPileCmd.Draw(choiceContext, 1, host.Owner);
        }

        if (rank >= 6)
        {
            parasite.Configure(ParasiteKind.BloodSeed, 6, 0);
            XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        }
        else
        {
            await ClearAsync(choiceContext, host, effectSource);
        }

        return true;
    }

    private static async Task ResolveAttackEffectAsync(
        PlayerChoiceContext choiceContext,
        CardModel effectSource,
        CardPlay? cardPlay,
        int damage,
        int bleed
    )
    {
        Creature? target = SelectLowestHealthEnemy(effectSource);
        if (target == null)
        {
            return;
        }

        bool wasAlive = target.IsAlive;
        await DamageCmd.Attack(damage)
            .FromCard(effectSource, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await XueDaoRemainsKillPatch.GrantForBloodDamageKillAsync(
            cardPlay,
            effectSource,
            target,
            wasAlive
        );

        if (bleed > 0 && target.IsAlive)
        {
            await XueDaoPowerSystem.ApplyLiuXue(
                choiceContext,
                effectSource,
                target,
                bleed
            );
        }
    }

    private static Creature? SelectLowestHealthEnemy(CardModel source)
    {
        if (source.Owner.Creature.CombatState is not { } combatState)
        {
            return null;
        }

        return GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => enemy.IsAlive)
            .OrderBy(enemy => enemy.CurrentHp)
            .ThenBy(enemy => enemy.CombatId)
            .FirstOrDefault();
    }

    private static async Task<bool> TriggerBloodMoonAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        CardPlay? cardPlay
    )
    {
        CardModel[] candidates = GetBloodMoonTargets(host);
        if (candidates.Length == 0)
        {
            return false;
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_CARD_PARASITE_BLOOD_MOON.selectionPrompt"
        );
        CardSelectorPrefs prefs = new(prompt, 1)
        {
            Cancelable = false,
            RequireManualConfirmation = candidates.Length > 1,
            PretendCardsCanBePlayed = true,
        };
        CardModel? selected = (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    candidates,
                    host.Owner,
                    prefs
                )
            )
            .FirstOrDefault();

        if (selected == null ||
            ReferenceEquals(selected, host) ||
            !candidates.Contains(selected) ||
            GetKind(selected) != ParasiteKind.Ordinary)
        {
            return false;
        }

        return await TriggerOrdinaryAsync(
            choiceContext,
            selected,
            effectSource,
            cardPlay
        );
    }

    private static CardModel[] GetBloodMoonTargets(CardModel host)
    {
        Player owner = host.Owner;
        return new[]
            {
                PileType.Hand.GetPile(owner),
                PileType.Draw.GetPile(owner),
                PileType.Discard.GetPile(owner),
            }
            .SelectMany(static pile => pile.Cards)
            .Where(card =>
                !ReferenceEquals(card, host) &&
                GetKind(card) == ParasiteKind.Ordinary
            )
            .Distinct()
            .OrderBy(card => card.Id.ToString(), StringComparer.Ordinal)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();
    }

    private static async Task ClearAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel source
    )
    {
        XueDaoEnchantmentSlotPatch.RemoveParasite(host);
        ClearLegacyState(host);
        ResolvingStates.Remove(host);

        if (host.Owner.Creature.GetPower<XueJiPower>() is { } power)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                -1,
                host.Owner.Creature,
                source
            );
        }
    }

    private static XueDaoParasiteEnchantment? GetParasiteEnchantment(
        CardModel card
    ) =>
        XueDaoEnchantmentSlotPatch.TryGetParasite(card) ??
        TryMigrateLegacyParasite(card);

    private static XueDaoParasiteEnchantment? TryMigrateLegacyParasite(
        CardModel card
    )
    {
        ParasiteKind kind = NormalizePersistedKind(LegacyKindState[card]);
        if (kind == ParasiteKind.None ||
            card.IsCanonical ||
            !IsEligibleHost(card))
        {
            return null;
        }

        int rank = Math.Clamp(LegacyRankState[card], 1, 6);
        int legacyStage = Math.Max(
            LegacyStageState[card],
            LegacyTriggersCompletedState[card]
        );
        int stage = kind == ParasiteKind.Ordinary
            ? Math.Clamp(legacyStage + 1, 1, 3)
            : 0;

        XueDaoParasiteEnchantment parasite =
            XueDaoEnchantmentSlotPatch.AttachOrRefreshParasite(card, rank);
        parasite.Configure(kind, rank, stage);
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        ClearLegacyState(card);
        return parasite;
    }

    private static void ClearLegacyState(CardModel card)
    {
        LegacyKindState[card] = 0;
        LegacyRankState[card] = 0;
        LegacyUpgradedState[card] = false;
        LegacyStageState[card] = 0;
        LegacyTriggersRemainingState[card] = 0;
        LegacyTriggersCompletedState[card] = 0;
        LegacyResolvingState[card] = false;
    }
}
