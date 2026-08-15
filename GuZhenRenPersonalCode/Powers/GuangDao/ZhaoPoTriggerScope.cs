using System.Threading;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 月芒蛊在六转前只能让第一段攻击触发照破；六转后才解锁
/// 每一段攻击都触发。该作用域仅覆盖当前异步伤害结算。
/// </summary>
internal static class ZhaoPoTriggerScope
{
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    internal static bool CanTrigger => SuppressionDepth.Value == 0;

    internal static IDisposable Suppress()
    {
        SuppressionDepth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SuppressionDepth.Value = Math.Max(
                0,
                SuppressionDepth.Value - 1
            );
        }
    }
}
