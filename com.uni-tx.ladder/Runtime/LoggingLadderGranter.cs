using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// A granter that logs what it would deliver and always succeeds.
    /// </summary>
    /// <remarks>
    /// The fallback when no granter is installed, so the whole flow is playable and testable
    /// before an economy exists. Swap in a real granter before ship; a warning is logged
    /// once so this cannot go unnoticed in a build.
    /// </remarks>
    public sealed class LoggingLadderGranter : ILadderRewardGranter
    {
        private static readonly LoggingLadderGranter Shared = new();

        /// <summary>
        /// Gets the shared logging granter.
        /// </summary>
        public static LoggingLadderGranter Instance => Shared;

        private bool _hasWarned;

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(LadderRungData rung, LadderRewardData reward,
            LadderRungRef reference, string grantId, CancellationToken cToken = default)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    "No ILadderRewardGranter is installed, so ladder rewards are recorded as " +
                    "claimed but never delivered. Install a granter before shipping.", this);
            }

            UniStatics.LogInfo(
                $"Granting {reward.Amount}x {reward.ItemId} ({reward.Kind}) from rung " +
                $"'{reference.RungId}' of ladder '{reference.LadderId}'.", this);

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
