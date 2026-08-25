using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 万我生成的“我力虚影”与独立临时生命池。
///
/// 我力虚影：每个提供 5 点独立来源临时生命；第一次显化必定成功，
/// 之后按万我转数决定显化率（6/7/8/9转 = 25/30/35/40%）。
/// 显化成功时复制本次触发动作 50% 的可复制效果（伤害按段、格挡）。
/// </summary>
public static class WoLiPhantomSystem
{
    internal static async Task AddShadowsAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        int rank,
        int count
    )
    {
        if (count <= 0)
        {
            return;
        }

        WoLiXuYing? controllerSource = null;

        for (int index = 0; index < count; index++)
        {
            WoLiXuYing phantom = GuGeneratedCardFactory.Create<WoLiXuYing>(
                owner,
                rank,
                upgraded: false
            );
            controllerSource ??= phantom;

            await GuGeneratedCardFactory.AddToHandOrDiscard(
                phantom,
                owner
            );
        }

        // 我力虚影由《万我》直接生成，并不会经过普通兽力蛊的
        // ActivateBeastGuAsync 流程。若此前没有兽力虚影，战斗中可能
        // 尚不存在 LiDaoBattlePower，导致后续攻击完全不会进入集中
        // 显化判定——即使我力虚影的首次显化概率已经是 100%。
        //
        // 因此，只要成功生成了我力虚影，就在这里确保集中显化控制器
        // 已注册。这样下一次符合条件的攻击即可正常触发首次必定显化。
        if (controllerSource != null)
        {
            await LiDaoPhantomSystem.EnsureControllerAsync(
                choiceContext,
                controllerSource
            );
        }

        WoLiTempHpPower? existing =
            owner.Creature.GetPower<WoLiTempHpPower>();
        if (existing == null)
        {
            WoLiTempHpPower power =
                (WoLiTempHpPower)ModelDb.Power<WoLiTempHpPower>().ToMutable();
            power.AddShadows(count);
            await PowerCmd.Apply(
                choiceContext,
                power,
                owner.Creature,
                count,
                owner.Creature,
                null
            );
        }
        else
        {
            existing.AddShadows(count);
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                count,
                owner.Creature,
                null
            );
        }
    }

    internal static async Task ConsumeShadowsAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        int count
    )
    {
        if (count <= 0)
        {
            return;
        }

        WoLiXuYing[] shadows = LiDaoPhantomSystem
            .GetPermanentPhantoms(owner)
            .OfType<WoLiXuYing>()
            .Take(count)
            .ToArray();

        foreach (WoLiXuYing shadow in shadows)
        {
            await CardPileCmd.RemoveFromCombat(
                shadow,
                skipVisuals: false
            );
        }
    }

    internal static async Task ExecuteCopyAsync(
        WoLiXuYing shadow,
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        CardModel source = triggeringPlay.Card;
        const decimal copyRatio = 0.5m;

        if (source.Type == CardType.Attack &&
            target != null &&
            TryGetDynamicValue(source, "Damage", out decimal damage))
        {
            int hits = TryGetDynamicInt(source, "Hits", 1);
            int perHit = Math.Max(
                0,
                (int)Math.Floor(damage * copyRatio)
            );
            for (int index = 0; index < hits; index++)
            {
                if (perHit <= 0 || !target.IsAlive)
                {
                    break;
                }

                await DamageCmd.Attack(perHit)
                    .FromCard(shadow, cardPlay: null)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
        }

        if (TryGetDynamicValue(source, "Block", out decimal block))
        {
            int copiedBlock = Math.Max(
                0,
                (int)Math.Floor(block * copyRatio)
            );
            if (copiedBlock > 0)
            {
                await CreatureCmd.GainBlock(
                    shadow.Owner.Creature,
                    new BlockVar(copiedBlock, ValueProp.Move),
                    cardPlay: null
                );
            }
        }
    }

    private static int TryGetDynamicInt(
        CardModel card,
        string key,
        int fallback
    )
    {
        try
        {
            return Math.Max(1, card.DynamicVars[key].IntValue);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool TryGetDynamicValue(
        CardModel card,
        string key,
        out decimal value
    )
    {
        try
        {
            value = card.DynamicVars[key].BaseValue;
            return value > 0;
        }
        catch
        {
            value = 0m;
            return false;
        }
    }
}
