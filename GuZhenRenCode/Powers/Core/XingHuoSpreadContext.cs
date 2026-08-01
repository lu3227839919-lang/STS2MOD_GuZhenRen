using System;
using System.Threading;

namespace GuZhenRen.Powers;

/// <summary>
/// 星火燎原传播期间的异步上下文。
///
/// 用 AsyncLocal 代替尖塔1的全局静态 bool，
/// 避免多人和嵌套异步结算互相干扰。
/// </summary>
internal static class XingHuoSpreadContext
{
    private static readonly AsyncLocal<int>
        Depth = new();

    internal static bool IsActive =>
        Depth.Value > 0;

    internal static IDisposable Enter()
    {
        Depth.Value++;

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

            Depth.Value =
                Math.Max(
                    0,
                    Depth.Value - 1
                );
        }
    }
}
