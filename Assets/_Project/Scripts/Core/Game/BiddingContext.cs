namespace CruceChaos.Core.Game
{
    /// <summary>
    /// Read-only information a controller needs to decide its bid: the current
    /// highest bid so far (or null if everyone has passed up to now), and the
    /// maximum legal bid for this match.
    ///
    /// A legal numeric bid must be strictly greater than
    /// <see cref="CurrentHighBid"/> (or at least 1 if there is none) and at most
    /// <see cref="MaxBid"/>. Passing is always legal.
    /// </summary>
    public sealed class BiddingContext
    {
        /// <summary>The highest bid value placed so far this round, or null if none yet.</summary>
        public int? CurrentHighBid { get; }

        /// <summary>The maximum legal bid for this match (from MatchConfig).</summary>
        public int MaxBid { get; }

        public BiddingContext(int? currentHighBid, int maxBid)
        {
            CurrentHighBid = currentHighBid;
            MaxBid = maxBid;
        }

        /// <summary>The lowest value a new numeric bid may take (one above the high bid, or 1).</summary>
        public int MinLegalBid => (CurrentHighBid ?? 0) + 1;
    }
}
