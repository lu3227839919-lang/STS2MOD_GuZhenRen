using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 力道道痕。
///
/// 每层道痕提供 1 点伴生力量。
/// </summary>
[RegisterPower]
public sealed class LiDaoDaoHenPower
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
        typeof(StrengthPower);

    protected override int RequiredDerivedPowerAmount =>
        Amount;

    public override async Task AfterApplied(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature?
            applier,
        MegaCrit.Sts2.Core.Models.CardModel?
            cardSource
    )
    {
        await ChangeDerivedPowerAsync<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Amount
        );
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature?
            applier,
        MegaCrit.Sts2.Core.Models.CardModel?
            cardSource
    )
    {
        if (!ReferenceEquals(power, this))
        {
            return;
        }

        await ChangeDerivedPowerAsync<StrengthPower>(
            choiceContext,
            (int)amount
        );
    }

    public override async Task AfterRemoved(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature
            oldOwner
    )
    {
        if (Amount > 0)
        {
            await ChangeDerivedPowerAsync<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                -Amount,
            oldOwner
            );
        }
    }
}
