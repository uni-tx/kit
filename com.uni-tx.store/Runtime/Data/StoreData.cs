using System;
using System.Collections.Generic;
using System.Text;
using UniTx.Content;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// One store's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content unit the service selects: a shop holding the offers currently on sale.
    /// A store replacement (a new sale, an event shop) re-points the entity's content key
    /// without moving the save, and the old offers' claim history survives under the new
    /// store because it is keyed by offer id.
    /// </para>
    /// <para>
    /// Offers keep the order they are authored in — the researched shop pattern is a
    /// single scrollable feed with high-conversion content first and the free offer last —
    /// so a designer controls the layout by arranging the list in the JSON.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class StoreData : IData
    {
        [Tooltip("Unique store id. Part of the recorded claim key, so changing it on a " +
                 "live store starts every player's claim history over.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing store name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("The offers of the shop, in display order. Daily deals first, the free " +
                 "offer last — the pattern that maximizes scroll-through.")]
        [SerializeField] private List<StoreOfferData> _offers = new();

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing store name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the offers in display order.
        /// </summary>
        public IReadOnlyList<StoreOfferData> Offers => _offers;

        /// <summary>
        /// Returns the offer with the given id, or null.
        /// </summary>
        /// <param name="offerId">The offer id.</param>
        public StoreOfferData GetOffer(string offerId)
        {
            foreach (var offer in _offers)
            {
                if (offer != null &&
                    string.Equals(offer.Id, offerId, StringComparison.Ordinal))
                {
                    return offer;
                }
            }

            return null;
        }

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the store is sound.</returns>
        /// <remarks>
        /// Content arrives as JSON a designer edits, so it is validated rather than trusted.
        /// These are the failures that would otherwise show up as an offer nobody can claim
        /// or a reward that never arrives.
        /// </remarks>
        public string DescribeProblems()
        {
            var problems = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_id)) Append(problems, "store id is blank");

            if (_offers.Count == 0)
            {
                Append(problems, "no offers are defined");
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var offer in _offers)
                {
                    if (offer == null)
                    {
                        Append(problems, "an offer entry is null");
                        continue;
                    }

                    var offerProblems = offer.DescribeProblems();

                    if (!string.IsNullOrEmpty(offerProblems))
                    {
                        Append(problems, offerProblems);
                    }

                    if (!string.IsNullOrWhiteSpace(offer.Id) && !seen.Add(offer.Id))
                    {
                        Append(problems, $"offer id '{offer.Id}' is duplicated");
                    }
                }
            }

            return problems.ToString();
        }

        private static void Append(StringBuilder problems, string problem)
        {
            if (problems.Length > 0) problems.Append("; ");
            problems.Append(problem);
        }
    }
}
