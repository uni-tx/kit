using System;
using UniTx.Entity;
using UnityEngine;

namespace UniTx.Currency
{
    /// <summary>
    /// One currency's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static because it is the same for every player: tuning values, the icon, and the
    /// cap all live here and ship with the build. The per-player balance lives in
    /// <see cref="CurrencySavedData"/>. Keeping the two apart is what lets an economy
    /// patch ship without rewriting anyone's wallet.
    /// </para>
    /// <para>
    /// Being an <see cref="IEntityData"/> is what makes a currency a first-class entity:
    /// <see cref="CreateEntity"/> builds a <see cref="Currency"/> that the entity service
    /// registers automatically when content is loaded.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class CurrencyData : IEntityData
    {
        [Tooltip("Unique currency id. Referenced by costs, rewards and the wallet.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("Addressables address of the currency icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("Soft currency earned through play, or hard currency bought or granted.")]
        [SerializeField] private CurrencyKind _kind = CurrencyKind.Soft;

        [Tooltip("Balance a fresh player starts with. 0 means none.")]
        [SerializeField, Min(0)] private int _initialBalance;

        [Tooltip("Highest balance a player may hold. 0 means uncapped.")]
        [SerializeField, Min(0)] private int _maxBalance;

        /// <inheritdoc />
        public string Id => _id;

        /// <inheritdoc />
        public string Name => _displayName;

        /// <summary>
        /// Gets the Addressables address of the currency icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Gets whether this is soft or hard currency.
        /// </summary>
        public CurrencyKind Kind => _kind;

        /// <summary>
        /// Indicates whether this is soft (earned through play) currency.
        /// </summary>
        public bool IsSoftCurrency => _kind == CurrencyKind.Soft;

        /// <summary>
        /// Gets the starting balance for a fresh player.
        /// </summary>
        public int InitialBalance => _initialBalance;

        /// <summary>
        /// Gets the maximum balance a player may hold, or zero when uncapped.
        /// </summary>
        public int MaxBalance => _maxBalance;

        /// <inheritdoc />
        public IEntity CreateEntity() => new Currency(Id);
    }
}
