using GuZhenRen.Aperture;
using GuZhenRen.Characters;
using GuZhenRen.RestSite;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Relics;

/// <summary>
/// 方源的空窍起始遗物。
/// 使用单个遗物模型承载 1～9 转状态；当前版本不包含灾劫和十转。
/// </summary>
[RegisterRelic(typeof(GuZhenRenRelicPool))]
[RegisterCharacterStarterRelic(typeof(GuZhenRenCharacter))]
public sealed class KongQiaoRelic : ModRelicTemplate
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

    public override Task BeforeCombatStart()
    {
        ApertureSystem.HandleCombatStarting(Owner);
        return Task.CompletedTask;
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

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ApertureSystem.HandleCardPlayed(Owner, cardPlay);
        return Task.CompletedTask;
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
