using Godot;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Cards;

/// <summary>
/// 只挂在蛊手牌 NModExtraHand 下的原始输入节点。_Input 早于 Control 的
/// GuiInput 分发运行，因此不会被 Holder/Hitbox 的既有点击逻辑提前吞掉。
/// </summary>
internal sealed partial class TiaoGuInputBridge : Node
{
    private NModExtraHand? _extraHand;

    internal void Bind(NModExtraHand extraHand)
    {
        _extraHand = extraHand;
    }

    public override void _Ready()
    {
        SetProcessInput(true);
    }

    public override void _Input(InputEvent inputEvent)
    {
        NModExtraHand? extraHand = _extraHand;
        if (extraHand == null ||
            !GodotObject.IsInstanceValid(extraHand))
        {
            return;
        }

        TiaoGuSystem.OnGuHandRawInput(extraHand, inputEvent);
    }

    public override void _ExitTree()
    {
        SetProcessInput(false);
        _extraHand = null;
    }
}
