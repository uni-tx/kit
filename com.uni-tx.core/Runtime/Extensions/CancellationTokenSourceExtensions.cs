using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UniTx.Core
{
    /// <summary>
    /// Extension methods for <see cref="CancellationTokenSource"/>.
    /// </summary>
    public static class CancellationTokenSourceExtensions
    {
        /// <summary>
        /// Cancels the source and disposes it, tolerating null and already-disposed sources.
        /// </summary>
        /// <param name="source">The token source to cancel and dispose. May be null.</param>
        /// <remarks>
        /// Calling <see cref="CancellationTokenSource.Cancel()"/> on a disposed source throws
        /// <see cref="ObjectDisposedException"/>. Teardown paths — <c>OnDestroy</c>, a service
        /// <c>Reset</c>, a retry loop — routinely run twice, so this swallows that one case
        /// rather than forcing every caller to track disposal itself.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SafeCancelAndDispose(this CancellationTokenSource source)
        {
            if (source == null) return;

            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                source.Dispose();
            }
        }
    }
}
