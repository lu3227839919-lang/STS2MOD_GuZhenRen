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
/// 白豕蛊。
///
/// 一至五转：
///
/// - 一转获得 2 层临时敏捷；
/// - 每完成两次升转，额外获得 1 层临时敏捷；
/// - 五转时获得 4 层临时敏捷。
///
/// 六转及以上：
///
/// - 临时敏捷保持五转数值；
/// - 六转使剩余防御保留到下一个回合；
/// - 此后每升一转，防御可多保留一个回合。
///
/// 初始拥有消耗；普通卡牌升级移除消耗。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class BaiShiGu
    : AbstractGuZhenRenCard
{
    private const int Cost = 1;
    private const decimal BaseDexterity = 2m;

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

    public BaiShiGu()
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
    /// 白豕蛊初始会消耗；升级时由 OnUpgrade 移除。
    /// </summary>
    public override IEnumerable<CardKeyword>
        CanonicalKeywords =>
            base.CanonicalKeywords.Append(
                CardKeyword.Exhaust
            );

    /// <summary>
    /// 当前转数对应的临时敏捷层数。
    ///
    /// 一至五转依次为 2、2、3、3、4；
    /// 六转及以上保持 4。
    /// </summary>
    private decimal DexterityForCurrentRank =>
        BaseDexterity +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        ) / 2;

    /// <summary>
    /// 剩余防御可以保留的未来回合数。
    ///
    /// 一至五转为 0；
    /// 六至九转依次为 1、2、3、4。
    /// </summary>
    public int BlockRetentionTurns =>
        Math.Max(
            0,
            GuRank -
            GuZhenRenCardRules.XianGuRank +
            1
        );

    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new PowerVar<DexterityPower>(
                BaseDexterity
            )
        ];

    /// <summary>
    /// 转数被设置、升转、读档或复制后，刷新临时敏捷动态变量。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Dexterity.BaseValue =
            DexterityForCurrentRank;
    }

    /// <summary>
    /// 为本地化描述提供临时敏捷别名与防御保留回合数。
    /// “消耗”由 CardModel 根据 CardKeyword.Exhaust 自动追加。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        // PowerVar<DexterityPower> 的规范变量名是 DexterityPower。
        // 本地化使用短别名 Dexterity，并继续传入 DynamicVar 以显示差值高亮。
        description.AddObj(
            "Dexterity",
            DynamicVars.Dexterity
        );

        description.Add(
            "BlockRetentionTurns",
            BlockRetentionTurns
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

        await PowerCmd.Apply<
            BaiShiGuTemporaryDexterityPower
        >(
            choiceContext,
            Owner.Creature,
            DynamicVars.Dexterity.BaseValue,
            Owner.Creature,
            this
        );

        if (BlockRetentionTurns <= 0)
        {
            return;
        }

        await ApplyTemporaryBlockRetention(
            choiceContext
        );
    }

    /// <summary>
    /// 使用原生壁垒负责阻止回合开始时清除防御，
    /// 再由隐藏的计时能力在指定回合数结束后移除壁垒。
    ///
    /// 若玩家已经拥有非本卡提供的壁垒，本效果无需重复施加，
    /// 也不会创建计时器或移除已有壁垒。
    /// </summary>
    private async Task ApplyTemporaryBlockRetention(
        PlayerChoiceContext choiceContext
    )
    {
        BarricadePower? barricade =
            Owner.Creature.GetPower<
                BarricadePower
            >();

        BaiShiGuBlockRetentionPower? timer =
            Owner.Creature.GetPower<
                BaiShiGuBlockRetentionPower
            >();

        // 已经存在永久或其他来源的壁垒时，本效果没有额外收益。
        // timer != null 表示壁垒属于白豕蛊的临时效果，可以正常延长。
        if (barricade != null &&
            timer == null)
        {
            return;
        }

        if (barricade == null)
        {
            await PowerCmd.Apply<BarricadePower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this
            );
        }

        if (timer == null)
        {
            await PowerCmd.Apply<
                BaiShiGuBlockRetentionPower
            >(
                choiceContext,
                Owner.Creature,
                BlockRetentionTurns,
                Owner.Creature,
                this
            );

            return;
        }

        // 重复打出时取更长的剩余持续时间，而不是把回合数相加。
        int missingTurns =
            BlockRetentionTurns -
            timer.Amount;

        if (missingTurns <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            timer,
            missingTurns,
            Owner.Creature,
            this,
            silent: true
        );
    }

    /// <summary>
    /// 普通升级删除消耗，不参与蛊虫升转。
    /// </summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();

        RemoveKeyword(
            CardKeyword.Exhaust
        );
    }
}
