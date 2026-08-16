using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// A granter that logs what it would deliver and always succeeds.
    /// </summary>
    /// <remarks>
    /// The default, so the whole flow — earning, unlocking, claiming, rolling over — is
    /// playable and testable before an economy exists. Swap in a real granter before ship; a
    /// warning is logged once so this cannot go unnoticed in a build.
    /// </remarks>
    public sealed class LoggingRewardGranter : ISeasonPassRewardGranter
    {
        private bool _hasWarned;

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
            CancellationToken cToken = default)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    "No ISeasonPassRewardGranter was registered, so season pass rewards are " +
                    "recorded as claimed but never delivered. Register one before shipping.", this);
            }

            UniStatics.LogInfo(
                $"Granting {reward.Amount}x {reward.ItemId} ({reward.Kind}) from {reference}.", this);

            return UniTask.FromResult(true);
        }
    }
}
