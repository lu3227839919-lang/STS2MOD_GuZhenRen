using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Powers;

/// <summary>
/// 智道道痕。
///
/// 回合结束时：
///
/// 1. 若能量会被正常重置，记录至多等同于层数的剩余能量；
/// 2. 在玩家需要弃牌时，选择至多等同于层数的手牌保留。
///
/// 下个回合开始时，在道痕转回变化道之后返还记录的能量。
///
/// 尖塔2当前没有 EquilibriumPower，因此不进行该项检查；
/// 符文金字塔等“不弃牌”效果由 Hook.ShouldFlush 自动处理；
/// 冰淇淋等“不重置能量”效果由 Hook.ShouldPlayerResetEnergy
/// 自动处理。
/// </summary>
[RegisterPower]
public sealed class ZhiDaoDaoHenPower
    : AbstractDaoHenPower
{

    /// <summary>
    /// 当前能力使用的图标资源。
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

/// <summary>
    /// 本回合结束时暂存、下回合返还的能量。
    /// </summary>
    private static readonly SavedAttachedState<PowerModel, int>
        EnergyRetainedState =
            new(
                "gu_zhen_ren.power.zhi_dao.energy_retained",
                static () => 0
            );

    private int EnergyRetained
    {
        get => EnergyRetainedState[this];
        set => EnergyRetainedState[this] = value;
    }

    /// <summary>
    /// 玩家回合结束前记录即将因重置而失去的能量。
    /// </summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (side != CombatSide.Player ||
            Amount <= 0 ||
            !participants.Contains(Owner))
        {
            EnergyRetained = 0;
            return Task.CompletedTask;
        }

        Player? player =
            Owner.Player;

        PlayerCombatState? combatState =
            player?.PlayerCombatState;

        if (player == null ||
            combatState == null)
        {
            EnergyRetained = 0;
            return Task.CompletedTask;
        }

        // 冰淇淋等效果会让该检查返回 false。
        // 此时能量本身不会消失，因此无需额外记录和返还。
        if (!Hook.ShouldPlayerResetEnergy(
                CombatState,
                player
            ))
        {
            EnergyRetained = 0;
            return Task.CompletedTask;
        }

        EnergyRetained =
            Math.Min(
                combatState.Energy,
                Amount
            );

        return Task.CompletedTask;
    }

    /// <summary>
    /// 在正常弃牌流程的后段，让玩家选择至多 Amount 张牌保留。
    ///
    /// 与原版 WellLaidPlansPower 使用相同的选择时机和命令。
    /// </summary>
    public override async Task BeforeFlushLate(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        if (!ReferenceEquals(
                player,
                Owner.Player
            ))
        {
            return;
        }

        var combatState =
            player.Creature.CombatState;

        if (combatState == null ||
            !Hook.ShouldFlush(
                combatState,
                player
            ))
        {
            return;
        }

        List<CardModel> selectedCards =
            (
                await CardSelectCmd.FromHand(
                    prefs:
                        new CardSelectorPrefs(
                            SelectionScreenPrompt,
                            0,
                            Amount
                        ),
                    context:
                        choiceContext,
                    player:
                        player,
                    filter:
                        RetainFilter,
                    source:
                        this
                )
            )
            .ToList();

        foreach (CardModel card in selectedCards)
        {
            card.GiveSingleTurnRetain();
        }
    }

    /// <summary>
    /// 玩家回合开始时先执行公共道痕转化，再返还能量。
    /// </summary>
    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        bool isOwner =
            ReferenceEquals(
                player.Creature,
                Owner
            );

        int retainedEnergy =
            isOwner
                ? EnergyRetained
                : 0;

        EnergyRetained = 0;

        if (retainedEnergy > 0)
        {
            // 在本能力被公共父类移除前先播放闪烁。
            Flash();
        }

        await base.AfterPlayerTurnStart(
            choiceContext,
            player
        );

        if (!isOwner ||
            retainedEnergy <= 0)
        {
            return;
        }

        await PlayerCmd.GainEnergy(
            retainedEnergy,
            player
        );
    }

    private static bool RetainFilter(
        CardModel card
    )
    {
        return !card.ShouldRetainThisTurn;
    }
}
