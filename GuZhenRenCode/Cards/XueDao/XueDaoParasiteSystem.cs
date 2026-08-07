using System.Reflection;

using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Multiplayer;
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

    private static readonly SavedAttachedState<CardModel, int> KindState =
        new("gu_zhen_ren.xue_dao.parasite_kind", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> RankState =
        new("gu_zhen_ren.xue_dao.parasite_rank", static () => 0);
    private static readonly SavedAttachedState<CardModel, bool> UpgradedState =
        new("gu_zhen_ren.xue_dao.parasite_upgraded", static () => false);
    private static readonly SavedAttachedState<CardModel, int> StageState =
        new("gu_zhen_ren.xue_dao.parasite_stage", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> TriggersRemainingState =
        new("gu_zhen_ren.xue_dao.parasite_triggers_remaining", static () => 0);
    private static readonly SavedAttachedState<CardModel, int> TriggersCompletedState =
        new("gu_zhen_ren.xue_dao.parasite_triggers_completed", static () => 0);
    private static readonly SavedAttachedState<CardModel, bool> ResolvingState =
        new("gu_zhen_ren.xue_dao.parasite_resolving", static () => false);

    private static readonly MethodInfo? DrawInternalMethod = typeof(CardPileCmd).GetMethod(
        "DrawInternal",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        types: [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)],
        modifiers: null
    );

    public static bool HasParasite(CardModel? card) =>
        card != null && GetKind(card) != ParasiteKind.None;

    public static ParasiteKind GetKind(CardModel card)
    {
        int value = KindState[card];
        return Enum.IsDefined(typeof(ParasiteKind), value)
            ? (ParasiteKind)value
            : ParasiteKind.None;
    }

    public static int GetRank(CardModel card) => Math.Max(0, RankState[card]);
    public static int GetStage(CardModel card) => Math.Max(0, StageState[card]);
    public static int GetTriggersRemaining(CardModel card) => Math.Max(0, TriggersRemainingState[card]);

    /// <summary>
    /// 按寄生类型与阶段返回宿主牌应显示的全部寄生关键词。
    /// 关键词会真正写入卡牌 LocalKeywords（附着时 AddKeyword，
    /// 解除时 RemoveKeyword），卡面以关键词标签展示附魔状态。
    /// </summary>
    public static IEnumerable<CardKeyword> GetParasiteKeywords(
        ParasiteKind kind,
        int stage,
        int triggerCount
    )
    {
        kind = NormalizeLegacyKind(kind);
        if (kind == ParasiteKind.None)
        {
            yield break;
        }

        // 破胎与孵化是所有寄生共有的机制词。
        yield return GuZhenRenKeywords.PoTai;
        yield return GuZhenRenKeywords.FuHua;

        switch (kind)
        {
            case ParasiteKind.BloodQi:
                // 血气 X：X 为宿主牌触发次数，按转数 1～3 次。
                yield return triggerCount switch
                {
                    <= 1 => GuZhenRenKeywords.XueQi1,
                    2 => GuZhenRenKeywords.XueQi2,
                    _ => GuZhenRenKeywords.XueQi3,
                };
                break;

            case ParasiteKind.BloodMoon:
                yield return GuZhenRenKeywords.YueXiang;
                yield return stage switch
                {
                    <= 0 => GuZhenRenKeywords.CanYue,
                    1 => GuZhenRenKeywords.YingYue,
                    _ => GuZhenRenKeywords.ManYue,
                };
                break;

            case ParasiteKind.BloodFetus:
                yield return GuZhenRenKeywords.XueTai;
                yield return GuZhenRenKeywords.TaiDong;
                yield return GuZhenRenKeywords.TunJi;
                break;
        }
    }

    /// <summary>
    /// 把宿主牌本地的寄生关键词与当前寄生状态对齐：先移除全部
    /// 寄生关键词，再按当前类型/阶段写入。附着、阶段推进、解除
    /// 三处都调用它，保证卡面关键词即时反映寄生状态。
    /// </summary>
    public static void RefreshHostKeywords(CardModel host)
    {
        foreach (CardKeyword keyword in
                 GuZhenRenKeywords.ParasiteKeywords)
        {
            // 对不存在的关键词调用 RemoveKeyword 无副作用（集合移除失败即忽略）。
            host.RemoveKeyword(keyword);
        }

        ParasiteKind kind = NormalizeLegacyKind(GetKind(host));
        if (kind == ParasiteKind.None)
        {
            return;
        }

        int triggerCount = kind == ParasiteKind.BloodQi
            ? GetBloodQiTriggerPercentages(GetRank(host)).Length
            : 0;

        foreach (CardKeyword keyword in
                 GetParasiteKeywords(
                     kind,
                     GetStage(host),
                     triggerCount
                 ))
        {
            host.AddKeyword(keyword);
        }
    }

    /// <summary>
    /// 宿主牌卡面动态附加文本：显示寄生关键词对应的当前效果与数值。
    /// 数值随蛊虫转数、触发次数、阶段动态注入，各端由同一份
    /// SavedAttachedState 数据渲染，保证多人一致。
    /// 无寄生时返回 null。
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
        int stage = GetStage(host);
        string? entry = kind switch
        {
            ParasiteKind.BloodQi =>
                "GU_ZHEN_REN_CARD_PARASITE_BLOOD_QI.cardText",
            ParasiteKind.BloodMoon => stage switch
            {
                <= 0 => "GU_ZHEN_REN_CARD_PARASITE_CRESCENT_MOON.cardText",
                1 => "GU_ZHEN_REN_CARD_PARASITE_WAXING_MOON.cardText",
                _ => "GU_ZHEN_REN_CARD_PARASITE_FULL_MOON.cardText",
            },
            ParasiteKind.BloodFetus =>
                "GU_ZHEN_REN_CARD_PARASITE_BLOOD_FETUS.cardText",
            _ => null,
        };

        if (string.IsNullOrEmpty(entry))
        {
            return null;
        }

        LocString text = new("cards", entry);
        text.Add("Rank", rank);
        text.Add("Stage", Math.Clamp(stage, 0, 3));
        text.Add("TriggersRemaining", GetTriggersRemaining(host));
        text.Add("TriggerCount", GetBloodQiTriggerPercentages(rank).Length);
        text.Add("ParasiteValue", GetBloodQiBaseValue(rank));
        text.Add("ParasiteBleed", GetBloodQiBleed(rank));
        return text.GetFormattedText();
    }

    internal static void MarkResolving(CardModel card, bool resolving) =>
        ResolvingState[card] = resolving;

    internal static bool IsResolving(CardModel card) => ResolvingState[card];

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
        card.CanBeGeneratedInCombat &&
        card is not IGuWormCard &&
        card is not AbstractShaZhaoCard &&
        card is not AbstractXueDaoToken &&
        card is not ShaZhaoTuiYan &&
        card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    public static bool CanAttach(CardModel card, ParasiteKind incomingKind)
    {
        if (!IsEligibleHost(card))
        {
            return false;
        }

        ParasiteKind current = NormalizeLegacyKind(GetKind(card));
        if (current == ParasiteKind.None)
        {
            return true;
        }

        return incomingKind switch
        {
            ParasiteKind.BloodQi => false,
            ParasiteKind.BloodMoon => current == ParasiteKind.BloodQi,
            ParasiteKind.BloodFetus =>
                current == ParasiteKind.BloodQi ||
                (current == ParasiteKind.BloodMoon && GetStage(card) < 2),
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

        KindState[host] = (int)kind;
        RankState[host] = Math.Max(1, rank);
        UpgradedState[host] = upgraded;
        StageState[host] = stage;
        TriggersCompletedState[host] = stage;
        TriggersRemainingState[host] = Math.Max(0, remaining);

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
        RefreshHostKeywords(host);

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
        bool upgraded = UpgradedState[host];
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

        await CreateRemainsForNewDeaths(host.Owner, enemiesAliveBefore);

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
        int completed = Math.Min(totalStages, TriggersCompletedState[host] + 1);
        TriggersCompletedState[host] = completed;
        StageState[host] = completed;
        TriggersRemainingState[host] = Math.Max(0, totalStages - completed);

        // 阶段推进（残月→盈月→满月）后刷新阶段关键词。
        RefreshHostKeywords(host);

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

    private static async Task CreateRemainsForNewDeaths(
        Player owner,
        IReadOnlyCollection<uint> enemiesAliveBefore
    )
    {
        if (owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        int count = enemiesAliveBefore
            .Select(id => combatState.GetCreature(id))
            .Where(enemy => enemy is { IsDead: true, IsPrimaryEnemy: true })
            .Take(2)
            .Count();

        if (count > 0)
        {
            await XueDaoCardSystem.AddRemains(owner, count);
        }
    }

    private static async Task ClearAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel source
    )
    {
        KindState[host] = (int)ParasiteKind.None;
        RankState[host] = 0;
        UpgradedState[host] = false;
        StageState[host] = 0;
        TriggersRemainingState[host] = 0;
        TriggersCompletedState[host] = 0;
        ResolvingState[host] = false;

        // 寄生解除后立即从卡牌上移除全部寄生关键词。
        RefreshHostKeywords(host);

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
}
