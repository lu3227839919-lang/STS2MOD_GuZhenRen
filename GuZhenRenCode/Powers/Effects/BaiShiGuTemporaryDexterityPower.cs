using GuZhenRen.Cards.Basic;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 白豕蛊提供的临时敏捷。
///
/// 施加时同步增加同等层数的敏捷；所属玩家一侧回合结束时，
/// 移除此能力并扣回相同层数的敏捷。实现与游戏原生
/// TemporaryDexterityPower 的生命周期一致，但来源显示为白豕蛊。
///
/// 所有数值变化均通过 PowerCmd 执行，因此会进入游戏动作序列并参与多人同步。
/// </summary>
[RegisterPower]
public sealed class BaiShiGuTemporaryDexterityPower
    : ModPowerTemplate,
      ITemporaryPower
{
    private bool _shouldIgnoreNextInstance;

    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public AbstractModel OriginModel =>
        ModelDb.Card<BaiShiGu>();

    public PowerModel InternallyAppliedPower =>
        ModelDb.Power<DexterityPower>();

    public override LocString Title =>
        ModelDb.Card<BaiShiGu>()
            .TitleLocString;

    public override LocString Description =>
        new(
            "powers",
            "TEMPORARY_DEXTERITY_POWER.description"
        );

    protected override string SmartDescriptionLocKey =>
        "TEMPORARY_DEXTERITY_POWER.smartDescription";

    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/TuDaoDaoHenPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/TuDaoDaoHenPower_p-256x256.png"
        );

    protected override IEnumerable<IHoverTip>
        AdditionalHoverTips =>
        [
            HoverTipFactory.FromCard(
                ModelDb.Card<BaiShiGu>()
            ),
            HoverTipFactory.FromPower<
                DexterityPower
            >()
        ];

    /// <summary>
    /// 复制临时能力时，跳过下一次内部敏捷施加，避免重复计算。
    /// </summary>
    public void IgnoreNextInstance()
    {
        _shouldIgnoreNextInstance = true;
    }

    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource
    )
    {
        if (_shouldIgnoreNextInstance)
        {
            _shouldIgnoreNextInstance = false;
            return;
        }

        await PowerCmd.Apply<DexterityPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            applier,
            cardSource,
            silent: true
        );
    }

    public override async Task
        AfterPowerAmountChanged(
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
            ) ||
            amount == Amount)
        {
            return;
        }

        if (_shouldIgnoreNextInstance)
        {
            _shouldIgnoreNextInstance = false;
            return;
        }

        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner,
            amount,
            applier,
            cardSource,
            silent: true
        );
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        Flash();

        int amountToRemove = Amount;

        await PowerCmd.Remove(
            this
        );

        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner,
            -amountToRemove,
            Owner,
            cardSource: null
        );
    }
}
