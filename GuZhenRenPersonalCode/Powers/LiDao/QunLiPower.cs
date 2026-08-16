using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 群力蛊的持续战斗状态。
/// 每回合前 N 次（5/6转 1 次，7转 2 次）兽力虚影自然显化后，
/// 按概率使该虚影额外显化一次；额外显化不会再次触发群力（递归阻断）。
/// </summary>
[RegisterPower]
public sealed class QunLiPower : ModPowerTemplate
{
    private int _lastTurn;
    private int _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => GroupChancePercent;

    public int Rank => DynamicVars["Rank"].IntValue;

    public int GroupChancePercent => QunLiGu.GroupChanceAtRank(Rank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 5m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 5, 7);
        InvokeDisplayAmountChanged();
    }

    internal async Task HandleNaturalManifestAsync(
        PlayerChoiceContext choiceContext,
        AbstractLiDaoXuYing phantom,
        CardPlay cardPlay
    )
    {
        int turn = CurrentTurn;
        if (turn != _lastTurn)
        {
            _lastTurn = turn;
            _usedThisTurn = 0;
        }

        if (_usedThisTurn >= QunLiGu.GroupTriggerLimitAtRank(Rank))
        {
            return;
        }
        _usedThisTurn++;
        InvokeDisplayAmountChanged();

        int chance = GroupChancePercent;
        if (chance <= 0 ||
            RitsuLibFramework.GetModPlayerRng(
                Owner.Player!,
                Entry.ModId,
                "li_dao/qun_li_repeat"
            ).NextInt(100) >= chance)
        {
            return;
        }

        bool executed = await phantom.TriggerFromControllerAsync(
            choiceContext,
            cardPlay,
            forced: true,
            effectMultiplier: 1m
        );
        if (executed)
        {
            await LiDaoManifestHub.NotifyGroupExtraManifestAsync(
                choiceContext,
                Owner.Player!,
                phantom
            );
        }
    }

    private int CurrentTurn =>
        Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
}
