using Godot;

/// <summary>
/// 蛊真人能量表盘场景中 OrbPreview.cs 对应的 Godot 脚本类型。
///
/// 旧构建把脚本资源打进了 PCK，却没有把同名 C# 类型编译进 DLL，导致场景实例化时
/// 报 “associated class could not be found”。该节点仅承担场景预览/装饰容器职责，
/// 不需要额外运行时逻辑；保持为 Control 可兼容 TextureRect 等 UI 子类节点。
/// </summary>
public partial class OrbPreview : Control
{
}
