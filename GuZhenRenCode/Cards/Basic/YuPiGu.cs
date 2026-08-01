using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

/// <summary>
/// 玉皮蛊。
///
/// 一至五转：
///
/// - 一转获得 5 点防御；
/// - 每次升转，防御增加 2 点；
/// - 五转时达到 13 点防御。
///
/// 六转及以上：
///
/// - 防御保持 13 点；
/// - 打出后获得壁垒，使防御不再在回合开始时消失。
///
/// 普通卡牌升级只添加虚无，不改变转数或防御。
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class YuPiGu
    : AbstractGuZhenRenCard
{
    private const int Cost = 1;
    private const decimal BaseBlock = 5m;
    private const decimal BlockPerRank = 2m;

    /// <summary>
    /// 六转开始进入仙蛊阶段，因此一至五转共有四次防御成长。
    /// </summary>
    private const int MaxPreXianGuBlockIncreases =
        GuZhenRenCardRules.XianGuRank - 2;

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
        );

    public override bool GainsBlock =>
        true;

    public YuPiGu()
        : base(
            baseCost: Cost,
            type: CardType.Skill,
            rarity: CardRarity.Common,
            target: TargetType.Self
        )
    {
        SetDao(Dao.TuDao);
    }

    /// <summary>
    /// 当前转数对应的基础防御。
    ///
    /// GuRank 为 0 的规范模型仍显示初始 5 点防御；
    /// 实际奖励牌会由项目现有逻辑分配一至九转。
    /// </summary>
    private decimal BlockForCurrentRank =>
        BaseBlock +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuBlockIncreases
        ) * BlockPerRank;

    /// <summary>
    /// 六转及以上会获得壁垒效果。
    /// </summary>
    public bool GrantsBarricade =>
        GuRank >=
        GuZhenRenCardRules.XianGuRank;

    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new BlockVar(
                BaseBlock,
                ValueProp.Move
            )
        ];

    /// <summary>
    /// 转数被设置、升转、读档或复制后，刷新防御动态变量。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Block.BaseValue =
            BlockForCurrentRank;
    }

    /// <summary>
    /// 为本地化描述提供六转效果条件。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "GrantsBarricade",
            GrantsBarricade ? 1 : 0
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

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        if (!GrantsBarricade ||
            Owner.Creature.GetPower<BarricadePower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<BarricadePower>(
            choiceContext,
            Owner.Creature,
            1m,
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
