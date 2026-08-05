using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 血道寄生的卡牌附加状态与统一结算入口。
/// 寄生只改造已有普通牌，不生成衍生牌；打出宿主后寄生消失。
/// </summary>
public static class XueDaoParasiteSystem
{
    public enum ParasiteKind
    {
        None = 0,
        BloodQi = 1,
        CrescentMoon = 2,
        BloodMoon = 3,
        BloodFetus = 4,
    }

    private static readonly SavedAttachedState<CardModel, int>
        KindState = new(
            "gu_zhen_ren.xue_dao.parasite_kind",
            static () => 0
        );

    private static readonly SavedAttachedState<CardModel, int>
        RankState = new(
            "gu_zhen_ren.xue_dao.parasite_rank",
            static () => 0
        );

    private static readonly SavedAttachedState<CardModel, bool>
        UpgradedState = new(
            "gu_zhen_ren.xue_dao.parasite_upgraded",
            static () => false
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

    public static int GetRank(CardModel card) =>
        Math.Max(0, RankState[card]);

    public static bool IsEligibleHost(CardModel card)
    {
        return !card.IsCanonical &&
            card.CanBeGeneratedInCombat &&
            card is not IGuWormCard &&
            card is not AbstractShaZhaoCard &&
            card is not AbstractXueDaoToken &&
            card.Type is CardType.Attack or CardType.Skill or CardType.Power;
    }

    public static bool CanAttach(
        CardModel card,
        bool allowBloodQiReplacement
    )
    {
        if (!IsEligibleHost(card))
        {
            return false;
        }

        ParasiteKind current = GetKind(card);
        return current == ParasiteKind.None ||
            (allowBloodQiReplacement && current == ParasiteKind.BloodQi);
    }

    public static async Task AttachAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        ParasiteKind kind,
        int rank,
        bool upgraded,
        CardModel sourceCard
    )
    {
        bool alreadyHadParasite = HasParasite(host);

        KindState[host] = (int)kind;
        RankState[host] = Math.Max(1, rank);
        UpgradedState[host] = upgraded;

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

        Entry.Logger.Info(
            $"[血寄] {host.Id} 获得 {kind}，来源转数 {rank}。"
        );
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

    /// <summary>
    /// 供血月祭等效果直接剥离并触发寄生。单体攻击寄生没有玩家选定
    /// 目标时，按确定性顺序选择当前生命最低的存活敌人。
    /// </summary>
    public static async Task TriggerDetachedAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel triggerSource
    )
    {
        if (!HasParasite(host) ||
            host.Owner.Creature.CombatState is not { } combatState)
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

        int energyValue = Math.Max(
            0,
            host.EnergyCost.GetAmountToSpend()
        );

        await TriggerCoreAsync(
            choiceContext,
            host,
            target,
            energyValue,
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
        ParasiteKind kind = GetKind(host);
        int rank = GetRank(host);
        bool upgraded = UpgradedState[host];
        CardModel effectSource = triggerSource ?? host;

        switch (kind)
        {
            case ParasiteKind.BloodQi:
                await TriggerBloodQiAsync(
                    choiceContext,
                    host,
                    effectSource,
                    target,
                    rank,
                    upgraded,
                    cardPlay
                );
                break;

            case ParasiteKind.CrescentMoon:
            case ParasiteKind.BloodMoon:
                await TriggerBloodMoonAsync(
                    choiceContext,
                    host,
                    effectSource,
                    rank,
                    energyValue,
                    kind == ParasiteKind.BloodMoon,
                    cardPlay
                );
                break;

            case ParasiteKind.BloodFetus:
                await TriggerBloodFetusAsync(
                    choiceContext,
                    host,
                    effectSource,
                    target,
                    rank,
                    energyValue,
                    cardPlay
                );
                break;
        }

        await CreateRemainsForNewDeaths(
            host.Owner,
            enemiesAliveBefore
        );

        await ClearAsync(choiceContext, host, effectSource);
    }

    private static async Task TriggerBloodQiAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        Creature? target,
        int rank,
        bool upgraded,
        CardPlay? cardPlay
    )
    {
        int skull = XueDaoPowerSystem.GetXueLu(host.Owner.Creature);
        int value = rank switch
        {
            <= 1 => 6,
            2 => 8,
            3 => 11,
            4 => 15,
            _ => 20,
        } + skull * 2 + (upgraded ? 2 : 0);

        int bleed = rank switch
        {
            <= 2 => 1,
            <= 4 => 2,
            _ => 3,
        };

        switch (host.Type)
        {
            case CardType.Attack when host.TargetType == TargetType.AllEnemies:
                // 群体攻击宿主仍使用“全场总额度”，避免寄生收益随敌人数
                // 复制；伤害和流血按确定性顺序分配。
                await DistributeDamage(
                    choiceContext,
                    effectSource,
                    cardPlay,
                    value
                );
                await DistributeDebuffs(
                    choiceContext,
                    effectSource,
                    bleed,
                    totalMarks: 0
                );
                break;

            case CardType.Attack when target is { IsAlive: true }:
                await DealAttack(
                    choiceContext,
                    effectSource,
                    cardPlay,
                    target,
                    value
                );
                if (target.IsAlive)
                {
                    await XueDaoPowerSystem.ApplyLiuXue(
                        choiceContext,
                        effectSource,
                        target,
                        bleed
                    );
                }
                break;

            case CardType.Skill:
                await CreatureCmd.GainBlock(
                    host.Owner.Creature,
                    value,
                    ValueProp.Unpowered | ValueProp.Move,
                    cardPlay
                );
                await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                    choiceContext,
                    effectSource,
                    1
                );
                break;

            case CardType.Power:
                await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                    choiceContext,
                    effectSource,
                    2
                );
                break;
        }
    }

    private static async Task TriggerBloodMoonAsync(
        PlayerChoiceContext choiceContext,
        CardModel host,
        CardModel effectSource,
        int rank,
        int energyValue,
        bool full,
        CardPlay? cardPlay
    )
    {
        int skull = XueDaoPowerSystem.GetXueLu(host.Owner.Creature);
        int totalDamage = full
            ? GetFullMoonBase(rank) + energyValue * GetFullMoonScale(rank) + skull * 4
            : GetCrescentBase(rank) + energyValue * GetCrescentScale(rank) + skull * 3;

        int totalBleed = full
            ? GetFullMoonBleed(rank) + Math.Min(energyValue, 4)
            : GetCrescentBleed(rank);

        int totalMarks = full
            ? rank switch
            {
                <= 2 => 0,
                <= 4 => 1,
                _ => Math.Min(energyValue, 4),
            }
            : 0;

        await DistributeDamage(
            choiceContext,
            effectSource,
            cardPlay,
            totalDamage
        );
        await DistributeDebuffs(
            choiceContext,
            effectSource,
            totalBleed,
            totalMarks
        );
    }

    private static async Task TriggerBloodFetusAsync(
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

        if (host.Type == CardType.Attack &&
            host.TargetType == TargetType.AllEnemies)
        {
            await DistributeDamage(
                choiceContext,
                effectSource,
                cardPlay,
                singleValue
            );
            await DistributeDebuffs(
                choiceContext,
                effectSource,
                Math.Max(1, rank / 2),
                totalMarks: 0
            );
        }
        else if (host.Type == CardType.Attack &&
                 target is { IsAlive: true })
        {
            await DealAttack(
                choiceContext,
                effectSource,
                cardPlay,
                target,
                singleValue
            );
            if (target.IsAlive)
            {
                await XueDaoPowerSystem.ApplyLiuXue(
                    choiceContext,
                    effectSource,
                    target,
                    Math.Max(1, rank / 2)
                );
            }
        }
        else if (host.Type == CardType.Skill)
        {
            await CreatureCmd.GainBlock(
                host.Owner.Creature,
                singleValue,
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );
        }
        else if (host.Type == CardType.Power)
        {
            await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                choiceContext,
                effectSource,
                2
            );
        }

        int totalDamage = 16 + rank * 4 + energyValue * 6 + skull * 4;
        int bleed = 1 + Math.Max(1, rank / 3);
        int marks = rank >= 6 ? Math.Min(2, Math.Max(1, energyValue)) : 0;

        await DistributeDamage(
            choiceContext,
            effectSource,
            cardPlay,
            totalDamage
        );
        await DistributeDebuffs(
            choiceContext,
            effectSource,
            bleed,
            marks
        );
    }

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

        await DamageCmd
            .Attack(amount)
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
            int damage = perEnemy + (index < remainder ? 1 : 0);
            await DealAttack(
                choiceContext,
                source,
                cardPlay,
                enemies[index],
                damage
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
            int bleed = totalBleed / enemies.Length +
                (index < totalBleed % enemies.Length ? 1 : 0);
            int marks = totalMarks / enemies.Length +
                (index < totalMarks % enemies.Length ? 1 : 0);

            if (bleed > 0)
            {
                await XueDaoPowerSystem.ApplyLiuXue(
                    choiceContext,
                    source,
                    enemies[index],
                    bleed
                );
            }

            if (marks > 0)
            {
                await XueDaoPowerSystem.ApplyXueYin(
                    choiceContext,
                    source,
                    enemies[index],
                    marks
                );
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

    private static int GetCrescentBase(int rank) => rank switch
    {
        <= 1 => 8,
        2 => 10,
        3 => 12,
        4 => 15,
        5 => 18,
        6 => 22,
        _ => 26,
    };

    private static int GetCrescentScale(int rank) => rank switch
    {
        <= 2 => 3,
        <= 4 => 4,
        <= 6 => 5,
        _ => 6,
    };

    private static int GetCrescentBleed(int rank) => rank switch
    {
        <= 3 => 1,
        <= 6 => 2,
        _ => 3,
    };

    private static int GetFullMoonBase(int rank) => rank switch
    {
        <= 1 => 14,
        2 => 17,
        3 => 20,
        4 => 24,
        5 => 28,
        6 => 32,
        _ => 36,
    };

    private static int GetFullMoonScale(int rank) => rank switch
    {
        <= 2 => 5,
        <= 4 => 6,
        <= 6 => 7,
        _ => 8,
    };

    private static int GetFullMoonBleed(int rank) => rank switch
    {
        <= 2 => 2,
        <= 4 => 3,
        <= 6 => 4,
        _ => 5,
    };
}
