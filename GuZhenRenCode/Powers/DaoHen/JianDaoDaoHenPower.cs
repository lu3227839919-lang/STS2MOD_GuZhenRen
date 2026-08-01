using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 剑道道痕。
///
/// 每层剑道道痕提供 1 层伴生剑锋。
/// </summary>
[RegisterPower]
public sealed class JianDaoDaoHenPower
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

protected override Type DerivedPowerType =>
        typeof(JianFengPower);

    protected override int RequiredDerivedPowerAmount =>
        Amount;

    public override async Task AfterApplied(
        Creature? applier,
        CardModel? cardSource
    )
    {
        await ChangeDerivedPowerAsync<JianFengPower>(
            new ThrowingPlayerChoiceContext(),
            Amount
        );
    }

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

        await ChangeDerivedPowerAsync<JianFengPower>(
            choiceContext,
            (int)amount
        );
    }

    public override async Task AfterRemoved(
        Creature oldOwner
    )
    {
        if (Amount <= 0)
        {
            return;
        }

        await ChangeDerivedPowerAsync<JianFengPower>(
            new ThrowingPlayerChoiceContext(),
            -Amount,
            oldOwner
        );
    }
}
