using System;
using System.Runtime.CompilerServices;

namespace UniTx.Core
{
    /// <summary>
    /// Extension methods for <see cref="Func{TResult}"/>
    /// </summary>
    public static class FuncExtensions
    {
        /// <summary>
        /// Safely invokes the function.
        /// </summary>
        /// <param name="source">The function to invoke.</param>
        /// <param name="value">The parameter to pass to the function.</param>
        /// <typeparam name="TParam">The type of the parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value.</typeparam>
        /// <returns>The result of the function, or default if the function is null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult SafeInvoke<TParam, TResult>(this Func<TParam, TResult> source, TParam value)
        {
            return source is null ? default : source.Invoke(value);
        }
    }
}