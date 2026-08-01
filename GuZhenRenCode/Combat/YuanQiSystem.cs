using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace GuZhenRen.Combat;

/// <summary>
/// 元气：本模组使用的第二套战斗资源。
///
/// 元气由 RitsuLib 的次级资源系统保存、同步并接入战斗界面；它与游戏原生能量
/// 和辉星完全独立，默认在每回合开始时清空，并且只在当前战斗内持久化。
/// </summary>
public static class YuanQiSystem
{
    public const string LocalId = "yuanqi";
    public static string ResourceId => $"{Entry.ModId}:{LocalId}";

    public static SecondaryResourceDefinition Definition { get; private set; } =
        new(
            defaultAmount: 5,
            baseMaxAmount: 99,
            minAmount: 0,
            hardMaxAmount: 999,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.AddMaxToCurrent,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            locTable: "secondary_resources",
            titleKey: "GU_ZHEN_REN_SECONDARY_RESOURCE_YUAN_QI.title",
            descriptionKey: "GU_ZHEN_REN_SECONDARY_RESOURCE_YUAN_QI.description",
            smallIconPath: $"{Entry.ResPath}/images/ui/yuanqi.svg",
            largeIconPath: $"{Entry.ResPath}/images/ui/yuanqi.svg"
        );

    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModSecondaryResourceRegistry registry =
                ModSecondaryResourceRegistry.For(Entry.ModId);

            // Register 返回已经绑定 ModId/完整 ID 的定义；后续 UI 和费用 API 都使用
            // 这个绑定后的实例，避免把未注册的模板对象传给 RitsuLib。
            Definition = registry.Register(LocalId, Definition);
            registry.RegisterCombatUi<NSecondaryResourceCounter>(
                LocalId,
                static _ => NSecondaryResourceCounter.Create(
                    Definition,
                    SecondaryResourceCounterStyle.Default
                ),
                static context => context.Node.Bind(context.Player, true),
                new NodeAttachmentOptions
                {
                    Name = "YuanQiCounter",
                    IncludeDerivedParentTypes = true,
                }
            );

            // 即使元气当前为 0，也让拥有本模组角色的战斗始终显示计数器。
            registry.AlwaysShowInCombatUiForCharacter<Characters.GuZhenRenCharacter>(
                LocalId
            );

            _initialized = true;
        }
    }

    public static void Uninitialize()
    {
        lock (SyncRoot)
        {
            _initialized = false;
        }
    }
}
