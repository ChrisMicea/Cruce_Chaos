using System.Collections.Generic;

namespace CruceChaos.Core.Game.Scoring
{
    /// <summary>
    /// Converts a completed round's captured card-points into match-score
    /// deltas per team. Kept behind an interface so the exact conversion is a
    /// pluggable policy (power-ups could later change scoring) and the match
    /// loop stays decoupled from the formula.
    /// </summary>
    public interface IRoundScorer
    {
        /// <summary>
        /// Compute the match-score delta for each team from
        /// <paramref name="result"/>. The returned map is keyed by team index
        /// (matching <see cref="ScoringUnit.TeamIndex"/>); every team present in
        /// the result appears in the map (delta may be 0 or negative).
        /// </summary>
        IReadOnlyDictionary<int, int> ScoreRound(RoundResult result);
    }
}
