using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// Interface for scene entities.
    /// </summary>
    public interface ISceneEntity
    {
        /// <summary>
        /// Gets the game object.
        /// </summary>
        GameObject GameObject { get; }

        /// <summary>
        /// Gets the transform.
        /// </summary>
        Transform Transform { get; }
    }
}