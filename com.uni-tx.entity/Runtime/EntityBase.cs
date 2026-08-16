using UniTx.Content;
using UniTx.IoC;
using UniTx.Serialization;

namespace UniTx.Entity
{
    /// <summary>
    /// Base entity implementation backed by content data and persisted saved data.
    /// </summary>
    public abstract class EntityBase<TData, TSavedData> : IEntity
        where TData : class, IEntityData
        where TSavedData : class, ISavedData, new()
    {
        /// <summary>
        /// The serialisation service used to persist the entity.
        /// </summary>
        protected ISerialisationService _serialisationService;

        /// <summary>
        /// The content service used to resolve the entity's data.
        /// </summary>
        protected IContentService _contentService;

        /// <inheritdoc />
        public TData Data { get; private set; }

        /// <inheritdoc />
        public TSavedData SavedData { get; private set; }

        /// <inheritdoc />
        public string Id { get; private set; }

        /// <summary>
        /// Creates the entity with the given id.
        /// </summary>
        /// <param name="id">The unique entity id.</param>
        protected EntityBase(string id) => Id = id;

        /// <summary>
        /// Called after dependency injection to allow derived entities to resolve extra services.
        /// </summary>
        /// <param name="resolver">The resolver to use.</param>
        protected abstract void OnInject(IResolver resolver);

        /// <summary>
        /// Called once after content and saved data are loaded.
        /// </summary>
        protected abstract void OnInit();

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
        public void Initialize()
        {
            Data = _contentService.GetData<TData>(Id);
            SavedData = _serialisationService.Load<TSavedData>(Id);
            OnInit();
        }

        /// <inheritdoc />
        public void Reset()
        {
            OnReset();
            Data = null;
            SavedData = null;
        }

        /// <inheritdoc />
        public void Save() => _serialisationService.Save(SavedData);
    }
}
