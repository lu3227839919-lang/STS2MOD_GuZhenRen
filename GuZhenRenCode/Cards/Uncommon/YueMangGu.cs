using GuZhenRen.Cards.Basic;
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Powers;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Uncommon;

/// <summary>
/// 月芒蛊。
///
/// 合练配方声明顺序：月光蛊 + 小光蛊 + 小光蛊。
///
/// - 蓝色（罕见）光道攻击牌；
/// - 2 费；
/// - 初始造成 12 点伤害并获得 2 层闪耀；
/// - 普通升级获得重放 1；
/// - 六转前每次升转伤害 +1，每完成两次升转闪耀 +1；
/// - 六转起，闪耀额外作用于 1 张光道攻击牌，之后每升一转 +1；
/// - 只能通过合练获得，不进入普通卡牌奖励，也不能在战斗中随机生成。
///
/// 合练结果转数由 AbstractGuZhenRenCard.InitializeFromHeLian
/// 确定为全部材料中的最高转数，并参与存档和多人运行快照。
/// </summary>
[HeLianRecipe(
    typeof(YueGuangGu),
    typeof(XiaoGuangGu),
    typeof(XiaoGuangGu)
)]
public sealed class YueMangGu
    : AbstractHeLianGuCard,
      ICardRewardExcluded,
      IShanYaoGeneratingAttack
{
    private const int Cost = 2;
    private const decimal BaseDamage = 12m;
    private const int BaseShanYao = 2;

    /// <summary>
    /// 一至五转共有四次升转成长。
    /// 六转后不再提高基础伤害和基础闪耀层数。
    /// </summary>
    private const int MaxPreXianGuRankIncreases =
        GuZhenRenCardRules.XianGuRank - 2;

    private int _shanYaoAmountBeforePlay;
    private int _extraUsesBeforePlay;

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
        );

    public YueMangGu()
        : base(
            baseCost: Cost,
            type: CardType.Attack,
            rarity: CardRarity.Uncommon,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    /// <summary>
    /// 一至五转依次为 12、13、14、15、16；
    /// 六转及以上保持 16。
    ///
    /// 卡牌图鉴规范模型最低为一转，显示初始 12。
    /// </summary>
    private decimal DamageForCurrentRank =>
        BaseDamage +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        );

    /// <summary>
    /// 一至五转依次为 2、2、3、3、4；
    /// 六转及以上保持 4。
    /// </summary>
    public int ShanYaoAmount =>
        BaseShanYao +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        ) / 2;

    /// <summary>
    /// 六至九转依次使闪耀额外作用于 1、2、3、4 张光道攻击牌。
    /// </summary>
    public int ExtraShanYaoUses =>
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

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Damage.BaseValue =
            DamageForCurrentRank;
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
            "ExtraShanYaoUses",
            ExtraShanYaoUses
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

        /*
         * 闪耀只在一次 Replay 系列的首段消耗。
         * 因此仅在首段保存“出牌前”状态，供 ShanYaoPower 确定性读取。
         */
        if (cardPlay.IsFirstInSeries)
        {
            _shanYaoAmountBeforePlay =
                Owner.Creature
                    .GetPower<ShanYaoPower>()
                    ?.Amount ??
                0;

            _extraUsesBeforePlay =
                Owner.Creature
                    .GetPower<
                        XiaoGuangGuShanYaoUsesPower
                    >()
                    ?.Amount ??
                0;
        }

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

        await PowerCmd.Apply<ShanYaoPower>(
            choiceContext,
            Owner.Creature,
            ShanYaoAmount,
            Owner.Creature,
            this
        );

        if (ExtraShanYaoUses <= 0)
        {
            return;
        }

        await AddExtraShanYaoUses(
            choiceContext
        );
    }

    private async Task AddExtraShanYaoUses(
        PlayerChoiceContext choiceContext
    )
    {
        XiaoGuangGuShanYaoUsesPower? existing =
            Owner.Creature.GetPower<
                XiaoGuangGuShanYaoUsesPower
            >();

        if (existing == null)
        {
            await PowerCmd.Apply<
                XiaoGuangGuShanYaoUsesPower
            >(
                choiceContext,
                Owner.Creature,
                ExtraShanYaoUses,
                Owner.Creature,
                this
            );

            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            existing,
            ExtraShanYaoUses,
            Owner.Creature,
            this,
            silent: true
        );
    }

    (
        int ShanYaoAmount,
        int ExtraUses
    ) IShanYaoGeneratingAttack
        .TakeShanYaoStateBeforePlay()
    {
        (
            int ShanYaoAmount,
            int ExtraUses
        ) state =
            (
                Math.Max(
                    0,
                    _shanYaoAmountBeforePlay
                ),
                Math.Max(
                    0,
                    _extraUsesBeforePlay
                )
            );

        _shanYaoAmountBeforePlay = 0;
        _extraUsesBeforePlay = 0;

        return state;
    }

    /// <summary>
    /// 普通升级使整张牌的完整效果额外重放一次。
    /// Replay 由游戏核心生成 CardPlay 系列并同步到所有联机端。
    /// </summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();

        BaseReplayCount += 1;
    }
}
