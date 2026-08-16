using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

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
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
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
            Entry.Logger.Info(
                $"[苦力] ActivateKuLiAsync 已施加 KuLiPower，rank={source.GuRank}，" +
                $"card={source.Id}。"
            );
            power.RecordActivation();
            await power.SyncInjuryThresholdsAsync(choiceContext);
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
        existing.RecordActivation();
        await existing.SyncInjuryThresholdsAsync(choiceContext);
        Entry.Logger.Info(
            $"[苦力] ActivateKuLiAsync 已更新 KuLiPower，rank={existing.Rank}，" +
            $"card={source.Id}。"
        );
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
            power.Arm();
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
        existing.Arm();
    }
}
