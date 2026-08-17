using System.Reflection;

using GuZhenRen.Aperture;
using GuZhenRen.Cards;
using GuZhenRen.Combat;
using GuZhenRen.Patches;
using GuZhenRen.Ui;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace GuZhenRen;

/// <summary>
/// 蛊真人模组的初始化入口。
/// </summary>
[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    public const string ModId = "GuZhenRenPersonal";

    public const string ResPath = "res://GuZhenRenPersonal";

    public static Logger Logger { get; } =
        RitsuLibFramework.CreateLogger(ModId);

    private static readonly object InitializationLock = new();

    private static readonly PatchLifecycle[] Lifecycles =
    [
        new(nameof(GuCardPileSystem), GuCardPileSystem.Initialize, GuCardPileSystem.Uninitialize),
        new(nameof(GuCardConsoleCommandPatch), GuCardConsoleCommandPatch.Initialize, GuCardConsoleCommandPatch.Uninitialize),
        new(nameof(GuCardPileCombatPatch), GuCardPileCombatPatch.Initialize, GuCardPileCombatPatch.Uninitialize),
        new(nameof(GuCardPlaySyncPatch), GuCardPlaySyncPatch.Initialize, GuCardPlaySyncPatch.Uninitialize),
        new(nameof(GuActivationModePatch), GuActivationModePatch.Initialize, GuActivationModePatch.Uninitialize),
        new(nameof(YuanQiSystem), YuanQiSystem.Initialize, YuanQiSystem.Uninitialize),
        new(nameof(ApertureSystem), ApertureSystem.Initialize, ApertureSystem.Uninitialize),
        // 按当前游戏语言延迟加载外部 zhs/eng JSON。
        new(
            nameof(LocalizationCompatibilityPatch),
            LocalizationCompatibilityPatch.Initialize,
            LocalizationCompatibilityPatch.Uninitialize
        ),
        new(nameof(GuRestSiteChoicePatch), GuRestSiteChoicePatch.Initialize, GuRestSiteChoicePatch.Uninitialize),
        new(nameof(CardCarouselPreviewPatch), CardCarouselPreviewPatch.Initialize, CardCarouselPreviewPatch.Uninitialize),
        new(nameof(DeckCardSelectionManualConfirmationPatch), DeckCardSelectionManualConfirmationPatch.Initialize, DeckCardSelectionManualConfirmationPatch.Uninitialize),
        new(nameof(StartingDeckGuRankPatch), StartingDeckGuRankPatch.Initialize, StartingDeckGuRankPatch.Uninitialize),
        new(nameof(NCardGuEnergyIconPatch), NCardGuEnergyIconPatch.Initialize, NCardGuEnergyIconPatch.Uninitialize),
        new(nameof(GuRankRewardPatch), GuRankRewardPatch.Initialize, GuRankRewardPatch.Uninitialize),
        new(nameof(GuWormUpgradePatch), GuWormUpgradePatch.Initialize, GuWormUpgradePatch.Uninitialize),
        new(nameof(GuPotionCompatibilityPatch), GuPotionCompatibilityPatch.Initialize, GuPotionCompatibilityPatch.Uninitialize),
        new(nameof(CardUniquenessPatch), CardUniquenessPatch.Initialize, CardUniquenessPatch.Uninitialize),
        new(nameof(HandCapacityExemptionPatch), HandCapacityExemptionPatch.Initialize, HandCapacityExemptionPatch.Uninitialize),
        new(nameof(EmptyCardRewardCompatibilityPatch), EmptyCardRewardCompatibilityPatch.Initialize, EmptyCardRewardCompatibilityPatch.Uninitialize),
        // 内置复合载体分别保存普通附魔与血寄，并显示成两个独立槽位。
        new(nameof(XueDaoEnchantmentSlotPatch), XueDaoEnchantmentSlotPatch.Initialize, XueDaoEnchantmentSlotPatch.Uninitialize),
        // 自定义关键词承担精简卡面说明的职责，所有卡牌界面都显示提示。
        new(nameof(XueDaoParasiteExhaustPatch), XueDaoParasiteExhaustPatch.Initialize, XueDaoParasiteExhaustPatch.Uninitialize),
        new(nameof(YiHaiCombatVictoryPatch), YiHaiCombatVictoryPatch.Initialize, YiHaiCombatVictoryPatch.Uninitialize),
        new(nameof(XueDaoRemainsKillPatch), XueDaoRemainsKillPatch.Initialize, XueDaoRemainsKillPatch.Uninitialize),
        new(nameof(MerchantInventoryCompatibilityPatch), MerchantInventoryCompatibilityPatch.Initialize, MerchantInventoryCompatibilityPatch.Uninitialize),
        new(nameof(AncientCompatibilityPatch), AncientCompatibilityPatch.Initialize, AncientCompatibilityPatch.Uninitialize),
        new(nameof(ShaZhaoTuiYanSystem), ShaZhaoTuiYanSystem.Initialize, ShaZhaoTuiYanSystem.Uninitialize),
        new(nameof(RecipeCompendiumSystem), RecipeCompendiumSystem.Initialize, RecipeCompendiumSystem.Uninitialize),
    ];

    private static bool _contentRegistered;
    private static bool _initialized;

    public static void Initialize()
    {
        lock (InitializationLock)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                RegisterContentOnce();

                foreach (PatchLifecycle lifecycle in Lifecycles)
                {
                    lifecycle.Initialize();
                }

                _initialized = true;
                TryLog("GuZhenRen Mod 初始化完成。");
            }
            catch (Exception exception)
            {
                _initialized = false;
                RollbackLifecycles();

                TryLog(
                    "GuZhenRen Mod 初始化失败，已回滚本次运行时补丁；" +
                    $"下次调用可重试。{exception}"
                );

                throw;
            }
        }
    }

    private static void RegisterContentOnce()
    {
        if (_contentRegistered)
        {
            return;
        }

        Assembly assembly = Assembly.GetExecutingAssembly();
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // PCK 场景包含 OrbPreview.cs，因此需要注册 Godot C# 脚本。
        RitsuLibFramework.EnsureGodotScriptsRegistered(
            assembly,
            Logger
        );

        // 所有卡牌统一使用 GuZhenRenPersonal/images/cards 下的同名 PNG。
        // 缺失文件只记录明确警告，不静默复用其他卡图。
        CardImageCatalog.ValidateAssembly(assembly);

        _contentRegistered = true;
    }

    private static void RollbackLifecycles()
    {
        for (int index = Lifecycles.Length - 1; index >= 0; index--)
        {
            PatchLifecycle lifecycle = Lifecycles[index];

            try
            {
                lifecycle.Uninitialize();
            }
            catch (Exception exception)
            {
                TryLog($"回滚 {lifecycle.Name} 失败：{exception}");
            }
        }
    }

    private static void TryLog(string message)
    {
        try
        {
            Logger.Info(message);
        }
        catch
        {
            // 日志失败不能改变初始化或回滚结果。
        }
    }

    private readonly record struct PatchLifecycle(
        string Name,
        Action Initialize,
        Action Uninitialize
    );
}
