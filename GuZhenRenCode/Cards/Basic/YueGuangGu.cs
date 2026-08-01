using GuZhenRen.Characters;
using GuZhenRen.Powers;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

/// <summary>
/// 月光蛊。
///
/// 一至五转：
///
/// - 一转造成 6 点伤害；
/// - 每次升转，伤害增加 1 点；
/// - 五转时达到 10 点伤害。
///
/// 六转及以上：
///
/// - 伤害保持 10 点；
/// - 六转获得 1 层闪耀；
/// - 之后每升一转，额外获得 1 层闪耀。
///
/// 普通卡牌升级只添加虚无，不改变转数或伤害。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class YueGuangGu
    : AbstractGuZhenRenCard
{
    private const int Cost = 1;
    private const decimal BaseDamage = 6m;

    /// <summary>
    /// 五转前最多获得的伤害加成。
    ///
    /// 六转开始进入仙蛊阶段，因此一至五转共有四次伤害成长。
    /// </summary>
    private const int MaxPreXianGuDamageBonus =
        GuZhenRenCardRules.XianGuRank - 2;

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
        );

    public YueGuangGu()
        : base(
            baseCost: Cost,
            type: CardType.Attack,
            rarity: CardRarity.Common,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    /// <summary>
    /// 当前转数对应的基础伤害。
    ///
    /// GuRank 为 0 的规范模型仍显示初始 6 点伤害；
    /// 实际奖励牌会由项目现有逻辑分配一至九转。
    /// </summary>
    private decimal DamageForCurrentRank =>
        BaseDamage +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuDamageBonus
        );

    /// <summary>
    /// 六转起获得闪耀，之后每升一转增加一层。
    /// </summary>
    public int ShanYaoAmount =>
        Math.Max(
            0,
            GuRank -
            GuZhenRenCardRules.XianGuRank +
            1
        );

    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new DamageVar(
                BaseDamage,
                ValueProp.Move
            )
        ];

    /// <summary>
    /// 转数被设置、升转、读档或复制后，刷新伤害动态变量。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Damage.BaseValue =
            DamageForCurrentRank;
    }

    /// <summary>
    /// 为本地化描述提供当前转数和闪耀层数。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "Rank",
            GuRank
        );

        description.Add(
            "ShanYao",
            ShanYaoAmount
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

        await DamageCmd
            .Attack(
                DynamicVars.Damage.BaseValue
            )
            .FromCard(this, cardPlay)
            .Targeting(
                cardPlay.Target
            )
            .Execute(
                choiceContext
            );

        if (ShanYaoAmount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<ShanYaoPower>(
            choiceContext,
            Owner.Creature,
            ShanYaoAmount,
            Owner.Creature,
            this
        );
    }

    /// <summary>
    /// 普通升级只添加虚无，不参与蛊虫升转。
    /// </summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();

        AddKeyword(
            CardKeyword.Ethereal
        );
    }
}
