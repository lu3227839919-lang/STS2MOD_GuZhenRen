using GuZhenRen.Characters;
using MegaCrit.Sts2.Core.Commands;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 四张仙元牌的公共父类。
/// 仙元牌均为 0 费、保留、消耗，并且只由仙窍在战斗中生成。
/// </summary>
[RegisterCard(
    typeof(GuZhenRenXianYuanCardPool),
    Inherit = true
)]
public abstract class AbstractXianYuanCard : ModCardTemplate
{
    /// <summary>
    /// 仙元牌固定属于仙元隐藏卡池。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenXianYuanCardPool>();

    protected abstract int EnergyGain { get; }

    /// <summary>
    /// 每张具体仙元牌必须声明非空的静态图片路径。
    /// 这样 RitsuLib 分析器和运行时才能在资源缺失时给出诊断。
    /// </summary>
    public abstract override CardAssetProfile AssetProfile { get; }

    protected AbstractXianYuanCard()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Rare,
            target: TargetType.Self,
            showInCardLibrary: false
        )
    {
    }

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(CardKeyword.Retain)
            .Append(CardKeyword.Exhaust);

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Energy", EnergyGain);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await PlayerCmd.GainEnergy(
            EnergyGain,
            Owner
        );
    }

    protected override void OnUpgrade()
    {
        // Java 原版升级不改变实际效果。
    }
}
