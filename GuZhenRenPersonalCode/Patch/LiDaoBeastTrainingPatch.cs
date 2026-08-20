using GuZhenRen.Cards.LiDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks; 
using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRen.Patches;

/// <summary>在一张伴生牌完整有效结算后统一登记本次炼力。</summary>
internal static class LiDaoBeastTrainingPatch
{
    private const string HarmonyId =
        Entry.ModId + ".LiDaoBeastTraining";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.AfterCardPlayed)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterCardPlayed)
            ),
            postfix: new HarmonyMethod(
                typeof(LiDaoBeastTrainingPatch),
                nameof(AfterCardPlayedPostfix)
            )
        );
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void AfterCardPlayedPostfix(
        CardPlay cardPlay,
        ref Task __result
    )
    {
        if (cardPlay.Card is not ILiDaoCompanionCard)
        {
            return;
        }

        __result = AwaitAndRecordAsync(__result, cardPlay);
    }

    private static async Task AwaitAndRecordAsync(
        Task original,
        CardPlay cardPlay
    )
    {
        await original;
        await LiDaoBeastTrainingSystem.RecordCompanionPlayAsync(
            cardPlay.Card,
            cardPlay
        );
    }
}
