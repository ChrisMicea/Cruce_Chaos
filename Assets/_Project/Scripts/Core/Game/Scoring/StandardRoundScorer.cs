using System.Collections.Generic;

namespace CruceChaos.Core.Game.Scoring
{
    /// <summary>
    /// The standard Cruce round-scoring rule.
    ///
    /// Victory points a team earns in a round = capturedPoints / 33 (integer
    /// division: 0–32 → 0, 33–65 → 1, 66–98 → 2, ... 198+ → 6).
    ///
    ///   - NON-bidding teams always ADD their earned victory points.
    ///   - The BIDDING team: if its earned victory points are at least its bid
    ///     (contract made), it ADDS its earned points; otherwise (contract
    ///     missed) it SUBTRACTS the BID value (not the earned points).
    ///
    /// Announcements (40/20 marriages) are not yet factored in — that is step 6.
    /// </summary>
    public sealed class StandardRoundScorer : IRoundScorer
    {
        /// <summary>Card points needed per victory point.</summary>
        public const int PointsPerVictoryPoint = 33;

        public IReadOnlyDictionary<int, int> ScoreRound(RoundResult result)
        {
            var deltas = new Dictionary<int, int>();

            foreach (ScoringUnit unit in result.ScoringUnits)
            {
                int earned = unit.CapturedPoints / PointsPerVictoryPoint;
                bool isBiddingTeam = unit.TeamIndex == result.BidWinnerTeam;

                int delta;
                if (isBiddingTeam && result.WinningBid.HasValue)
                {
                    int bid = result.WinningBid.Value;
                    delta = earned >= bid ? earned : -bid;  // made: +earned; missed: -bid
                }
                else
                {
                    delta = earned;  // non-bidding side banks what it earned
                }

                deltas[unit.TeamIndex] = delta;
            }

            return deltas;
        }
    }
}
