using GuZhenRen.Cards.Basic;
using GuZhenRen.Characters;
using GuZhenRen.Powers;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招：月霓裳。
///
/// 有序配方：月光蛊 → 玉皮蛊。
///
/// 一至五转：
///
/// - 一转造成 6 点伤害，获得 5 点防御；
/// - 每升一转，伤害和防御各增加 1；
/// - 五转时造成 10 点伤害，获得 9 点防御。
///
/// 六转及以上：
///
/// - 伤害和防御保持五转数值；
/// - 六转获得 1 层闪耀，此后每升一转增加 1 层；
/// - 本回合剩余的防御不会在下回合开始时消失。
///
/// 杀招转数继承两张材料中的最高转数，不进入普通奖励池。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[ShaZhaoRecipe(
    typeof(YueGuangGu),
    typeof(YuPiGu)
)]
public sealed class YueNiChang
    : AbstractShaZhaoCard
{
    private const int Cost = 1;
    private const decimal BaseDamage = 6m;
    private const decimal BaseBlock = 5m;

    /// <summary>
    /// 六转开始进入仙蛊阶段，因此一至五转共有四次数值成长。
    /// </summary>
    private const int MaxPreXianGuRankIncreases =
        GuZhenRenCardRules.XianGuRank - 2;

    /// <summary>
    /// 暂时复用月光蛊卡图，避免缺少 YueNiChang.png 时加载失败。
    /// 添加正式卡图后，可将文件名改为 YueNiChang.png。
    /// </summary>
    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/YueGuangGu.png"
        );

    public override bool GainsBlock =>
        true;

    public YueNiChang()
        : base(
            baseCost: Cost,
            type: CardType.Attack,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    /// <summary>
    /// 一至五转伤害依次为 6、7、8、9、10；
    /// 六转及以上保持 10。
    /// </summary>
    private decimal DamageForCurrentRank =>
        BaseDamage +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        );

    /// <summary>
    /// 一至五转防御依次为 5、6、7、8、9；
    /// 六转及以上保持 9。
    /// </summary>
    private decimal BlockForCurrentRank =>
        BaseBlock +
        Math.Clamp(
            GuRank - 1,
            0,
            MaxPreXianGuRankIncreases
        );

    /// <summary>
    /// 六至九转闪耀依次为 1、2、3、4。
    /// </summary>
    public int ShanYaoAmount =>
        Math.Max(
            0,
            GuRank -
            GuZhenRenCardRules.XianGuRank +
            1
        );

    /// <summary>
    /// 六转及以上保留当前剩余防御到下一个回合。
    /// </summary>
    public bool PreservesBlock =>
        GuRank >=
        GuZhenRenCardRules.XianGuRank;

    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new DamageVar(
                BaseDamage,
                ValueProp.Move
            ),
            new BlockVar(
                BaseBlock,
                ValueProp.Move
            )
        ];

    /// <summary>
    /// 组成、读档或复制后，根据杀招转数刷新伤害与防御。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        DynamicVars.Damage.BaseValue =
            DamageForCurrentRank;

        DynamicVars.Block.BaseValue =
            BlockForCurrentRank;
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
            "PreservesBlock",
            PreservesBlock ? 1 : 0
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

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );

        if (!PreservesBlock)
        {
            return;
        }

        // 原生 BlurPower 的 1 层正好表示：
        // 下一次自身回合开始时阻止防御清除，随后移除这一层。
        await PowerCmd.Apply<BlurPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this
        );
    }

    /// <summary>
    /// 在普通 AfterCardPlayed 钩子全部结算后再给予新闪耀。
    ///
    /// 这样，打出月霓裳前已有的闪耀会正常强化本次攻击并被消耗，
    /// 本牌新产生的闪耀则会保留给之后的光道攻击牌。
    ///
    /// 必须检查同一个卡牌实例，否则牌堆中其他月霓裳也会重复触发。
    /// </summary>
    public override async Task AfterCardPlayedLate(
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

        if (!ReferenceEquals(
                cardPlay.Card,
                this
            ) ||
            ShanYaoAmount <= 0)
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
}
