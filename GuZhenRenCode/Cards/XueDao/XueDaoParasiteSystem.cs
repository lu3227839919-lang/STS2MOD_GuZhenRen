using System.Reflection;
using System.Runtime.CompilerServices;

using GuZhenRen.Cards.LiDao;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Multiplayer;
using GuZhenRen.Patches;
using GuZhenRen.Powers.XueDao;

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

/// <summary>
/// “血寄胎变”统一系统。寄生跟随宿主跨牌堆存在；完整 CardPlay 系列
/// （含 Replay）结束后只推进一次。最终阶段孵化，未完成却进入消耗堆时破胎。
/// </summary>
public static class XueDaoParasiteSystem
{
    public enum ParasiteKind
    {
        None = 0,
        BloodQi = 1,
        CrescentMoon = 2, // 兼容旧战斗快照；按月相残月处理。
        BloodMoon = 3,
        BloodFetus = 4,
    }

    public readonly record struct BloodMoonPhaseValues(
        int BaseDamage,
        int EnergyScale,
        int TotalBleed,
        int TotalMarks
    );

    // 旧版旁路存档键仅用于首次读取时迁移。新数据全部写入
    // XueDaoParasiteEnchantment 的 SavedProperty。
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

    // 0.9.3 的早期构建曾把一次出牌期间的 resolving 标记写入
    // SavedProperties。即使新实现已经改用 ConditionalWeakTable，旧存档和
    // Replay 历史仍可能携带该属性；若不继续注册它，0.110.x 会在胜利结算
    // 或退出保存时因找不到属性 net ID 而中断。此状态仅作为旧档兼容占位，
    // 实际结算仍只读取下方的运行时 ResolvingStates。
    private static readonly SavedAttachedState<CardModel, bool> LegacyResolvingState =
        new("lu_gu_zhen_ren.xue_dao.parasite_resolving", static () => false);

    private sealed class ResolvingFlag
    {
        internal bool Value { get; set; }
    }

    // 只在一次出牌结算期间使用，不属于需要存档或同步的玩法状态。
    private static readonly ConditionalWeakTable<CardModel, ResolvingFlag>
        ResolvingStates = new();

    private static readonly MethodInfo? DrawInternalMethod = typeof(CardPileCmd).GetMethod(
        "DrawInternal",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        types: [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)],
        modifiers: null
    );

    public static bool HasParasite(CardModel? card)
    {
        if (card == null)
        {
            return false;
        }

        if (XueDaoEnchantmentSlotPatch.TryGetParasite(card) is { } parasite)
        {
            return parasite.Kind != ParasiteKind.None;
        }

        return TryMigrateLegacyParasite(card) is
            { Kind: not ParasiteKind.None };
    }

    public static ParasiteKind GetKind(CardModel card)
    {
        return GetParasiteEnchantment(card)?.Kind ?? ParasiteKind.None;
    }

    public static int GetRank(CardModel card) =>
        GetParasiteEnchantment(card)?.Rank ?? 0;

    public static int GetStage(CardModel card) =>
        GetParasiteEnchantment(card)?.Stage ?? 0;

    public static int GetTriggersRemaining(CardModel card) =>
        GetParasiteEnchantment(card)?.TriggersRemaining ?? 0;

/// <summary>
    /// 宿主牌与血寄附魔提示共用的动态效果文本。
    ///
    /// 文本严格从附魔的 SavedProperty 读取种类、转数、阶段与剩余次数，
    /// 并在渲染时结合宿主牌类型、目标类型、血颅与来源升级状态计算下一次
    /// 实际结算值。因此卡面、悬浮提示、克隆、读档和复合附魔都会显示相同的
    /// 当前效果，而不会退化为静态说明。
    /// </summary>
    public static string? GetHostCardDynamicText(
        CardModel host
    )
    {
        ParasiteKind kind = NormalizeLegacyKind(GetKind(host));
        if (kind == ParasiteKind.None)
        {
            return null;
        }

        int rank = GetRank(host);
        int stage = Math.Max(0, GetStage(host));
        int triggersRemaining = GetTriggersRemaining(host);
        XueDaoParasiteEnchantment? parasite =
            GetParasiteEnchantment(host);
        bool upgraded = parasite?.SourceWasUpgraded ?? false;
        int skull = host.IsCanonical ||
            host.Owner?.Creature is not { } ownerCreature
                ? 0
                : XueDaoPowerSystem.GetXueLu(ownerCreature);
        int[] bloodQiRates = GetBloodQiTriggerPercentages(rank);
        int triggerIndex = Math.Clamp(stage, 0, bloodQiRates.Length - 1);
        int triggerChance = bloodQiRates[triggerIndex];
        int bloodQiValue = ScaleCeiling(
            GetBloodQiBaseValue(rank) + skull * 2 + (upgraded ? 2 : 0),
            triggerChance
        );
        int bloodQiBleed = ScaleCeiling(
            GetBloodQiBleed(rank),
            triggerChance
        );
        BloodMoonPhaseValues moonValues =
            GetBloodMoonPhaseValues(rank, Math.Clamp(stage, 0, 2));
        int moonSkullScale = Math.Clamp(stage, 0, 2) switch
        {
            0 => 3,
            1 => 4,
            _ => 5,
        };
        int fetusHostValue = 8 + rank * 3 + skull * 2;
        int fetusHostBleed = Math.Max(1, rank / 2);
        int fetusHatchBaseDamage = 16 + rank * 4;
        int fetusHatchBleed = 1 + Math.Max(1, rank / 3);
        int fetusHatchMarksMin = rank >= 6 ? 1 : 0;
        int fetusHatchMarksMax = rank >= 6 ? 2 : 0;

        string? entry = kind switch
        {
            ParasiteKind.BloodQi => host.Type switch
            {
                CardType.Attack when
                    host.TargetType == TargetType.AllEnemies =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_QI_ATTACK_ALL.cardText",
                CardType.Attack =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_QI_ATTACK.cardText",
                CardType.Skill =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_QI_SKILL.cardText",
                _ =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_QI_POWER.cardText",
            },
            ParasiteKind.BloodMoon => stage switch
            {
                <= 0 => "LU_GU_ZHEN_REN_CARD_PARASITE_CRESCENT_MOON.cardText",
                1 => "LU_GU_ZHEN_REN_CARD_PARASITE_WAXING_MOON.cardText",
                _ => "LU_GU_ZHEN_REN_CARD_PARASITE_FULL_MOON.cardText",
            },
            ParasiteKind.BloodFetus => host.Type switch
            {
                CardType.Attack when
                    host.TargetType == TargetType.AllEnemies =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_FETUS_ATTACK_ALL.cardText",
                CardType.Attack =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_FETUS_ATTACK.cardText",
                CardType.Skill =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_FETUS_SKILL.cardText",
                _ =>
                    "LU_GU_ZHEN_REN_CARD_PARASITE_BLOOD_FETUS_POWER.cardText",
            },
            _ => null,
        };

        if (string.IsNullOrEmpty(entry))
        {
            return null;
        }

        LocString text = new("cards", entry);
        text.Add("Rank", rank);
        text.Add("Stage", Math.Clamp(stage, 0, 3));
        text.Add("TriggersRemaining", triggersRemaining);
        text.Add("TriggerCount", bloodQiRates.Length);
        text.Add("TriggerIndex", triggerIndex + 1);
        text.Add("TriggerChance", triggerChance);
        text.Add("HostType", GetHostTypeDisplay(host));
        text.Add("ParasiteValue", bloodQiValue);
        text.Add("ParasiteBleed", bloodQiBleed);
        text.Add("ParasitePowerGain", GetBloodQiPowerGain(rank));
        text.Add("MoonBaseDamage", moonValues.BaseDamage);
        text.Add("MoonEnergyScale", moonValues.EnergyScale);
        text.Add("MoonSkullScale", moonSkullScale);
        text.Add("MoonUpgradeBonus", upgraded ? 3 : 0);
        text.Add(
            "MoonDamageBeforeEnergy",
            moonValues.BaseDamage + skull * moonSkullScale +
                (upgraded ? 3 : 0)
        );
        text.Add("MoonBleed", moonValues.TotalBleed);
        text.Add("MoonMarks", moonValues.TotalMarks);
        text.Add("FetusHostValue", fetusHostValue);
        text.Add("FetusHostBleed", fetusHostBleed);
        text.Add("FetusHatchBaseDamage", fetusHatchBaseDamage);
        text.Add("FetusHatchEnergyScale", 6);
        text.Add("FetusHatchSkullScale", 4);
        text.Add("FetusHatchDamageBeforeEnergy", fetusHatchBaseDamage + skull * 4);
        text.Add("FetusHatchBleed", fetusHatchBleed);
        text.Add("FetusHatchMarksMin", fetusHatchMarksMin);
        text.Add("FetusHatchMarksMax", fetusHatchMarksMax);
        return text.GetFormattedText();
    }

    private static string GetHostTypeDisplay(CardModel host)
    {
        string entry = host.Type switch
        {
            CardType.Attack when host.TargetType == TargetType.AllEnemies =>
                "LU_GU_ZHEN_REN_CARD_PARASITE_HOST_ATTACK_ALL",
            CardType.Attack => "LU_GU_ZHEN_REN_CARD_PARASITE_HOST_ATTACK",
            CardType.Skill => "LU_GU_ZHEN_REN_CARD_PARASITE_HOST_SKILL",
            CardType.Power => "LU_GU_ZHEN_REN_CARD_PARASITE_HOST_POWER",
            _ => "LU_GU_ZHEN_REN_CARD_PARASITE_HOST_OTHER",
        };
        return new LocString("cards", entry).GetFormattedText();
    }

    internal static void MarkResolving(CardModel card, bool resolving)
    {
        // 旧档可能恢复出 true；立即归零，绝不让已废弃的持久化标记参与逻辑。
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

    internal static async Task BreakIfExhaustedAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel? source = null
    )
    {
        if (!HasParasite(host) ||
            IsResolving(host) ||
            host.Pile?.Type != PileType.Exhaust)
        {
            return;
        }

        await BreakFetusAsync(choiceContext, host, source ?? host);
    }

    public static int GetBloodQiCost(int rank) => rank switch
    {
        <= 3 => 1,
        <= 6 => 2,
        _ => 3,
    };

    public static int GetBloodQiBaseValue(int rank) => rank switch
    {
        <= 1 => 6,
        2 => 8,
        3 => 10,
        4 => 12,
        5 => 15,
        6 => 18,
        7 => 21,
        8 => 25,
        _ => 30,
    };

    public static int GetBloodQiBleed(int rank) => rank switch
    {
        <= 2 => 1,
        <= 4 => 2,
        <= 6 => 3,
        <= 8 => 4,
        _ => 5,
    };

    public static int[] GetBloodQiTriggerPercentages(int rank) => rank switch
    {
        <= 3 => [100],
        4 or 5 => [100, 50],
        6 => [100, 75],
        7 => [100, 100],
        8 => [100, 75, 50],
        _ => [100, 100, 75],
    };

    public static BloodMoonPhaseValues GetBloodMoonPhaseValues(int rank, int phase)
    {
        rank = Math.Clamp(rank, 4, 9);
        phase = Math.Clamp(phase, 0, 2);
        return (rank, phase) switch
        {
            (4, 0) => new(8, 2, 1, 0),
            (4, 1) => new(12, 3, 2, 0),
            (4, 2) => new(18, 4, 2, 1),
            (5, 0) => new(10, 2, 1, 0),
            (5, 1) => new(14, 3, 2, 1),
            (5, 2) => new(22, 4, 3, 1),
            (6, 0) => new(12, 3, 1, 0),
            (6, 1) => new(17, 4, 2, 1),
            (6, 2) => new(27, 5, 3, 2),
            (7, 0) => new(14, 3, 2, 0),
            (7, 1) => new(20, 4, 3, 1),
            (7, 2) => new(32, 5, 4, 2),
            (8, 0) => new(16, 4, 2, 0),
            (8, 1) => new(24, 5, 3, 2),
            (8, 2) => new(38, 6, 4, 3),
            (9, 0) => new(19, 4, 3, 1),
            (9, 1) => new(29, 5, 4, 2),
            _ => new(46, 7, 5, 3),
        };
    }

    public static bool IsEligibleHost(CardModel card) =>
        !card.IsCanonical &&
        (card.CanBeGeneratedInCombat ||
         // 力道伴生牌虽为战斗生成牌，仍允许作为血道寄生宿主。
         card is ILiDaoCompanionCard ||
         // 血道衍生牌（刀翅血蝠/刀翅血蝠群/血蝠王）允许作为血道寄生宿主；
         // 遗骸是状态牌，被下方类型条件排除。
         card is AbstractXueDaoToken) &&
        card is not IGuWormCard &&
        card is not AbstractShaZhaoCard &&
        card is not ShaZhaoTuiYan &&
        card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    public static bool CanAttach(CardModel card, ParasiteKind incomingKind)
    {
        if (!IsEligibleHost(card))
        {
            return false;
        }

        ParasiteKind current = NormalizeLegacyKind(GetKind(card));

        // 血月/血胎为独立寄生体系：无需血气附魔作为前置，可直接植入
        // 无寄生宿主；仍保留“吞寄”升级链（血气→盈月起点的月相、
        // 血气/未满月月相→血胎）。禁止同级覆盖，血胎为寄生终点。
        if (current == ParasiteKind.None)
        {
            return incomingKind != ParasiteKind.None;
        }

        return incomingKind switch
        {
            ParasiteKind.BloodQi => false,
            ParasiteKind.BloodMoon =>
                current != ParasiteKind.BloodMoon &&
                current != ParasiteKind.BloodFetus,
            ParasiteKind.BloodFetus =>
                current != ParasiteKind.BloodFetus &&
                !(current == ParasiteKind.BloodMoon && GetStage(card) >= 2),
            _ => false,
        };
    }

    // 保留旧调用接口，兼容其他分支代码。
    public static bool CanAttach(CardModel card, bool allowBloodQiReplacement) =>
        CanAttach(
            card,
            allowBloodQiReplacement ? ParasiteKind.BloodMoon : ParasiteKind.BloodQi
        );

    public static async Task AttachAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        ParasiteKind kind,
        int rank,
        bool upgraded,
        CardModel sourceCard
    )
    {
        ParasiteKind previous = NormalizeLegacyKind(GetKind(host));
        int previousStage = GetStage(host);
        bool alreadyHadParasite = previous != ParasiteKind.None;

        int stage = 0;
        int remaining;

        switch (kind)
        {
            case ParasiteKind.BloodQi:
                remaining = GetBloodQiTriggerPercentages(rank).Length;
                break;

            case ParasiteKind.BloodMoon:
                // 吞入血气后从盈月开始。
                stage = previous == ParasiteKind.BloodQi ? 1 : 0;
                remaining = 3 - stage;
                break;

            case ParasiteKind.BloodFetus:
                stage = previous switch
                {
                    ParasiteKind.BloodQi => 1,
                    ParasiteKind.BloodMoon => Math.Clamp(previousStage + 1, 1, 2),
                    _ => 0,
                };
                remaining = 3 - stage;
                break;

            default:
                remaining = 0;
                break;
        }

        XueDaoParasiteEnchantment parasite =
            XueDaoEnchantmentSlotPatch.AttachOrRefreshParasite(
                host,
                rank
            );
        parasite.Configure(
            kind,
            rank,
            upgraded,
            stage,
            Math.Max(0, remaining),
            stage
        );
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        ClearLegacyState(host);

        if (!alreadyHadParasite)
        {
            await PowerCmd.Apply<XueJiPower>(
                choiceContext,
                sourceCard.Owner.Creature,
                1,
                sourceCard.Owner.Creature,
                sourceCard
            );
        }

        // 附着完成后把寄生关键词真正写入宿主卡牌。

        Entry.Logger.Info($"[血寄] {host.Id} 获得 {kind}，来源转数 {rank}，阶段 {stage}。");
    }

    public static async Task TriggerFromCardPlayAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        IReadOnlyCollection<uint> enemiesAliveBefore
    )
    {
        CardModel host = cardPlay.Card;
        if (!HasParasite(host))
        {
            return;
        }

        await TriggerCoreAsync(
            choiceContext,
            host,
            cardPlay.Target,
            Math.Max(0, cardPlay.Resources.EnergyValue),
            cardPlay,
            enemiesAliveBefore
        );
    }

    public static async Task TriggerDetachedAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel triggerSource
    )
    {
        if (!HasParasite(host) || host.Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        uint[] aliveBefore = combatState.Enemies
            .Where(enemy => enemy.IsAlive)
            .Select(enemy => enemy.CombatId)
            .OfType<uint>()
            .ToArray();

        Creature? target = GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => enemy.IsAlive)
            .OrderBy(enemy => enemy.CurrentHp)
            .ThenBy(enemy => enemy.CombatId)
            .FirstOrDefault();

        await TriggerCoreAsync(
            choiceContext,
            host,
            target,
            Math.Max(0, host.EnergyCost.GetAmountToSpend()),
            null,
            aliveBefore,
            triggerSource
        );
    }

    private static async Task TriggerCoreAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        Creature? target,
        int energyValue,
        CardPlay? cardPlay,
        IReadOnlyCollection<uint> enemiesAliveBefore,
        CardModel? triggerSource = null
    )
    {
        ParasiteKind kind = NormalizeLegacyKind(GetKind(host));
        int rank = GetRank(host);
        bool upgraded = GetParasiteEnchantment(host)?.SourceWasUpgraded ?? false;
        int stage = GetStage(host);
        CardModel effectSource = triggerSource ?? host;
        bool completed = false;

        switch (kind)
        {
            case ParasiteKind.BloodQi:
                completed = await TriggerBloodQiAsync(
                    choiceContext, host, effectSource, target, rank, upgraded, stage, cardPlay
                );
                break;

            case ParasiteKind.BloodMoon:
                completed = await TriggerBloodMoonAsync(
                    choiceContext, host, effectSource, rank, upgraded, stage, energyValue, cardPlay
                );
                break;

            case ParasiteKind.BloodFetus:
                completed = await TriggerBloodFetusAsync(
                    choiceContext, host, effectSource, target, rank, energyValue, cardPlay
                );
                break;
        }

        // 击杀产生遗骸统一由 XueDaoRemainsKillPatch 在 Hook.AfterDeath
        // 检测（死亡瞬间尸体仍有效）；此处不再做“出牌前快照 + 事后对比”。
        if (completed)
        {
            await HatchAsync(choiceContext, host, effectSource, kind, rank);
            return;
        }

        // 消耗宿主在本次出牌后仍有未成熟寄生：触发一次后破胎。
        if (host.Pile?.Type == PileType.Exhaust)
        {
            await BreakFetusAsync(choiceContext, host, effectSource);
        }
    }

    private static async Task<bool> TriggerBloodQiAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        Creature? target,
        int rank,
        bool upgraded,
        int stage,
        CardPlay? cardPlay
    )
    {
        int[] rates = GetBloodQiTriggerPercentages(rank);
        int rate = rates[Math.Clamp(stage, 0, rates.Length - 1)];
        int skull = XueDaoPowerSystem.GetXueLu(host.Owner.Creature);
        int value = ScaleCeiling(GetBloodQiBaseValue(rank) + skull * 2 + (upgraded ? 2 : 0), rate);
        int bleed = ScaleCeiling(GetBloodQiBleed(rank), rate);

        await TriggerHostAdaptationAsync(
            choiceContext, host, effectSource, target, value, bleed,
            GetBloodQiPowerGain(rank), cardPlay
        );

        return Advance(host, rates.Length);
    }

    private static async Task<bool> TriggerBloodMoonAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        int rank,
        bool upgraded,
        int phase,
        int energyValue,
        CardPlay? cardPlay
    )
    {
        phase = Math.Clamp(phase, 0, 2);
        BloodMoonPhaseValues values = GetBloodMoonPhaseValues(rank, phase);
        int skull = XueDaoPowerSystem.GetXueLu(host.Owner.Creature);
        int skullBonus = phase switch { 0 => 3, 1 => 4, _ => 5 };
        int totalDamage = values.BaseDamage +
            values.EnergyScale * energyValue +
            skull * skullBonus +
            (upgraded ? 3 : 0);

        await DistributeDamage(choiceContext, effectSource, cardPlay, totalDamage);
        await DistributeDebuffs(
            choiceContext,
            effectSource,
            values.TotalBleed,
            values.TotalMarks
        );

        return Advance(host, 3);
    }

    private static async Task<bool> TriggerBloodFetusAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        Creature? target,
        int rank,
        int energyValue,
        CardPlay? cardPlay
    )
    {
        int skull = XueDaoPowerSystem.GetXueLu(host.Owner.Creature);
        int singleValue = 8 + rank * 3 + skull * 2;
        int hostBleed = Math.Max(1, rank / 2);

        await TriggerHostAdaptationAsync(
            choiceContext, host, effectSource, target, singleValue, hostBleed,
            powerBloodGain: 2, cardPlay
        );

        bool completed = Advance(host, 3);
        if (!completed)
        {
            return false;
        }

        int totalDamage = 16 + rank * 4 + energyValue * 6 + skull * 4;
        int bleed = 1 + Math.Max(1, rank / 3);
        int marks = rank >= 6 ? Math.Min(2, Math.Max(1, energyValue)) : 0;
        await DistributeDamage(choiceContext, effectSource, cardPlay, totalDamage);
        await DistributeDebuffs(choiceContext, effectSource, bleed, marks);
        return true;
    }

    private static async Task TriggerHostAdaptationAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        Creature? target,
        int value,
        int bleed,
        int powerBloodGain,
        CardPlay? cardPlay
    )
    {
        switch (host.Type)
        {
            case CardType.Attack when host.TargetType == TargetType.AllEnemies:
                await DistributeDamage(choiceContext, effectSource, cardPlay, value);
                await DistributeDebuffs(choiceContext, effectSource, bleed, 0);
                break;

            case CardType.Attack when target is { IsAlive: true }:
                await DealAttack(choiceContext, effectSource, cardPlay, target, value);
                if (target.IsAlive)
                {
                    await XueDaoPowerSystem.ApplyLiuXue(choiceContext, effectSource, target, bleed);
                }
                break;

            case CardType.Skill:
                await CreatureCmd.GainBlock(
                    host.Owner.Creature,
                    value,
                    ValueProp.Unpowered | ValueProp.Move,
                    cardPlay
                );
                await XueDaoPowerSystem.GainXueYuanFromCardEffect(choiceContext, effectSource, 1);
                break;

            case CardType.Power:
                await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                    choiceContext, effectSource, powerBloodGain
                );
                break;
        }
    }

    private static bool Advance(CardModel host, int totalStages)
    {
        XueDaoParasiteEnchantment? parasite =
            GetParasiteEnchantment(host);
        if (parasite == null)
        {
            return false;
        }

        int completed = Math.Min(
            totalStages,
            parasite.TriggersCompleted + 1
        );
        parasite.Advance(completed, totalStages);
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);

        // 阶段推进（残月→盈月→满月）后刷新阶段关键词。

        return completed >= totalStages;
    }

    private static async Task HatchAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel source,
        ParasiteKind kind,
        int rank
    )
    {
        int bloodGain = kind switch
        {
            ParasiteKind.BloodQi when rank >= 9 => 2,
            ParasiteKind.BloodQi when rank >= 6 => 1,
            ParasiteKind.BloodMoon when rank >= 9 => 2,
            ParasiteKind.BloodMoon when rank >= 6 => 1,
            _ => 0,
        };

        if (bloodGain > 0)
        {
            await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                choiceContext, source, bloodGain
            );
        }

        if (rank >= 9 && kind == ParasiteKind.BloodMoon)
        {
            await DrawOneAsync(choiceContext, host.Owner);
        }

        await ClearAsync(choiceContext, host, source);
    }

    private static async Task BreakFetusAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel source
    )
    {
        await XueDaoPowerSystem.GainXueYuanFromCardEffect(choiceContext, source, 1);
        await ClearAsync(choiceContext, host, source);
    }

    private static async Task DrawOneAsync(PlayerChoiceContext choiceContext, Player player)
    {
        try
        {
            if (DrawInternalMethod?.Invoke(
                    null,
                    new object?[] { choiceContext, 1m, player, true }
                ) is Task<IEnumerable<CardModel>> drawTask)
            {
                await drawTask;
                return;
            }

            Entry.Logger.Warn("[血寄] 未找到 CardPileCmd.DrawInternal，九转孵化跳过抽牌。");
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[血寄] 九转月相孵化抽牌失败，已跳过：{exception.GetBaseException().Message}"
            );
        }
    }

    private static int GetBloodQiPowerGain(int rank) => rank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    private static int ScaleCeiling(int value, int percent) =>
        Math.Max(1, (value * percent + 99) / 100);

    private static ParasiteKind NormalizeLegacyKind(ParasiteKind kind) =>
        kind == ParasiteKind.CrescentMoon ? ParasiteKind.BloodMoon : kind;

    private static async Task DealAttack(
        PlayerChoiceContext choiceContext,
        CardModel source,
        CardPlay? cardPlay,
        Creature target,
        decimal amount
    )
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        await DamageCmd.Attack(amount)
            .FromCard(source, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    private static async Task DistributeDamage(
        PlayerChoiceContext choiceContext,
        CardModel source,
        CardPlay? cardPlay,
        int totalDamage
    )
    {
        if (totalDamage <= 0 || source.Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        Creature[] enemies = GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => enemy.IsAlive)
            .ToArray();

        if (enemies.Length == 0)
        {
            return;
        }

        int perEnemy = totalDamage / enemies.Length;
        int remainder = totalDamage % enemies.Length;
        for (int index = 0; index < enemies.Length; index++)
        {
            await DealAttack(
                choiceContext,
                source,
                cardPlay,
                enemies[index],
                perEnemy + (index < remainder ? 1 : 0)
            );
        }
    }

    private static async Task DistributeDebuffs(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int totalBleed,
        int totalMarks
    )
    {
        if (source.Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        Creature[] enemies = GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => enemy.IsAlive)
            .ToArray();

        if (enemies.Length == 0)
        {
            return;
        }

        for (int index = 0; index < enemies.Length; index++)
        {
            int bleed = totalBleed / enemies.Length + (index < totalBleed % enemies.Length ? 1 : 0);
            int marks = totalMarks / enemies.Length + (index < totalMarks % enemies.Length ? 1 : 0);
            if (bleed > 0)
            {
                await XueDaoPowerSystem.ApplyLiuXue(choiceContext, source, enemies[index], bleed);
            }
            if (marks > 0)
            {
                await XueDaoPowerSystem.ApplyXueYin(choiceContext, source, enemies[index], marks);
            }
        }
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

        // 寄生解除后立即从卡牌上移除全部寄生关键词。

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
    )
    {
        return XueDaoEnchantmentSlotPatch.TryGetParasite(card) ??
            TryMigrateLegacyParasite(card);
    }

    private static XueDaoParasiteEnchantment? TryMigrateLegacyParasite(
        CardModel card
    )
    {
        int rawKind = LegacyKindState[card];
        if (!Enum.IsDefined(typeof(ParasiteKind), rawKind) ||
            (ParasiteKind)rawKind == ParasiteKind.None ||
            card.IsCanonical ||
            !IsEligibleHost(card))
        {
            return null;
        }

        ParasiteKind kind = NormalizeLegacyKind((ParasiteKind)rawKind);
        int rank = Math.Max(1, LegacyRankState[card]);
        int stage = Math.Max(0, LegacyStageState[card]);
        int remaining = Math.Max(
            0,
            LegacyTriggersRemainingState[card]
        );
        int completed = Math.Max(
            0,
            LegacyTriggersCompletedState[card]
        );

        XueDaoParasiteEnchantment parasite =
            XueDaoEnchantmentSlotPatch.AttachOrRefreshParasite(card, rank);
        parasite.Configure(
            kind,
            rank,
            LegacyUpgradedState[card],
            stage,
            remaining,
            completed
        );
        XueDaoEnchantmentSlotPatch.NotifyParasiteChanged(parasite);
        ClearLegacyState(card);

        Entry.Logger.Info(
            $"[血寄] 已将 {card.Id} 的旧版旁路状态迁移为原生附魔。"
        );
        return parasite;
    }

    private static void ClearLegacyState(CardModel card)
    {
        LegacyKindState[card] = (int)ParasiteKind.None;
        LegacyRankState[card] = 0;
        LegacyUpgradedState[card] = false;
        LegacyStageState[card] = 0;
        LegacyTriggersRemainingState[card] = 0;
        LegacyTriggersCompletedState[card] = 0;
        LegacyResolvingState[card] = false;
    }
}
