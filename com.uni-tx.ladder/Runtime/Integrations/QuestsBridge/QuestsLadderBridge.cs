using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Events;
using UniTx.Quests;

namespace UniTx.Ladder.Integrations
{
    /// <summary>
    /// Turns claimed quests into ladder steps — the climb the user asked for: complete a
    /// task or objective, climb the ladder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Listens to <see cref="QuestClaimed"/> rather than sitting on the claim call path, so
    /// the ladder advances exactly when the player has something in hand — a claim is only
    /// raised after delivery succeeds. Every reward the player already has, the ladder
    /// counts; a quest that failed delivery simply does not climb.
    /// </para>
    /// <para>
    /// Each claimed quest adds one step by default. Games where quests weigh differently —
    /// a weekly quest worth more than a daily one — can bind a <see cref="Func{TResult}"/>
    /// that maps a quest to its steps.
    /// </para>
    /// </remarks>
    public sealed class QuestsLadderBridge : IDisposable
    {
        private readonly ILadderService _service;
        private readonly Func<QuestClaimed, int> _stepsOf;

        private bool _isDisposed;

        /// <summary>
        /// Starts converting claimed quests into ladder steps.
        /// </summary>
        /// <param name="service">The ladder to climb.</param>
        /// <param name="stepsOf">
        /// Maps a claimed quest to the steps it adds. Defaults to one step per quest.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public QuestsLadderBridge(ILadderService service,
            Func<QuestClaimed, int> stepsOf = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _stepsOf = stepsOf ?? (_ => 1);

            UniEvents.Subscribe<QuestClaimed>(OnQuestClaimed);
        }

        /// <summary>
        /// Stops converting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<QuestClaimed>(OnQuestClaimed);
        }

        private void OnQuestClaimed(QuestClaimed @event)
        {
            var steps = _stepsOf(@event);

            if (steps <= 0) return;

            ReportAsync(steps).Forget();
        }

        private async UniTaskVoid ReportAsync(int steps)
        {
            try
            {
                // Fire-and-forget by design: the bridge owns the report and no caller can
                // cancel it — the ladder keeps climbing even if a screen is gone, because
                // the reward is already the player's.
                await _service.ReportStepsAsync(steps, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Not expected with CancellationToken.None; kept so a future cancellation
                // source cannot take a claim down with it.
            }
            catch (Exception exception)
            {
                // A step report must never take a claim down with it — the reward is already
                // in the player's hands, and the climb will catch up on the next report.
                UniStatics.LogException(exception, this);
            }
        }
    }
}
