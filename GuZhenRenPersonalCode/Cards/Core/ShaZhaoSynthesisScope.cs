using System.Threading;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 标记当前异步调用链是否正在执行杀招合成。
///
/// 合成材料被移除或消耗时，其他系统可以通过 IsActive
/// 区分“杀招合成消耗”和普通永久删牌行为。
/// </summary>
internal static class ShaZhaoSynthesisScope
{
    private static readonly AsyncLocal<int> Depth = new();

    /// <summary>
    /// 当前异步调用链是否位于杀招合成作用域中。
    /// </summary>
    internal static bool IsActive =>
        Depth.Value > 0;

    /// <summary>
    /// 进入杀招合成作用域。
    /// 返回值必须通过 using 释放。
    /// </summary>
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
