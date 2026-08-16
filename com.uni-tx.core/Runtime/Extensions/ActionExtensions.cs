using System;
using System.Runtime.CompilerServices;

namespace UniTx.Core
{
    /// <summary>
    /// Extension methods for <see cref="Action"/>.
    /// </summary>
    /// <remarks>
    /// A C# event with no subscribers is null, so raising it needs a null check every time.
    /// These keep that check to one call rather than an <c>if</c> at every raise site.
    /// </remarks>
    public static class ActionExtensions
    {
        /// <summary>
        /// Invokes the action if it has subscribers.
        /// </summary>
        /// <param name="source">The action to invoke. May be null.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SafeInvoke(this Action source) => source?.Invoke();

        /// <summary>
        /// Invokes the action if it has subscribers.
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="source">The action to invoke. May be null.</param>
        /// <param name="value">The argument to pass.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SafeInvoke<T>(this Action<T> source, T value) => source?.Invoke(value);

        /// <summary>
        /// Invokes the action if it has subscribers.
        /// </summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <param name="source">The action to invoke. May be null.</param>
        /// <param name="first">The first argument to pass.</param>
        /// <param name="second">The second argument to pass.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SafeInvoke<T1, T2>(this Action<T1, T2> source, T1 first, T2 second)
            => source?.Invoke(first, second);
    }
}
