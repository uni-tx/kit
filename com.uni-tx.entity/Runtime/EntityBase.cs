using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.IoC;
using UniTx.Serialization;

namespace UniTx.Entity
{
    /// <summary>
    /// Base entity implementation backed by content data and persisted saved data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <typeparamref name="TData"/> is the static half, loaded from the content service by
    /// <see cref="DataId"/>; <typeparamref name="TSavedData"/> is the per-player half, loaded
    /// from the serialisation service by <see cref="Id"/>. The two keys are deliberately
    /// independent — an entity whose content id changes at runtime (a season rollover) keeps
    /// its save under the same key forever.
    /// </para>
    /// <para>
    /// Subclasses implement <see cref="OnInject"/>, <see cref="OnInitAsync"/> and
    /// <see cref="OnReset"/>. The persistence seams (<see cref="LoadSavedDataAsync"/> and
    /// <see cref="SaveSavedDataAsync"/>) default to the serialisation service and can be
    /// overridden when the entity must store through something else — a backend with server
    /// authority, for example.
    /// </para>
    /// </remarks>
    public abstract class EntityBase<TData, TSavedData> : IEntity
        where TData : class, IData
        where TSavedData : class, ISavedData, new()
    {
        /// <summary>
        /// The serialisation service used to persist the entity.
        /// </summary>
        protected ISerialisationService _serialisationService;

        /// <summary>
        /// The content service used to resolve the entity's static data.
        /// </summary>
        protected IContentService _contentService;

        /// <summary>
        /// Gets the static content data, or null before initialization or when no content
        /// exists for the current <see cref="DataId"/>.
        /// </summary>
        public TData Data { get; private set; }

        /// <summary>
        /// Gets the loaded saved data, or null before initialization.
        /// </summary>
        public TSavedData SavedData { get; private set; }

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string DataId { get; private set; }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <summary>
        /// Creates an entity whose content is keyed by the same id as its save.
        /// </summary>
        /// <param name="id">The stable entity id.</param>
        protected EntityBase(string id)
            : this(id, id)
        {
        }

        /// <summary>
        /// Creates an entity with separate content and save keys.
        /// </summary>
        /// <param name="id">The stable entity id, also the save key.</param>
        /// <param name="dataId">The content key. Defaults to <paramref name="id"/>.</param>
        protected EntityBase(string id, string dataId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DataId = string.IsNullOrWhiteSpace(dataId) ? id : dataId;
        }

        /// <summary>
        /// Called after dependency injection to allow derived entities to resolve extra services.
        /// </summary>
        /// <param name="resolver">The resolver to use.</param>
        protected abstract void OnInject(IResolver resolver);

        /// <summary>
        /// Called once after content and saved data are loaded.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        protected abstract UniTask OnInitAsync(CancellationToken cToken);

        /// <summary>
        /// Called when the entity is reset or unloaded.
        /// </summary>
        protected abstract void OnReset();

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _serialisationService = resolver.Resolve<ISerialisationService>();
            _contentService = resolver.Resolve<IContentService>();
            OnInject(resolver);
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            Data = LoadData();
            SavedData = await LoadSavedDataAsync(cToken);
            SavedData.Id ??= Id;

            await OnInitAsync(cToken);

            // Readiness is claimed last: an OnInitAsync that cancels or throws leaves the
            // entity half-built, and a half-built entity that reports IsReady is worse
            // than one that reports nothing.
            IsReady = true;
        }

        /// <summary>
        /// Re-points the static content this entity reads.
        /// </summary>
        /// <param name="dataId">The new content key, or null to fall back to <see cref="Id"/>.</param>
        /// <remarks>
        /// Content that is selected at runtime — a season picked by date — re-points here and
        /// then calls <see cref="ReloadData"/>. The saved data is untouched.
        /// </remarks>
        public void SetDataId(string dataId) => DataId = string.IsNullOrWhiteSpace(dataId) ? Id : dataId;

        /// <summary>
        /// Re-fetches the static content for the current <see cref="DataId"/>.
        /// </summary>
        /// <returns>The freshly loaded data, or null when no content exists.</returns>
        public TData ReloadData()
        {
            Data = LoadData();
            return Data;
        }

        /// <summary>
        /// Resolves the static content for the current <see cref="DataId"/>.
        /// </summary>
        /// <returns>The data, or null when a derived entity tolerates missing content.</returns>
        protected virtual TData LoadData() => _contentService.GetData<TData>(DataId);

        /// <summary>
        /// Loads the entity's saved data.
        /// </summary>
        /// <param name="cToken">Token to cancel the read.</param>
        /// <remarks>
        /// The persistence seam. Defaults to the serialisation service; override to store
        /// through a backend with server authority.
        /// </remarks>
        protected virtual UniTask<TSavedData> LoadSavedDataAsync(CancellationToken cToken)
        {
            cToken.ThrowIfCancellationRequested();

            return UniTask.FromResult(_serialisationService.Load<TSavedData>(Id));
        }

        /// <summary>
        /// Persists the entity's saved data.
        /// </summary>
        /// <param name="immediate">Write to disk now rather than at the next batch.</param>
        /// <param name="cToken">Token to cancel the write.</param>
        /// <remarks>
        /// The persistence seam. Defaults to the serialisation service; override to store
        /// through a backend with server authority.
        /// </remarks>
        protected virtual UniTask SaveSavedDataAsync(bool immediate, CancellationToken cToken)
        {
            cToken.ThrowIfCancellationRequested();

            _serialisationService.Save(SavedData);

            if (immediate) _serialisationService.Flush();

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void Save() => SaveAsync(false, default).Forget();

        /// <inheritdoc />
        public UniTask SaveAsync(bool immediate = false, CancellationToken cToken = default)
        {
            if (SavedData == null) return UniTask.CompletedTask;

            return SaveSavedDataAsync(immediate, cToken);
        }

        /// <inheritdoc />
        public void Reset()
        {
            OnReset();

            Data = null;
            SavedData = null;
            IsReady = false;
        }
    }
}
