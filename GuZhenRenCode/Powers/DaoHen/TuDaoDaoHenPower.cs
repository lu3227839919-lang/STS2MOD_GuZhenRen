using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 土道道痕。
///
/// 自身每次获得正数格挡时，额外获得等同于层数的格挡。
/// </summary>
[RegisterPower]
public sealed class TuDaoDaoHenPower
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

private bool _isTriggering;

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource
    )
    {
        if (_isTriggering ||
            Amount <= 0 ||
            amount <= 0m ||
            !ReferenceEquals(
                creature,
                Owner
            ))
        {
            return;
        }

        Flash();
        _isTriggering = true;

        try
        {
            await CreatureCmd.GainBlock(
                Owner,
                Amount,
                ValueProp.Unpowered,
                cardPlay: null,
                fast: true
            );
        }
        finally
        {
            _isTriggering = false;
        }
    }
}
