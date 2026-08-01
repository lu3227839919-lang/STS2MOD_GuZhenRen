using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 金道道痕。
///
/// 尖塔1原版每层金道道痕提供 1 层伴生“金属化”。
///
/// 尖塔2没有沿用同名机制，本移植版改为原生
/// <see cref="PlatingPower"/>（覆甲），仍保持 1:1 的层数关系：
///
/// - 获得 1 层金道道痕，同时获得 1 层覆甲；
/// - 金道道痕叠加时，同步增加覆甲；
/// - 金道道痕减少或移除时，同步减少覆甲；
/// - 外部效果不能把覆甲降低到当前金道道痕保障值以下。
/// </summary>
[RegisterPower]
public sealed class JinDaoDaoHenPower
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
    /// 声明金道的伴生能力为覆甲。
    /// </summary>
    protected override Type DerivedPowerType =>
        typeof(PlatingPower);

    /// <summary>
    /// 每层金道道痕保障 1 层覆甲。
    /// </summary>
    protected override int RequiredDerivedPowerAmount =>
        Amount;

    /// <summary>
    /// 首次获得金道道痕时，同步发放覆甲。
    /// </summary>
    public override async Task AfterApplied(
        Creature? applier,
        CardModel? cardSource
    )
    {
        await ChangeDerivedPowerAsync<PlatingPower>(
            new ThrowingPlayerChoiceContext(),
            Amount
        );
    }

    /// <summary>
    /// 金道道痕层数变化时，同步修改覆甲层数。
    /// </summary>
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(
                power,
                this
            ))
        {
            return;
        }

        await ChangeDerivedPowerAsync<PlatingPower>(
            choiceContext,
            (int)amount
        );
    }

    /// <summary>
    /// 金道道痕被移除时，扣除其提供的全部伴生覆甲。
    /// </summary>
    public override async Task AfterRemoved(
        Creature oldOwner
    )
    {
        if (Amount <= 0)
        {
            return;
        }

        await ChangeDerivedPowerAsync<PlatingPower>(
            new ThrowingPlayerChoiceContext(),
            -Amount,
            oldOwner
        );
    }
}
