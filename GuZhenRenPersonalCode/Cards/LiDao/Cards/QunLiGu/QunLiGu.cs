using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 群力蛊：练满后仍可把伴生牌产生的有效练力转为群力层数，
/// 并在常驻力道虚影显化后进行不递归的额外触发判定。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class QunLiGu : AbstractLiDaoGuCard, ILiDaoExtraTrainingGuCard
{
    public override int TrainingRequired => GuRank >= 8 ? 1 : 2;

    public override Type CompanionCardType => typeof(ZhongLiYiJi);

    public override int RecoveryDelayTurns => GuRank >= 9 ? 4 : 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("GroupChance", 25m)];

    public QunLiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await LiDaoPowerSystem.ActivateQunLiAsync(
            choiceContext,
            this
        );
        await LiDaoPhantomSystem.EnsureControllerAsync(
            choiceContext,
            this
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<ZhongLiYiJi>(this)];

    internal static int GroupChanceAtRank(int rank) => rank switch
    {
        <= 5 => 0,
        6 => 25,
        7 => 30,
        8 => 35,
        _ => 40,
    };

    private void RefreshRankValues() =>
        DynamicVars["GroupChance"].BaseValue = GroupChanceAtRank(GuRank);
}
