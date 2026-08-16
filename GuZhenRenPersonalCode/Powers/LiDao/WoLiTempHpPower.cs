using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 万我生成的独立临时生命来源。
/// 每个我力虚影提供 5 点临时生命；每累计损失 5 点该来源临时生命，
/// 消耗 1 个我力虚影。临时生命优先于真实生命吸收伤害。
/// </summary>
[RegisterPower]
public sealed class WoLiTempHpPower : ModPowerTemplate
{
    private const string TempHpVar = "TempHp";
    private const string LossRemainderVar = "LossRemainder";

    private int _pendingAbsorption;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Math.Max(0, Amount);

    public int TempHp => Math.Max(0, DynamicVars[TempHpVar].IntValue);

    public int LossRemainder =>
        Math.Max(0, DynamicVars[LossRemainderVar].IntValue);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(TempHpVar, 0m),
        new DynamicVar(LossRemainderVar, 0m),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    internal void AddShadows(int count)
    {
        if (count <= 0)
        {
            return;
        }

        DynamicVars[TempHpVar].BaseValue = TempHp + count * 5;
        InvokeDisplayAmountChanged();
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        if (!ReferenceEquals(target, Owner) ||
            TempHp <= 0 ||
            amount <= 0)
        {
            _pendingAbsorption = 0;
            return 0m;
        }

        _pendingAbsorption = Math.Min(
            TempHp,
            Math.Max(0, (int)Math.Ceiling(amount))
        );
        return -_pendingAbsorption;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(target, Owner) || _pendingAbsorption <= 0)
        {
            _pendingAbsorption = 0;
            return;
        }

        int absorbed = _pendingAbsorption;
        _pendingAbsorption = 0;
        if (absorbed <= 0)
        {
            return;
        }

        int hp = Math.Max(0, TempHp - absorbed);
        int remainder = LossRemainder + absorbed;
        int consumed = 0;
        while (remainder >= 5 && Amount - consumed > 0)
        {
            remainder -= 5;
            consumed++;
        }

        DynamicVars[TempHpVar].BaseValue = hp;
        DynamicVars[LossRemainderVar].BaseValue = remainder;
        if (consumed > 0)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                this,
                -consumed,
                Owner,
                cardSource
            );
            await WoLiPhantomSystem.ConsumeShadowsAsync(
                choiceContext,
                Owner.Player!,
                consumed
            );
        }

        InvokeDisplayAmountChanged();
    }
}
