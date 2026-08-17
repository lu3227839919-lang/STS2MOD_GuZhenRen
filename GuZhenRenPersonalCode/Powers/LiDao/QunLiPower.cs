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
/// 每次力道虚影（包括我力虚影）自然显化后都开启一条独立连锁：每次判定成功便使
/// 同一虚影额外显化，并继续判定，直至失败或达到 1/2/3 次上限。
/// 连锁在本 Power 内循环执行，不会递归开启新的群力连锁。
/// </summary>
[RegisterPower]
public sealed class QunLiPower : ModPowerTemplate
{
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
        int chance = GroupChancePercent;
        if (chance <= 0)
        {
            return;
        }

        var rng = RitsuLibFramework.GetModPlayerRng(
            Owner.Player!,
            Entry.ModId,
            "li_dao/qun_li_repeat"
        );

        int repeatLimit = QunLiGu.GroupRepeatLimitAtRank(Rank);
        for (int repeat = 0; repeat < repeatLimit; repeat++)
        {
            if (rng.NextInt(100) >= chance)
            {
                break;
            }

            bool executed = await phantom.TriggerFromControllerAsync(
                choiceContext,
                cardPlay,
                forced: true,
                effectMultiplier: 1m
            );
            if (!executed)
            {
                break;
            }

            if (QunLiGu.ExtraManifestCountsAsActualAtRank(Rank))
            {
                await LiDaoManifestHub.NotifyGroupExtraManifestAsync(
                    choiceContext,
                    Owner.Player!,
                    phantom
                );
            }
        }
    }
}
