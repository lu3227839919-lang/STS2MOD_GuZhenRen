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
/// 骨道道痕。
///
/// 每层道痕提供 2 点伴生荆棘。
/// </summary>
[RegisterPower]
public sealed class GuDaoDaoHenPower
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
        typeof(ThornsPower);

    protected override int RequiredDerivedPowerAmount =>
        Amount * 2;

    public override async Task AfterApplied(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature?
            applier,
        CardModel? cardSource
    )
    {
        await ChangeDerivedPowerAsync<ThornsPower>(
            new ThrowingPlayerChoiceContext(),
            Amount * 2
        );
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature?
            applier,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(power, this))
        {
            return;
        }

        await ChangeDerivedPowerAsync<ThornsPower>(
            choiceContext,
            (int)amount * 2
        );
    }

    public override async Task AfterRemoved(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature
            oldOwner
    )
    {
        if (Amount > 0)
        {
            await ChangeDerivedPowerAsync<ThornsPower>(
                new ThrowingPlayerChoiceContext(),
                -Amount * 2,
            oldOwner
            );
        }
    }
}
