using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Content
{
    /// <summary>
    /// Loads and unloads content files by Addressables label.
    /// </summary>
    public interface IContentLoader
    {
        /// <summary>
        /// Asynchronously loads all content files associated with the given labels.
        /// </summary>
        /// <param name="labels">Addressables labels of the content files to load.</param>
        /// <param name="cToken">Token to cancel the load operation.</param>
        UniTask LoadContentAsync(IEnumerable<string> labels, CancellationToken cToken = default);

        /// <summary>
        /// Asynchronously unloads all content files associated with the given labels.
        /// </summary>
        /// <param name="labels">Addressables labels of the content files to unload.</param>
        /// <param name="cToken">Token to cancel the unload operation.</param>
        UniTask UnloadContentAsync(IEnumerable<string> labels, CancellationToken cToken = default);
    }
}
