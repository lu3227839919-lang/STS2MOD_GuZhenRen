// ============================================================================
// 中文维护说明
// 文件职责：实现可附加到生物或玩家的能力模型与回合事件响应。
// 主要类型：LiDaoBattlePower。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using System.Numerics;

using GuZhenRen.Cards;
using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

[RegisterPower]
public sealed class LiDaoBattlePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ReferenceEquals(cardPlay.Player.Creature, Owner)
        ? LiDaoPhantomSystem.ResolveAttackAsync(choiceContext, cardPlay)
        : Task.CompletedTask;
}
