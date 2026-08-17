using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// A granter that logs what it would deliver and always succeeds.
    /// </summary>
    /// <remarks>
    /// The fallback when no granter is installed, so the whole flow is playable and testable
    /// before an economy exists. Swap in a real granter before ship; a warning is logged
    /// once so this cannot go unnoticed in a build.
    /// </remarks>
    public sealed class LoggingQuestGranter : IQuestRewardGranter
    {
        private static readonly LoggingQuestGranter Shared = new();

        /// <summary>
        /// Gets the shared logging granter.
        /// </summary>
        public static LoggingQuestGranter Instance => Shared;

        private bool _hasWarned;

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(QuestData quest, QuestRewardData reward,
            QuestRef reference, string grantId, CancellationToken cToken = default)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    "No IQuestRewardGranter is installed, so quest rewards are recorded as " +
                    "claimed but never delivered. Install a granter before shipping.", this);
            }

            UniStatics.LogInfo(
                $"Granting {reward.Amount}x {reward.ItemId} ({reward.Kind}) from quest " +
                $"'{reference.QuestId}' of '{reference.SetId}'.", this);

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
