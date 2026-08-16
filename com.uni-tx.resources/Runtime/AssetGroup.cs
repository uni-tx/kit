using System;
using System.Collections;
using System.Collections.Generic;

namespace UniTx.Resources
{
    /// <summary>
    /// A disposable set of assets loaded together by label.
    /// </summary>
    /// <typeparam name="TObject">The asset type contained in the group.</typeparam>
    /// <remarks>
    /// Composes a list rather than inheriting one, so callers cannot <c>Add</c> or
    /// <c>Remove</c> entries and desynchronize the group from the Addressables handle that
    /// actually owns the memory.
    /// </remarks>
    public sealed class AssetGroup<TObject> : IReadOnlyList<TObject>, IDisposable
        where TObject : UnityEngine.Object
    {
        private readonly List<TObject> _assets;

        /// <summary>
        /// Gets the identifier the loading strategy uses to track this group's handle.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets how many assets the group contains.
        /// </summary>
        public int Count => _assets.Count;

        /// <summary>
        /// Indicates whether the group has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets the asset at the given index.
        /// </summary>
        /// <param name="index">Zero-based index into the group.</param>
        public TObject this[int index] => _assets[index];

        internal AssetGroup(IEnumerable<TObject> assets)
        {
            _assets = new List<TObject>(assets ?? throw new ArgumentNullException(nameof(assets)));
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Releases the group's references. The underlying handle is released by the strategy.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            _assets.Clear();
            IsDisposed = true;
        }

        /// <summary>
        /// Returns an enumerator over the group's assets.
        /// </summary>
        public List<TObject>.Enumerator GetEnumerator() => _assets.GetEnumerator();

        IEnumerator<TObject> IEnumerable<TObject>.GetEnumerator() => _assets.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _assets.GetEnumerator();
    }
}
