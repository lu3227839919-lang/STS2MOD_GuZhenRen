using System.Threading;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 标记当前异步调用链正在执行永久牌组合练。
///
/// 合练材料从牌组移除时，本命蛊等系统可以据此忽略普通删牌惩罚。
/// </summary>
internal static class GuHeLianScope
{
    private static readonly AsyncLocal<int> Depth = new();

    internal static bool IsActive =>
        Depth.Value > 0;

    internal static IDisposable Enter()
    {
        Depth.Value++;
        return new ScopeToken();
    }

    private sealed class ScopeToken : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (Depth.Value > 0)
            {
                Depth.Value--;
            }
        }
    }
}
