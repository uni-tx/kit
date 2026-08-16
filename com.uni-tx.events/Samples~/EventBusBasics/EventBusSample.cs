using UniTx.Events;
using UnityEngine;

namespace UniTx.Events.Samples
{
    /// <summary>
    /// Publishing and subscribing struct events, with priority ordering.
    /// </summary>
    /// <remarks>
    /// Drop on any GameObject and press Play. Watch the console for the dispatch order.
    /// </remarks>
    public sealed class EventBusSample : MonoBehaviour
    {
        // ------------------------------------------------------------------------------
        // Events are structs. That is what keeps dispatch allocation-free: a class event
        // would allocate on every raise, which matters when something fires every frame.
        // ------------------------------------------------------------------------------

        public readonly struct PlayerDamaged : IEvent
        {
            public readonly int Amount;
            public readonly int RemainingHealth;

            public PlayerDamaged(int amount, int remainingHealth)
            {
                Amount = amount;
                RemainingHealth = remainingHealth;
            }
        }

        public readonly struct CoinCollected : IEvent
        {
            public readonly int Value;

            public CoinCollected(int value) => Value = value;
        }

        private int _coins;

        private void Awake()
        {
            // UniTxStep does this during bootstrap. It is here so the sample runs standalone.
            if (!UniEvents.IsInitialized) UniEvents.Initialize();
        }

        private void OnEnable()
        {
            // Priority controls dispatch order: Highest runs first, Lowest last. Use it when
            // one listener must observe state before another mutates it — here, the audio
            // cue should fire before the death check can disable the object.
            UniEvents.Subscribe<PlayerDamaged>(PlayDamageSound, Priority.Highest);
            UniEvents.Subscribe<PlayerDamaged>(UpdateHealthBar);                  // Medium, the default
            UniEvents.Subscribe<PlayerDamaged>(CheckForDeath, Priority.Lowest);

            UniEvents.Subscribe<CoinCollected>(OnCoinCollected);
        }

        private void OnDisable()
        {
            // Always unsubscribe. The bus holds a strong reference to the delegate, and the
            // delegate holds this component — a missed unsubscribe keeps the whole object
            // alive across scene loads and the handler fires on a destroyed target.
            //
            // Unsubscribe is a no-op when the bus was already reset, so teardown ordering
            // during shutdown never throws.
            UniEvents.Unsubscribe<PlayerDamaged>(PlayDamageSound);
            UniEvents.Unsubscribe<PlayerDamaged>(UpdateHealthBar);
            UniEvents.Unsubscribe<PlayerDamaged>(CheckForDeath);
            UniEvents.Unsubscribe<CoinCollected>(OnCoinCollected);
        }

        private void Start()
        {
            // Raising is a plain call; every subscriber runs synchronously, in priority order.
            UniEvents.Raise(new PlayerDamaged(25, 75));
            UniEvents.Raise(new CoinCollected(10));
            UniEvents.Raise(new CoinCollected(5));

            // SubscriberCount is a cheap leak check: if this climbs across scene loads,
            // something is subscribing without unsubscribing.
            Debug.Log($"PlayerDamaged listeners: {UniEvents.SubscriberCount<PlayerDamaged>()}");

            UniEvents.Raise(new PlayerDamaged(80, 0));
        }

        private static void PlayDamageSound(PlayerDamaged e)
            => Debug.Log($"[Highest] hurt sound for {e.Amount} damage");

        private static void UpdateHealthBar(PlayerDamaged e)
            => Debug.Log($"[Medium]  health bar -> {e.RemainingHealth}");

        private void CheckForDeath(PlayerDamaged e)
        {
            if (e.RemainingHealth > 0) return;

            Debug.Log("[Lowest]  player died");

            // Unsubscribing from inside a handler is safe: the entry is tombstoned and the
            // list is compacted once dispatch unwinds, so the in-flight loop is undisturbed.
            UniEvents.Unsubscribe<CoinCollected>(OnCoinCollected);
        }

        private void OnCoinCollected(CoinCollected e)
        {
            _coins += e.Value;
            Debug.Log($"Coins: {_coins}");
        }
    }
}
