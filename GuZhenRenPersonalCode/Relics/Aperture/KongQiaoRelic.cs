using GuZhenRen.Aperture;
using GuZhenRen.Cards;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;
using GuZhenRen.RestSite;

using GuZhenRen.Tribulations.Core;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Relics;

/// <summary>
/// 方源的空窍起始遗物。
/// 使用单个遗物模型承载 1～9 转状态；当前版本不包含灾劫和十转。
/// </summary>
[RegisterRelic(typeof(GuZhenRenRelicPool))]
[RegisterCharacterStarterRelic(typeof(GuZhenRenCharacter))]
public sealed class KongQiaoRelic
    : ModRelicTemplate, ISecondaryResourceHookListener
{
    private int _lastVisualRank = -1;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount
    {
        get
        {
            if (!IsMutable || !ApertureSystem.IsInitialized)
            {
                return ApertureProgression.MinimumRank;
            }

            try
            {
                return ApertureSystem.GetState(Owner).Rank;
            }
            catch
            {
                return ApertureProgression.MinimumRank;
            }
        }
    }

    /// <summary>
    /// 所有图片路径均显式声明为非空值。
    /// 对应资源缺失时，RitsuLib 分析器和运行时会正常报告警告。
    /// 未提供单独轮廓图，因此轮廓字段交给模板回退。
    /// </summary>
    public override RelicAssetProfile AssetProfile
    {
        get
        {
            int rank = GetApertureRankForVisual();
            string iconPath =
                $"res://GuZhenRenPersonal/images/relics/KongQiao{rank}.png";

            return new RelicAssetProfile(
                IconPath: iconPath,
                BigIconPath: iconPath
            );
        }
    }

    /// <summary>
    /// 通过玩家始终携带的空窍起始遗物注册合练与升炼选项。
    /// RestSiteOption.Generate 只遍历运行中的 Hook 监听模型，
    /// 角色定义本身不在该列表中，因此不能把选项注册放在角色模型上。
    /// </summary>
    public override bool TryModifyRestSiteOptions(
        Player player,
        ICollection<RestSiteOption> options
    )
    {
        bool modified = base.TryModifyRestSiteOptions(player, options);

        if (!ReferenceEquals(player, Owner))
        {
            return modified;
        }

        if (!options.OfType<GuRankUpRestSiteOption>().Any())
        {
            options.Add(new GuRankUpRestSiteOption(player));
            modified = true;
        }

        if (!options.OfType<GuHeLianRestSiteOption>().Any())
        {
            options.Add(new GuHeLianRestSiteOption(player));
            modified = true;
        }

        return modified;
    }

    public override async Task BeforeCombatStart()
    {
        ApertureSystem.HandleCombatStarting(Owner);
        await TribulationSystem.TryPrepareCombatAsync(Owner);
        await GuangDaoPowerSystem.EnsureZheGuang(Owner);
    }

    /// <summary>
    /// 光辉与照破只能由光道卡牌改变。这里位于原生 PowerCmd 的
    /// giver 修正链中，因此也会拦住绕过 GuangDaoPowerSystem 的误调用；
    /// 同时保证光辉初次施加时不超过 9 点。
    /// </summary>
    public override decimal ModifyPowerAmountGivenAdditive(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(giver, Owner.Creature) ||
            power is not GuangHuiPower ||
            amount <= 0 ||
            !ReferenceEquals(target, Owner.Creature) ||
            !GuangDaoPowerSystem.IsGuangDaoCard(cardSource))
        {
            return 0;
        }

        int existing = Owner.Creature
            .GetPower<GuangHuiPower>()?.Amount ?? 0;
        decimal allowed = Math.Min(
            amount,
            Math.Max(0, GuangHuiPower.MaximumAmount - existing)
        );
        return allowed - amount;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(giver, Owner.Creature) ||
            power is not (GuangHuiPower or ZhaoPoPower))
        {
            return 1;
        }

        bool validTarget = power switch
        {
            GuangHuiPower => ReferenceEquals(
                target,
                Owner.Creature
            ),
            ZhaoPoPower => target?.IsEnemy == true,
            _ => false,
        };

        return validTarget &&
            GuangDaoPowerSystem.IsGuangDaoCard(cardSource)
                ? 1
                : 0;
    }

    /// <summary>
    /// 只在本场第一次初始抽牌前发放对应仙元牌与杀招推演牌。
    /// </summary>
    public override Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState
    )
    {
        if (!ReferenceEquals(player, Owner))
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(
            ApertureSystem.HandleBeforeHandDrawAsync(player),
            ApertureSystem.HandleShaZhaoDerivationGrantAsync(player)
        );
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await TribulationSystem.ResolveVictoryAsync(Owner);
        await ApertureSystem.HandleCombatVictoryAsync(Owner, room);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await TribulationSystem.ResolveCombatEndAsync(Owner);
        await base.AfterCombatEnd(room);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!ReferenceEquals(cardPlay.Card.Owner, Owner))
        {
            return;
        }

        await TribulationSystem.EventRouter.OnCardPlayedAsync(Owner, cardPlay);
        if (cardPlay.IsFirstInSeries)
        {
            await TribulationSystem.EventRouter.OnNativeEnergySpentAsync(
                Owner,
                cardPlay.Resources.EnergySpent);
            if (cardPlay.TryGetSecondaryResources(out var resources))
            {
                await TribulationSystem.EventRouter.OnYuanQiSpentAsync(
                    Owner,
                    resources.Spent(YuanQiSystem.ResourceId));
            }
        }
        if (cardPlay.Card is IGuWormCard && cardPlay.IsFirstInSeries)
        {
            await TribulationSystem.EventRouter.OnGuActivatedAsync(Owner, cardPlay.Card);
        }

        if (cardPlay.Card is IGuWormCard &&
            cardPlay.IsFirstInSeries)
        {
            // Power 类型蛊（能力蛊：苦力蛊/自力更生蛊/我力蛊/群力蛊）：
            // 使用原版能力牌逻辑，打出后从本场战斗移除（离场），
            // 不进入蛊冷却/恢复循环，本场战斗不再返回蛊手牌；
            // 蛊牌属于永久牌组，下场战斗重新入场后仍可再次打出。
            if (cardPlay.Card.Type == CardType.Power)
            {
                await CardPileCmd.RemoveFromCombat(
                    cardPlay.Card,
                    skipVisuals: false
                );
                await GuCardPileSystem.RefillGuHandAsync(Owner);
                return;
            }

            // CardModel.OnPlayWrapper 在 AfterCardPlayed 之后才会把
            // Play 堆中的卡牌送入结果堆。蛊牌的结果堆是 RitsuLib 自定义
            // 牌堆；多人动作在客户端执行时，原版的最后一步迁移可能
            // 发生在客户端仍保留普通手牌的状态窗口内，下一次操作便会
            // 再次请求同一张卡，最终造成 checksum 分叉。
            //
            // 在两端都由同一个出牌钩子、基于已经登记的催动次数，明确
            // 把当前卡牌送入结果蛊牌堆。此处使用原生 CardPileCmd.Add
            // （而不是只改 CardPile 的内部列表），保证牌堆变更钩子和
            // Multiplayer checksum 都按相同顺序执行。迁移后原版
            // OnPlayWrapper 看到卡牌已不在 Play 堆，会跳过重复迁移。
            PileType resultPile = GuCardUsageRules.CanUse(cardPlay.Card)
                ? GuCardPileSystem.PileType
                : GuCardPileSystem.RecoveryPileType;
            await GuCardPileSystem.MoveCardToPileAsync(
                cardPlay.Card,
                resultPile,
                // The result pile move occurs while the card is still shown
                // in its play holder. Keep the native flight enabled so that
                // holder is released instead of remaining on the combat UI.
                skipVisuals: false
            );

            await GuCardPileSystem
                .MoveDepletedGuCardsToRecoveryAsync(Owner);
            await GuRecoveryEffectSystem
                .HandleEnteredRecoveryAsync(cardPlay.Card);
            await GuCardPileSystem.RefillGuHandAsync(Owner);
        }

        if (cardPlay.IsLastInSeries)
        {
            await ImmortalEssenceSystem
                .ExhaustDepletedCardsAsync(Owner);
        }
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw) =>
        ReferenceEquals(card.Owner, Owner)
            ? TribulationSystem.EventRouter.OnCardDrawnAsync(Owner, card)
            : Task.CompletedTask;

    public override Task AfterCardDiscarded(
        PlayerChoiceContext choiceContext,
        CardModel card) =>
        ReferenceEquals(card.Owner, Owner)
            ? TribulationSystem.EventRouter.OnCardDiscardedAsync(Owner, card)
            : Task.CompletedTask;

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player) =>
        ReferenceEquals(player, Owner)
            ? TribulationSystem.EventRouter.OnPlayerTurnStartAsync(
                Owner,
                Owner.PlayerCombatState?.TurnNumber ?? 1)
            : Task.CompletedTask;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState) =>
        side == CombatSide.Enemy
            ? TribulationSystem.EventRouter.OnEnemyTurnStartAsync(
                Owner,
                combatState.RoundNumber)
            : Task.CompletedTask;

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants) =>
        side == CombatSide.Player
            ? TribulationSystem.EventRouter.OnPlayerTurnEndAsync(
                Owner,
                Owner.PlayerCombatState?.TurnNumber ?? 1)
            : TribulationSystem.EventRouter.OnEnemyTurnEndAsync(
                Owner,
                Owner.Creature.CombatState?.RoundNumber ?? 1);

    public override Task BeforeBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource) =>
        TribulationSystem.EventRouter.OnBeforeBlockGainedAsync(
            Owner,
            creature,
            amount,
            props,
            cardSource);

    public override Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource) =>
        TribulationSystem.EventRouter.OnBlockGainedAsync(
            Owner,
            creature,
            amount,
            props,
            cardSource);

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        TribulationSystem.EventRouter.OnDamageResolvedAsync(
            Owner,
            new GuZhenRen.Tribulations.Contracts.TribulationDamageEvent(
                target,
                dealer,
                result,
                props,
                cardSource,
                CardPlay: null));

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        ReferenceEquals(creature, Owner.Creature) && delta < 0m
            ? TribulationSystem.EventRouter.OnPlayerHpLostAsync(Owner, -delta)
            : Task.CompletedTask;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TribulationSystem.EventRouter.ModifyDamageAdditive(
            Owner, target, amount, props, dealer, cardSource, cardPlay);

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TribulationSystem.EventRouter.ModifyDamageMultiplicative(
            Owner, target, amount, props, dealer, cardSource, cardPlay);

    public override decimal ModifyDamageCap(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TribulationSystem.EventRouter.ModifyDamageCap(
            Owner, target, props, dealer, cardSource, cardPlay);

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (block <= 0m)
            return 1m;
        decimal modified = TribulationSystem.EventRouter.ModifyPlayerBlockGain(
            Owner, target, block, props, cardSource, cardPlay);
        return modified / block;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        bool baseModified = base.TryModifyPowerAmountReceived(
            canonicalPower,
            target,
            amount,
            applier,
            out modifiedAmount);
        decimal input = modifiedAmount;
        bool tribulationModified =
            TribulationSystem.EventRouter.TryModifyPowerAmountReceived(
                Owner,
                canonicalPower,
                target,
                input,
                applier,
                out decimal tribulationAmount);
        if (tribulationModified)
            modifiedAmount = tribulationAmount;
        return baseModified || tribulationModified;
    }

    /// <summary>
    /// 统一拦截蛊虫剩余催动次数，并在六转以后检查手牌仙元。
    /// 该遗物位于原版战斗 Hook 列表中，因此也能覆盖从自定义蛊虫牌堆自动打出的牌。
    /// </summary>
    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType
    )
    {
        bool allowedByBase = base.ShouldPlay(card, autoPlayType);

        if (!allowedByBase ||
            !ReferenceEquals(card.Owner, Owner) ||
            card is not IGuWormCard)
        {
            return allowedByBase;
        }

        return GuCardUsageRules.CanActivate(card);
    }

    /// <summary>
    /// 每次仙蛊催动在效果结算前扣除仙元；Replay 的后续段不重复付费。
    /// </summary>
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        await base.BeforeCardPlayed(cardPlay);

        if (!ReferenceEquals(cardPlay.Card.Owner, Owner) ||
            cardPlay.Card is not IGuWormCard guCard ||
            !cardPlay.IsFirstInSeries)
        {
            return;
        }

        // 客户端执行同步的蛊牌出牌动作时没有本地 pending/移牌记录，
        // 蛊牌可能仍位于自定义蛊牌堆。这里在各端动作执行时幂等地把它
        // 补移到原版 Hand，让原生出牌管线在两端看到一致的位置。
        // （发起端 RitsuLib 已移入 Hand，此处跳过；脚本 AutoPlay 不打
        // 普通手牌，保持原有路径。）
        if (!cardPlay.IsAutoPlay &&
            cardPlay.Card.Pile?.Type == GuCardPileSystem.PileType)
        {
            GuCardPileSystem.MoveCardToPile(
                cardPlay.Card,
                PileType.Hand.GetPile(Owner)
            );
            Entry.Logger.Info(
                $"[蛊牌催动] {cardPlay.Card.Id} 出牌前已从蛊牌堆补移到 Hand。"
            );
        }

        int yuanQiCost = Math.Max(0, guCard.YuanQiCost);
        bool freeByBrilliantScarf =
            GuCardUsageRules.IsBrilliantScarfFreePlay(cardPlay.Card);
        bool paidByNativePipeline =
            yuanQiCost == 0 ||
            freeByBrilliantScarf ||
            (
                cardPlay.TryGetSecondaryResources(out var ledger) &&
                ledger.Spent(YuanQiSystem.ResourceId) >= yuanQiCost
            );

        // ExtraHand 手动出牌会由 RitsuLib 的次级资源管线自动支付元气。
        // 艳丽围巾的第 5 张免费由次级资源费用钩子把元气降为 0；
        // 这里把它视为已支付，避免旧入口的兜底逻辑再次扣除元气。
        // 只有没有支付记录的旧 AutoPlay/脚本入口才由模组补付。
        if (!paidByNativePipeline &&
            !await GuCardUsageRules
                .EnsureActivationPayment(cardPlay.Card))
        {
            GuCardUsageRules.ActivationEligibility eligibility =
                GuCardUsageRules.GetActivationEligibility(cardPlay.Card);
            string message =
                $"无法催动 {cardPlay.Card.Id}：{eligibility.Describe()}。";
            GuActivationModeSystem.Cancel(message);
            throw new InvalidOperationException(message);
        }

        // Legacy AutoPlay/script entry points do not populate RitsuLib's
        // secondary-resource ledger. Report their successful fallback payment
        // here so the unified tribulation router observes every real Yuan Qi
        // expenditure exactly once.
        if (!paidByNativePipeline && yuanQiCost > 0 && !freeByBrilliantScarf)
        {
            await TribulationSystem.EventRouter.OnYuanQiSpentAsync(
                Owner,
                yuanQiCost);
        }

        if (guCard.GuRank >= ApertureProgression.ImmortalRank)
        {
            bool paid = await ImmortalEssenceSystem
                .SpendForActivation(cardPlay);

            if (!paid)
            {
                string message =
                    $"仙元不足，无法催动 {cardPlay.Card.Id}。";
                GuActivationModeSystem.Cancel(message);
                throw new InvalidOperationException(message);
            }
        }

        GuCardUsageRules.RegisterActivation(cardPlay.Card);
        GuActivationModeSystem.CompleteActivation(cardPlay.Card);
    }

    /// <summary>
    /// 兼容先古之民遗物“艳丽围巾”：当其第 5 张牌免费效果已准备好时，
    /// 同步把蛊牌的元气费用降为 0。只修改元气，不改变其他次级资源，
    /// 也不接管艳丽围巾自身的出牌计数。
    /// </summary>
    public decimal ModifySecondaryResourceCostLate(
        SecondaryResourceCostContext context,
        decimal cost
    )
    {
        if (!ReferenceEquals(context.Player, Owner) ||
            context.Card is not IGuWormCard ||
            !string.Equals(
                context.Definition.Id,
                YuanQiSystem.ResourceId,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return cost;
        }

        return GuCardUsageRules.IsBrilliantScarfFreePlay(context.Card)
            ? 0m
            : cost;
    }

    /// <summary>
    /// 一至九转的元气上限依次为 3、4、4、5、5、7、7、8、9。
    /// </summary>
    public decimal ModifyMaxSecondaryResource(
        SecondaryResourceMaxContext context,
        decimal amount
    )
    {
        if (!ReferenceEquals(context.Player, Owner) ||
            !string.Equals(
                context.Definition.Id,
                YuanQiSystem.ResourceId,
                StringComparison.Ordinal
            ))
        {
            return amount;
        }

        int rank = ApertureSystem.IsInitialized
            ? ApertureSystem.GetState(Owner).Rank
            : ApertureProgression.MinimumRank;

        return ApertureProgression.GetYuanQiCapacity(rank);
    }

    /// <summary>
    /// 原生能量重置完成后，第一回合把元气补满至当前空窍转数的
    /// 上限（一转 3 ~ 九转 9）；从第二回合起根据当前元气上限回复：
    /// 上限 3 回复 1、4~5 回复 2、6~7 回复 3、8~9 回复 4，
    /// 且不超过当前元气上限。
    /// </summary>
    public override async Task AfterEnergyReset(Player player)
    {
        await base.AfterEnergyReset(player);

        if (!ReferenceEquals(player, Owner) ||
            player.PlayerCombatState == null)
        {
            return;
        }

        await GuCardPileSystem.RestoreRecoveredGuCardsAsync(
            player,
            player.PlayerCombatState.TurnNumber
        );

        int rank = ApertureSystem.IsInitialized
            ? ApertureSystem.GetState(Owner).Rank
            : ApertureProgression.MinimumRank;

        int currentAmount = SecondaryResourceCmd.Get(
            player,
            YuanQiSystem.ResourceId
        );
        int maximumAmount = SecondaryResourceCmd.GetMax(
            player,
            YuanQiSystem.ResourceId
        ) ?? YuanQiSystem.Definition.HardMaxAmount;
        int targetAmount = player.PlayerCombatState.TurnNumber <= 1
            ? ApertureProgression.GetYuanQiStartAmount(rank)
            : currentAmount +
              ApertureProgression.GetYuanQiRecovery(rank);
        int requestedGain = Math.Max(0, targetAmount - currentAmount);
        int modifiedGain = TribulationSystem.EventRouter.ModifyYuanQiGain(
            player,
            requestedGain);
        targetAmount = currentAmount + modifiedGain;

        await SecondaryResourceCmd.Set(
            player,
            YuanQiSystem.ResourceId,
            Math.Clamp(
                targetAmount,
                YuanQiSystem.Definition.MinAmount,
                maximumAmount
            ),
            source: this
        );
    }


    /// <summary>
    /// 所有奖励完成 Populate 后移除空卡牌奖励，避免空列表进入奖励界面。
    /// </summary>
    public override bool TryModifyRewardsLate(
        Player player,
        List<Reward> rewards,
        AbstractRoom? room
    )
    {
        bool modified = base.TryModifyRewardsLate(
            player,
            rewards,
            room
        );

        if (!ReferenceEquals(player, Owner))
        {
            return modified;
        }

        int removed = rewards.RemoveAll(reward =>
            reward is CardReward && !reward.IsPopulated
        );

        if (removed > 0)
        {
            Entry.Logger.Info(
                $"移除了 {removed} 个没有候选牌的空卡牌奖励。"
            );
        }

        return modified || removed > 0;
    }

    public override Task AfterObtained()
    {
        RefreshVisualStateIfAvailable();
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        return ApertureSystem.IsInitialized
            ? ApertureSystem.HandleRoomEnteredAsync(Owner)
            : Task.CompletedTask;
    }

    internal void RefreshApertureVisualState(ApertureRunData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        AssertMutable();

        Status = RelicStatus.Normal;

        if (_lastVisualRank != data.Rank)
        {
            _lastVisualRank = data.Rank;
            RelicIconChanged();
        }

        InvokeDisplayAmountChanged();
    }

    private void RefreshVisualStateIfAvailable()
    {
        if (ApertureSystem.IsInitialized)
        {
            ApertureSystem.RefreshRelicVisualState(Owner);
        }
    }

    private int GetApertureRankForVisual()
    {
        if (!IsMutable || !ApertureSystem.IsInitialized)
        {
            return ApertureProgression.MinimumRank;
        }

        try
        {
            return Math.Clamp(
                ApertureSystem.GetState(Owner).Rank,
                ApertureProgression.MinimumRank,
                ApertureProgression.MaximumImplementedRank
            );
        }
        catch
        {
            return ApertureProgression.MinimumRank;
        }
    }
}
