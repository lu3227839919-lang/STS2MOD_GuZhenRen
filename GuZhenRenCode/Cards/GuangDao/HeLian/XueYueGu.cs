using GuZhenRen.Cards.XueDao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 血月蛊：普通血道寄生蛊。血元不足2点时自动触发残月档；血元
/// 至少2点时自动消耗2点并触发完整血月档，不允许生命代付。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueYueGu : AbstractGuWormCard
{
    public override int MaxGuRank => 7;

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank >= 7 ? 4 : GuRank >= 4 ? 3 : 2;

    protected override bool IsPlayable =>
        base.IsPlayable &&
        (IsCanonical || HasEligibleHost());

    public XueYueGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Uncommon,
            target: TargetType.Self
        )
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        CardModel? host = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1)
            {
                Cancelable = false
            },
            card => XueDaoParasiteSystem.CanAttach(
                card,
                allowBloodQiReplacement: true
            ),
            this
        )).FirstOrDefault();

        if (host == null)
        {
            return;
        }

        bool fullMoon = XueDaoPowerSystem.GetXueYuan(Owner.Creature) >= 2;
        if (fullMoon &&
            !await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                2
            ))
        {
            fullMoon = false;
        }

        await XueDaoParasiteSystem.AttachAsync(
            choiceContext,
            host,
            fullMoon
                ? XueDaoParasiteSystem.ParasiteKind.BloodMoon
                : XueDaoParasiteSystem.ParasiteKind.CrescentMoon,
            GuRank,
            IsUpgraded,
            this
        );
    }

    private bool HasEligibleHost() =>
        Owner.PlayerCombatState?.Hand.Cards.Any(card =>
            XueDaoParasiteSystem.CanAttach(
                card,
                allowBloodQiReplacement: true
            )
        ) == true;
}
