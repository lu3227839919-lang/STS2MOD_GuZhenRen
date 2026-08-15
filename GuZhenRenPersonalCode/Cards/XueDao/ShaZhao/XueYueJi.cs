using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.XueDao;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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
            CardKeyword.Retain,
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
    /// 次数型寄生推进杀招：一至二转 1 次、三至五转 2 次、
    /// 六至九转 3 次；最后一次使用后消耗并返还材料。
    /// </summary>
    public override ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Charged;

    public override int ShaZhaoMaxUses => GuRank switch
    {
        <= 2 => 1,
        <= 5 => 2,
        _ => 3,
    };

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        try
        {
            // 可选宿主区域随转数提升：
            // 一至五转仅手牌，六至七转加入弃牌堆，八至九转再加入抽牌堆。
            List<CardModel> candidates =
            [
                .. PileType.Hand.GetPile(Owner).Cards
                    .Where(XueDaoParasiteSystem.HasParasite),
            ];

            if (GuRank >= 6)
            {
                candidates.AddRange(
                    PileType.Discard.GetPile(Owner).Cards
                        .Where(XueDaoParasiteSystem.HasParasite)
                );
            }

            if (GuRank >= 8)
            {
                candidates.AddRange(
                    PileType.Draw.GetPile(Owner).Cards
                        .Where(XueDaoParasiteSystem.HasParasite)
                );
            }

            if (candidates.Count == 0)
            {
                return;
            }

            CardModel? host = (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    candidates.ToArray(),
                    Owner,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = true,
                    }
                )
            ).FirstOrDefault();

            if (host == null)
            {
                return;
            }

            await XueDaoParasiteSystem.TriggerDetachedAsync(
                choiceContext,
                host,
                this
            );

            // 九转质变：本次祭炼使寄生完成孵化时，额外获得 2 点血元。
            if (GuRank >= 9 &&
                !XueDaoParasiteSystem.HasParasite(host))
            {
                await XueDaoPowerSystem.GainXueYuanFromCardEffect(
                    choiceContext,
                    this,
                    2
                );
            }
        }
        finally
        {
            await AdvanceLifecycleAsync(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
