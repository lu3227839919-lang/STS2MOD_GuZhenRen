using Godot;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace GuZhenRen.Combat;

/// <summary>
/// 元气：本模组使用的第二套战斗资源。
///
/// 元气由 RitsuLib 的次级资源系统保存、同步并接入战斗界面；它与游戏原生能量
/// 和辉星完全独立。初始为5点，上限随空窍转数提升至25点，并在回合开始回复2点。
/// </summary>
public static class YuanQiSystem
{
    public const string LocalId = "yuanqi";
    public static string ResourceId => $"{Entry.ModId}:{LocalId}";

    public const string SecondaryEnergyCounterScenePath =
        "res://GuZhenRen/scenes/ui/nodes2/GuZhenRen_energy_counter2.tscn";

    public const string LargeIconPath =
        "res://GuZhenRen/images/characters/energy2_big.png";

    public const string SmallIconPath =
        "res://GuZhenRen/images/characters/energy2_text.png";

    private static readonly SecondaryResourceCounterStyle
        MultiplayerCounterStyle =
            SecondaryResourceCounterStyle.Default with
            {
                CounterSize = new Vector2(34f, 34f),
                IconSize = new Vector2(32f, 32f),
                FontSize = 18,
                OutlineSize = 4,
                AnimateAmountGain = false,
                FormatAmount = static (amount, _) =>
                    amount.ToString(),
            };

    public static SecondaryResourceDefinition Definition { get; private set; } =
        new(
            defaultAmount: 5,
            baseMaxAmount: 5,
            minAmount: 0,
            hardMaxAmount: 25,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            locTable: "secondary_resources",
            titleKey: "GU_ZHEN_REN_SECONDARY_RESOURCE_YUAN_QI.title",
            descriptionKey: "GU_ZHEN_REN_SECONDARY_RESOURCE_YUAN_QI.description",
            smallIconPath: SmallIconPath,
            largeIconPath: LargeIconPath
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
            registry.RegisterCombatUi<YuanQiEnergyCounter>(
                LocalId,
                static _ => YuanQiEnergyCounter.Create(
                    Definition,
                    SecondaryEnergyCounterScenePath
                ),
                static context =>
                {
                    context.Node.Bind(context.Player);

                    // 节点挂载注册会作用于所有角色的战斗界面；只有本模组
                    // 角色才移动原版能量容器，避免影响其他角色或联机队友。
                    if (context.Player?.Character is
                        Characters.GuZhenRenCharacter)
                    {
                        context.Node.AttachBesideNativeEnergyCounter(
                            context.Parent
                        );
                    }
                },
                static context =>
                    context.Node.Refresh(context.Player),
                new NodeAttachmentOptions
                {
                    Name = "YuanQiCounter",
                    IncludeDerivedParentTypes = true,
                }
            );

            // 联机队友状态栏显示紧凑元气数值，避免只在本机战斗 UI 可见。
            registry.RegisterMultiplayerPlayerStateUi<
                NSecondaryResourceCounter
            >(
                LocalId + "_multiplayer_state",
                static _ => NSecondaryResourceCounter.Create(
                    Definition,
                    MultiplayerCounterStyle
                ),
                static context =>
                    context.Node.Bind(context.Player, true),
                new NodeAttachmentOptions
                {
                    Name = "YuanQiMultiplayerCounter",
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
            // RitsuLib 的资源与节点挂载注册是进程级且不可撤销的。
            // 保持初始化标记，避免初始化回滚后重试时重复注册界面节点。
        }
    }
}
