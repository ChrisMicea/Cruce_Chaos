using System.Collections.Generic;

namespace CruceChaos.Core.Game.Scoring
{
    /// <summary>
    /// The standard Cruce round-scoring rule.
    ///
    /// Victory points a team earns in a round = totalPoints / 33 (integer
    /// division: 0–32 → 0, 33–65 → 1, ...), where totalPoints = captured card
    /// points PLUS marriage bonuses (so marriages help make the contract).
    ///
    ///   - NON-bidding teams always ADD their earned victory points.
    ///   - The BIDDING team: if its earned victory points are at least its bid
    ///     (contract made), it ADDS its earned points; otherwise (contract
    ///     missed) it SUBTRACTS the BID value (not the earned points).
    ///
    /// Marriage bonuses (40/20) are already included, because the scorer divides
    /// each unit's <see cref="ScoringUnit.TotalPoints"/> (captured + marriage).
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
                int earned = unit.TotalPoints / PointsPerVictoryPoint;
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
