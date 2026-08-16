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

public static class LiDaoPowerSystem
{
    public static async Task ActivateQunLiAsync(
        PlayerChoiceContext choiceContext,
        QunLiGu source
    )
    {
        QunLiPower? existing = source.Owner.Creature.GetPower<QunLiPower>();
        if (existing == null)
        {
            QunLiPower power =
                (QunLiPower)ModelDb.Power<QunLiPower>().ToMutable();
            power.ConfigureRank(source.GuRank);
            await PowerCmd.Apply(
                choiceContext,
                power,
                source.Owner.Creature,
                1,
                source.Owner.Creature,
                source
            );
        }
        else
        {
            existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                1,
                source.Owner.Creature,
                source
            );
        }

        await LiDaoTrainingSystem.FlushExtraTrainingAsync(
            choiceContext,
            source
        );
    }

    public static async Task ActivateWoLiAsync(
        PlayerChoiceContext choiceContext,
        WoLiGu source
    )
    {
        if (source.Owner.Creature.GetPower<WoLiPower>() != null)
        {
            return;
        }

        WoLiPower power =
            (WoLiPower)ModelDb.Power<WoLiPower>().ToMutable();
        power.ConfigureRank(source.GuRank);
        await PowerCmd.Apply(
            choiceContext,
            power,
            source.Owner.Creature,
            1,
            source.Owner.Creature,
            source
        );
    }

    public static async Task ActivateKuLiAsync(
        PlayerChoiceContext choiceContext,
        KuLiGu source
    )
    {
        KuLiPower? existing = source.Owner.Creature.GetPower<KuLiPower>();
        if (existing == null)
        {
            KuLiPower power =
                (KuLiPower)ModelDb.Power<KuLiPower>().ToMutable();
            power.ConfigureRank(source.GuRank);
            await PowerCmd.Apply(
                choiceContext,
                power,
                source.Owner.Creature,
                1,
                source.Owner.Creature,
                source
            );
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
        if (existing.GrindingStacks < 3)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                1,
                source.Owner.Creature,
                source
            );
        }
    }

    public static async Task ActivateZiLiAsync(
        PlayerChoiceContext choiceContext,
        ZiLiGengShengGu source
    )
    {
        ZiLiGengShengPower? existing =
            source.Owner.Creature.GetPower<ZiLiGengShengPower>();
        if (existing == null)
        {
            ZiLiGengShengPower power =
                (ZiLiGengShengPower)
                    ModelDb.Power<ZiLiGengShengPower>().ToMutable();
            power.ConfigureRank(source.GuRank);
            await PowerCmd.Apply(
                choiceContext,
                power,
                source.Owner.Creature,
                1,
                source.Owner.Creature,
                source
            );
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
        if (existing.StrongBodyStacks < 3)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                1,
                source.Owner.Creature,
                source
            );
        }
    }

    public static decimal GetBeastEffectMultiplier(Creature owner) =>
        owner.GetPower<KuLiPower>()?.EffectMultiplier ?? 1m;

    public static Task NotifyManifested(
        Creature owner,
        LiDaoBeastKind kind
    ) => owner.GetPower<ZiLiGengShengPower>() is { } power
        ? power.RecordManifestationAsync(kind)
        : Task.CompletedTask;

    public static Task NotifyPhantomTriggered(
        PlayerChoiceContext choiceContext,
        Creature owner,
        AbstractLiDaoXuYing phantom
    ) => owner.GetPower<WoLiPower>() is { } power
        ? power.RecordPhantomTriggerAsync(choiceContext, phantom)
        : Task.CompletedTask;

    public static int GetPhantomStrengthBonus(Creature owner) =>
        owner.GetPower<WoLiPower>() is { StrengthApplies: true } &&
        owner.GetPower<StrengthPower>() is { } strength
            ? Math.Max(0, strength.Amount)
            : 0;

    public static bool TryRollGroupRepeat(
        Player owner,
        AbstractLiDaoXuYing phantom
    )
    {
        if (phantom.IsFullForcePhantom || phantom is WoLiXuYing)
        {
            return false;
        }

        return owner.Creature.GetPower<QunLiPower>() is { } power &&
            power.TryRollRepeat(owner);
    }

    public static void NotifyCondensed(Creature owner) =>
        owner.GetPower<ZiLiGengShengPower>()?.RecordCondensation();
}
