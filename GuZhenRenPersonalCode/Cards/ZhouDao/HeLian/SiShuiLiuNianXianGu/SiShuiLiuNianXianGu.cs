using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

using GuZhenRen.Cards.ZhouDao;

namespace GuZhenRen.Cards.HeLian;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
// 八转以上年蛊 ×1 + 八转以上光阴荏苒蛊 ×1 → 似水流年仙蛊
[HeLianRecipe(typeof(NianGu), typeof(GuangYinRenRanGu), MinimumMaterialRank = 8)]
// 或 八转以上年蛊 ×1 + 八转以上日蛊 ×1 + 八转以上月蛊 ×1 → 似水流年仙蛊
[HeLianRecipe(typeof(NianGu), typeof(RiGu), typeof(YueGu), MinimumMaterialRank = 8)]
public sealed class SiShuiLiuNianXianGu : AbstractZhouDaoCompanionGuCard, ICardRewardExcluded
{
    public override int MinimumAvailableGuRank => 8;
    public override Type CompanionCardType => typeof(SiShuiLiuNian);
    public override int RecoveryDelayTurns => 4;

    public SiShuiLiuNianXianGu() : base(CardRarity.Rare)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("NianHuaGain", GuRank >= 9 ? 5 : 4);
        description.Add("PlusToken", GuRank >= 9 ? 1 : 0);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int gain = GuRank >= 9 ? 5 : 4;
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        AbstractGuZhenRenCard token = GuRank >= 9
            ? GuGeneratedCardFactory.Create<NianLiuPlus>(Owner, 9)
            : GuGeneratedCardFactory.Create<NianLiu>(Owner, 8);
        await GuGeneratedCardFactory.AddToHandOrDiscard(token, Owner);
    }
}
