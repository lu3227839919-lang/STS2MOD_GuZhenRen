using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

[RegisterCard(typeof(GuZhenRen.Characters.GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(typeof(XueTaiGu))]
[ShaZhaoRecipe(typeof(XueYueGu))]
public sealed class XueYueJi : AbstractShaZhaoCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [
            CardKeyword.Exhaust,
            GuZhenRenKeywords.FuHua,
        ];

    protected override bool IsPlayable =>
        base.IsPlayable &&
        (IsCanonical ||
         Owner.PlayerCombatState?.Hand.Cards.Any(
             XueDaoParasiteSystem.HasParasite
         ) == true);

    public XueYueJi()
        : base(1, CardType.Skill, TargetType.Self)
    {
        SetDao(Dao.XueDao);
    }

    /// <summary>
    /// 次数型寄生推进杀招：每场最多使用 2 次。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Charged;

    public override int MaxUses => 2;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await AdvanceLifecycleAsync(choiceContext);

        CardModel? host = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1)
            {
                Cancelable = false
            },
            XueDaoParasiteSystem.HasParasite,
            this
        )).FirstOrDefault();

        if (host != null)
        {
            await XueDaoParasiteSystem.TriggerDetachedAsync(
                choiceContext,
                host,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
