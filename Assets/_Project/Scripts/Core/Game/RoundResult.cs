using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// The outcome of one completed round: the per-team captured-point tallies,
    /// plus the bidding/trump facts needed later for score conversion.
    ///
    /// The bid fields are RECORDED now but not yet consumed — score conversion
    /// (contract made/failed, 33×S thresholds) is a later milestone. Carrying
    /// them on the result means that milestone needs no re-plumbing: the data it
    /// requires is already here.
    /// </summary>
    public sealed class RoundResult
    {
        /// <summary>Per-team captured-point tallies for the round.</summary>
        public ScoringUnit[] ScoringUnits { get; }

        /// <summary>
        /// The winning bid value (the contract the bid winner must make), or
        /// null if — defensively — no contract was established. With the
        /// forced-1 rule this is normally never null.
        /// </summary>
        public int? WinningBid { get; }

        /// <summary>Seat index of the bid winner (the opening leader), or -1 if none.</summary>
        public int BidWinnerSeat { get; }

        /// <summary>The trump suit that was established for the round, or null if none.</summary>
        public Suit? Trump { get; }

        public RoundResult(ScoringUnit[] scoringUnits, int? winningBid, int bidWinnerSeat, Suit? trump)
        {
            ScoringUnits = scoringUnits;
            WinningBid = winningBid;
            BidWinnerSeat = bidWinnerSeat;
            Trump = trump;
        }

        /// <summary>
        /// Finds the scoring unit a participant belongs to — the UI's
        /// participant → team lookup. In individual modes this is the unit that
        /// wraps just that participant; in 2v2 it is the shared team unit, whose
        /// <see cref="ScoringUnit.CapturedPoints"/> is the team total to display.
        /// Returns null if the participant is not part of this result.
        ///
        /// Kept here (not as a back-reference on RoundParticipant) so the
        /// participant and unit stay decoupled: whoever holds all the units does
        /// the lookup. It is a linear scan over at most 4 units, which is
        /// trivial for a per-round UI query.
        /// </summary>
        public ScoringUnit UnitOf(RoundParticipant participant)
        {
            foreach (ScoringUnit unit in ScoringUnits)
            {
                if (unit.Members.Contains(participant))
                {
                    return unit;
                }
            }

            return null;
        }
    }
}
