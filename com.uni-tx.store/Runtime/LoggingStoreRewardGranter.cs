using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// A granter that logs what it would deliver and always succeeds.
    /// </summary>
    /// <remarks>
    /// The fallback when no granter is installed, so the whole shop is playable and testable
    /// before an economy exists. Swap in a real granter before ship; a warning is logged
    /// once so this cannot go unnoticed in a build.
    /// </remarks>
    public sealed class LoggingStoreRewardGranter : IStoreRewardGranter
    {
        private static readonly LoggingStoreRewardGranter Shared = new();

        /// <summary>
        /// Gets the shared logging granter.
        /// </summary>
        public static LoggingStoreRewardGranter Instance => Shared;

        private bool _hasWarned;

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(StoreOfferData offer, StoreRewardData reward,
            StoreOfferRef reference, string grantId, CancellationToken cToken = default)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    "No IStoreRewardGranter is installed, so store rewards are recorded as " +
                    "claimed but never delivered. Install a granter before shipping.", this);
            }

            UniStatics.LogInfo(
                $"Granting {reward.Amount}x {reward.ItemId} ({reward.Kind}) from offer " +
                $"'{reference.OfferId}' of store '{reference.StoreId}'.", this);

            return UniTask.FromResult(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled, in which case the shared instance survives
            // entering play mode and the warn-once flag with it — the "no granter installed"
            // warning would then never fire again after the first session.
            Shared._hasWarned = false;
        }
    }
}
