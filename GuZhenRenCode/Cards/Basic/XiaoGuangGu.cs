using GuZhenRen.Characters;
using GuZhenRen.Powers;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

/// <summary>
/// 小光蛊。
///
/// 一至五转：
///
/// - 一转施加 1 层虚弱并获得 1 层闪耀；
/// - 每完成两次升转，虚弱与闪耀各增加 1 层；
/// - 五转时施加 3 层虚弱并获得 3 层闪耀。
///
/// 六转及以上保持五转数值。
///
/// 普通升级只添加保留，不改变转数、虚弱或闪耀层数。
///
/// 本牌是技能牌，因此获得的闪耀不会被自身立即消耗。
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class XiaoGuangGu
    : AbstractGuZhenRenCard
{
    private const int Cost = 0;
    private const decimal BaseWeak = 1m;
    private const int BaseShanYao = 1;

    /// <summary>
    /// 六转开始进入仙蛊阶段，因此一至五转共有四次升转成长。
    /// </summary>
    private const int MaxPreXianGuRankIncreases =
        GuZhenRenCardRules.XianGuRank - 2;

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
        );

    public XiaoGuangGu()
        : base(
            baseCost: Cost,
            type: CardType.Skill,
            rarity: CardRarity.Common,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    /// <summary>
    /// 一至五转虚弱依次为 1、1、2、2、3；
    /// 六转及以上保持 3。
    /// </summary>
    private decimal WeakForCurrentRank =>
        BaseWeak +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        ) / 2;

    /// <summary>
    /// 一至五转闪耀依次为 1、1、2、2、3；
    /// 六转及以上保持 3。
    /// </summary>
    public int ShanYaoAmount =>
        BaseShanYao +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        ) / 2;

    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new PowerVar<WeakPower>(
                BaseWeak
            )
        ];

    /// <summary>
    /// 设置转数、升转、复制或读档后刷新虚弱动态变量。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Weak.BaseValue =
            WeakForCurrentRank;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "ShanYao",
            ShanYaoAmount
        );

        description.Add(
            "Retain",
            IsUpgraded ? 1 : 0
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(
            choiceContext
        );
        ArgumentNullException.ThrowIfNull(
            cardPlay
        );
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target
        );

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this
        );

        await PowerCmd.Apply<ShanYaoPower>(
            choiceContext,
            Owner.Creature,
            ShanYaoAmount,
            Owner.Creature,
            this
        );
    }

    /// <summary>
    /// 普通升级只添加保留，不参与蛊虫升转。
    /// </summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();

        AddKeyword(
            CardKeyword.Retain
        );
    }
}
