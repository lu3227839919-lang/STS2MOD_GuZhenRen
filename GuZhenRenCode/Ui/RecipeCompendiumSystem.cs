using Godot;

namespace GuZhenRen.Ui;

/// <summary>
/// 在游戏根节点上维护配方大全界面。界面只读取配方注册表，不参与
/// 任何战斗状态、随机数或多人同步。
/// </summary>
internal static class RecipeCompendiumSystem
{
    private static RecipeCompendiumOverlay? _overlay;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            throw new InvalidOperationException(
                "配方大全初始化失败：当前主循环不是 SceneTree。"
            );
        }

        RecipeCompendiumOverlay overlay = new()
        {
            Name = "GuZhenRenRecipeCompendium",
        };

        _overlay = overlay;
        tree.Root.CallDeferred(Node.MethodName.AddChild, overlay);
        _initialized = true;
    }

    internal static void Uninitialize()
    {
        RecipeCompendiumOverlay? overlay = _overlay;
        _overlay = null;
        _initialized = false;

        if (GodotObject.IsInstanceValid(overlay))
        {
            overlay!.CallDeferred(Node.MethodName.QueueFree);
        }
    }
}
