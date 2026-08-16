namespace UniTx.Entity
{
    /// <summary>
    /// Loads and unloads all registered entity instances.
    /// </summary>
    public interface IEntityLoader
    {
        /// <summary>
        /// Creates and registers all entities described by the loaded content data.
        /// </summary>
        void LoadEntities();

        /// <summary>
        /// Resets and unregisters all currently loaded entities.
        /// </summary>
        void UnloadEntities();
    }
}
