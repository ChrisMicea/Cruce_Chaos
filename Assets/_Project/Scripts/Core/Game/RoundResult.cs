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
    }
}
