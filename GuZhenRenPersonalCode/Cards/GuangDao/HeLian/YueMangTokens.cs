// ============================================================================
// 中文维护说明
// 文件职责：定义同一玩法分支共享的衍生牌基类与令牌约定。
// 主要类型：AbstractYueMangToken。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

public abstract class AbstractYueMangToken
    : AbstractGuZhenRenGeneratedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    protected AbstractYueMangToken(int cost)
        : base(
            cost,
            CardType.Attack,
            CardRarity.Token,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    protected async Task AttackMany(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Creature target,
        int hitCount,
        decimal damage,
        decimal finalHitBonus = 0
    )
    {
        for (int hit = 0; hit < hitCount; hit++)
        {
            decimal currentDamage = damage;
            if (hit == hitCount - 1)
            {
                currentDamage += finalHitBonus;
            }

            await DamageCmd
                .Attack(currentDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (target.IsDead)
            {
                break;
            }
        }
    }
}


