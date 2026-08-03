using GuZhenRen.Aperture;
using GuZhenRen.Cards;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;
using GuZhenRen.RestSite;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;

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
                $"res://GuZhenRen/images/relics/KongQiao{rank}.png";

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
    /// 只在本场第一次初始抽牌前发放对应仙元牌。
    /// </summary>
    public override Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState
    )
    {
        return ReferenceEquals(player, Owner)
            ? ApertureSystem.HandleBeforeHandDrawAsync(player)
            : Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        return ApertureSystem.HandleCombatVictoryAsync(Owner, room);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ApertureSystem.HandleCardPlayed(Owner, cardPlay);

        if (ReferenceEquals(cardPlay.Card.Owner, Owner) &&
            cardPlay.Card is IGuWormCard)
        {
            await GuCardPileSystem
                .MoveDepletedGuCardsToRecoveryAsync(Owner);
        }
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

        return GuCardUsageRules.CanUse(card) &&
               ImmortalEssenceSystem.CanPayForActivation(card);
    }

    /// <summary>
    /// 每次仙蛊催动在效果结算前扣除仙元；Replay 的后续段不重复付费。
    /// </summary>
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        await base.BeforeCardPlayed(cardPlay);

        if (!ReferenceEquals(cardPlay.Card.Owner, Owner) ||
            cardPlay.Card is not IGuWormCard guCard ||
            cardPlay.PlayIndex != 0)
        {
            return;
        }

        if (guCard.GuRank >= ApertureProgression.ImmortalRank)
        {
            bool paid = await ImmortalEssenceSystem
                .SpendForActivation(cardPlay);

            if (!paid)
            {
                throw new InvalidOperationException(
                    $"仙元不足，无法催动 {cardPlay.Card.Id}。"
                );
            }
        }

        GuCardUsageRules.RegisterActivation(cardPlay.Card);
    }

    /// <summary>
    /// 一至六转的元气上限依次为 5、7、10、14、19、25；
    /// 六转以后保持 25。
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
    /// 原生能量重置完成后，第一回合把元气设为 5 点；从第二回合起
    /// 自动回复 2 点，且不超过转数上限。
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

        int currentAmount = SecondaryResourceCmd.Get(
            player,
            YuanQiSystem.ResourceId
        );
        int maximumAmount = SecondaryResourceCmd.GetMax(
            player,
            YuanQiSystem.ResourceId
        ) ?? YuanQiSystem.Definition.HardMaxAmount;
        int targetAmount = player.PlayerCombatState.TurnNumber <= 1
            ? 5
            : currentAmount + 2;

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

    internal void RefreshApertureVisualState(
        ApertureRunData data,
        bool inCombat,
        bool effectUsedThisCombat
    )
    {
        ArgumentNullException.ThrowIfNull(data);
        AssertMutable();

        Status = inCombat &&
                 data.Rank is > 1 and <= 5 &&
                 !effectUsedThisCombat
            ? RelicStatus.Active
            : RelicStatus.Normal;

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
