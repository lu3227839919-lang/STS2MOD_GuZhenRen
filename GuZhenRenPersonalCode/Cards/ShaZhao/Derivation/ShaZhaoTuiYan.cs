using GuZhenRen.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招推演（0.8.0 杀招系统）：空窍三转解锁后每场战斗开始时
/// 加入手牌的特殊系统牌。
///
/// 打出后选择蛊手牌中的可用蛊虫作为材料；若材料组成合法
/// 配方，支付推演元气与本牌的原生费用，将材料封装并生成对应杀招加入
/// 手牌，本牌随后消耗。取消、配方无效或资源不足时本牌回到手牌，
/// 并原额返还原生管线已经支付的能量/星星。
/// </summary>
[RegisterCard(typeof(GuZhenRen.Characters.GuZhenRenCardPool))]
public sealed class ShaZhaoTuiYan : AbstractGuZhenRenGeneratedCard
{
    public ShaZhaoTuiYan()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.None,
            showInCardLibrary: true
        )
    {
    }

    /// <summary>
    /// 保留：取消推演后继续留在手牌。
    /// 成功推演后由 OnPlay 主动消耗，不自动添加“消耗”关键词，
    /// 避免取消/失败时被误消耗。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    /// <summary>
    /// 卡图统一使用 images/cards/ShaZhaoTuiYan.png。
    /// </summary>
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    protected override void OnUpgrade()
    {
        // 系统牌不可升级。
    }

    protected override async Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer
            .PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        bool succeeded =
            await ShaZhaoTuiYanSystem.DeriveFromCardAsync(
                choiceContext,
                Owner,
                cardPlay
            );

        if (succeeded)
        {
            await CardExhaustCompat.ExhaustAsync(
                choiceContext,
                this
            );
        }
        else
        {
            PlayerCombatState? combatState = Owner.PlayerCombatState;
            if (combatState != null)
            {
                combatState.GainEnergy(cardPlay.Resources.EnergySpent);
                combatState.GainStars(cardPlay.Resources.StarsSpent);
            }

            await GuCardPileSystem.MoveCardToPileAsync(
                this,
                PileType.Hand,
                skipVisuals: false
            );

            Entry.Logger.Info(
                $"[杀招推演] 已回滚原生支付：" +
                $"能量 {cardPlay.Resources.EnergySpent}，" +
                $"星星 {cardPlay.Resources.StarsSpent}。"
            );
        }
    }
}
