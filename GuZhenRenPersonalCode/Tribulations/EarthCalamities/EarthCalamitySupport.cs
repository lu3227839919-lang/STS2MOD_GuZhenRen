// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌。
// 主要类型：EarthCalamitySupport、EarthCalamityPower、TribulationStatusCard。
// 实现要点：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 实现补充：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 实现补充：战斗衍生牌必须由当前 CombatState 创建，确保网络卡号和牌堆归属有效。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Cards;
using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Tribulations.EarthCalamities;

internal static class EarthCalamitySupport
{
    internal static int ScaleFlat(TribulationContext context, int rankSixValue)
    {
        decimal multiplier = context.CurrentRank switch
        {
            7 => 1.20m,
            8 => 1.45m,
            9 => 1.70m,
            _ => 1m,
        };
        return (int)Math.Ceiling(rankSixValue * multiplier);
    }

    internal static int ScaleFlat(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        int rankSixValue)
    {
        int rank = ApertureSystem.IsInitialized
            ? ApertureSystem.GetState(player).Rank
            : 6;
        decimal multiplier = rank switch
        {
            7 => 1.20m,
            8 => 1.45m,
            9 => 1.70m,
            _ => 1m,
        };
        return (int)Math.Ceiling(rankSixValue * multiplier);
    }

    internal static int PercentCeiling(Creature creature, decimal percent) =>
        Math.Max(1, (int)Math.Ceiling(creature.MaxHp * percent));

    internal static async Task ApplyAnchorPowerAsync<T>(TribulationContext context)
        where T : PowerModel
    {
        if (context.Leader.GetPower<T>() != null)
            return;
        await PowerCmd.Apply<T>(
            new ThrowingPlayerChoiceContext(),
            context.Leader,
            1m,
            applier: null,
            cardSource: null,
            silent: false);
    }

    internal static async Task AddStatusToDiscardAsync<T>(
        TribulationContext context,
        int count = 1)
        where T : CardModel
    {
        for (int i = 0; i < Math.Max(0, count); i++)
        {
            CardModel card = context.Combat.CreateCard(
                ModelDb.Card<T>(),
                context.Player);
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Discard,
                context.Player);
        }
    }

    internal static async Task GainBlockAsync(Creature target, decimal amount)
    {
        _ = await CreatureCmd.GainBlock(
            target,
            Math.Max(0m, amount),
            ValueProp.Unpowered,
            cardPlay: null,
            fast: false);
    }

    internal static Task HealAsync(Creature target, decimal amount) =>
        CreatureCmd.Heal(target, Math.Max(0m, amount));

    internal static async Task DamagePlayerAsync(
        TribulationContext context,
        int amount,
        bool unblockable = false)
    {
        ValueProp props = ValueProp.Unpowered;
        if (unblockable)
            props |= ValueProp.Unblockable;

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            context.Player.Creature,
            Math.Max(0, amount),
            props,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }

    internal static void RefreshAnchorPower<T>(TribulationContext context)
        where T : EarthCalamityPower
    {
        context.Leader.GetPower<T>()?.RefreshDisplay();
    }
}

public abstract class EarthCalamityPower : ModPowerTemplate
{
    protected abstract string PrimaryCounterKey { get; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => IsMutable
        ? TribulationStateStore.ReadLeaderCounter(Owner, PrimaryCounterKey)
        : 0;

    /// <summary>
    /// 灾劫 Power 的图标基名（不含尺寸后缀）。每个地灾在 images/power 下
    /// 有同名图标（如 XueYuePower-64x64.png / XueYuePower-256x256.png）。
    /// 基类默认使用通用战斗占位图标；具体地灾覆盖为各自类名。
    /// </summary>
    protected virtual string IconBaseName => "LiDaoBattlePower";

    public override PowerAssetProfile AssetProfile => new(
        IconPath:
            $"res://GuZhenRenPersonal/images/power/{IconBaseName}-64x64.png",
        BigIconPath:
            $"res://GuZhenRenPersonal/images/power/{IconBaseName}-256x256.png"
    );

    public void RefreshDisplay() => InvokeDisplayAmountChanged();
}

public abstract class TribulationStatusCard :
    AbstractGuZhenRenGeneratedCard,
    ITribulationGeneratedObject
{
    protected virtual bool ExhaustAtTurnEnd => true;

    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        ExhaustAtTurnEnd
            ? [CardKeyword.Unplayable, CardKeyword.Ethereal]
            : [CardKeyword.Unplayable];

    protected TribulationStatusCard()
        : base(-1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }

    protected override void OnUpgrade() { }
}
