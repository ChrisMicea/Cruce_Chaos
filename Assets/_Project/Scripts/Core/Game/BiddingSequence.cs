using System;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// The single source of truth for Cruce's bidding RULES, independent of how
    /// a bid is actually obtained from a seat.
    ///
    /// Cruce bidding is poker-style: starting from a configured opener, each
    /// seat acts EXACTLY ONCE in clockwise order. A seat either passes or places
    /// one numeric bid — there is no re-raising once an opponent outbids it. The
    /// highest bid wins. If every seat passes, the FORCED-1 rule applies: the
    /// last seat to pass is forced to a contract of 1.
    ///
    /// This type does not know whether a decision comes from a synchronous AI
    /// call or an asynchronous UI coroutine. The caller drives it one seat at a
    /// time:
    ///
    ///   while (seq.TryGetNextSeat(out int seat, out BiddingContext ctx))
    ///   {
    ///       Bid bid = /* obtain bid for `seat` using `ctx`, however the caller likes */;
    ///       seq.Record(seat, bid);
    ///   }
    ///   BiddingOutcome outcome = seq.Resolve();
    ///
    /// <see cref="RoundEngine"/> drives it synchronously; the Unity
    /// <c>GameManager</c> drives the same object from a coroutine, yielding
    /// between <see cref="TryGetNextSeat"/> and <see cref="Record"/> so it can
    /// show panels, pass the device, and wait for input. Both share this one
    /// rule implementation, so the headless and on-screen bidding can never
    /// diverge (the reason an earlier auction-style bug only got half-fixed).
    /// </summary>
    public sealed class BiddingSequence
    {
        private readonly int _playerCount;
        private readonly int _maxBid;
        private readonly int _startingSeat;

        private int _highBid;          // 0 == no numeric bid yet
        private int _highSeat = -1;
        private int _lastPasserSeat;

        private int _index;            // how many seats have acted (0.._playerCount)
        private int _pendingSeat = -1; // seat handed out by TryGetNextSeat, awaiting Record

        /// <param name="playerCount">Number of seats; must be positive.</param>
        /// <param name="maxBid">The maximum legal numeric bid (from MatchConfig).</param>
        /// <param name="startingSeat">
        /// The seat that bids first; normalised into range. Callers typically
        /// pass <see cref="MatchConfig.StartingBidderSeat"/> for the round.
        /// </param>
        public BiddingSequence(int playerCount, int maxBid, int startingSeat)
        {
            if (playerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "playerCount must be positive.");
            }

            _playerCount = playerCount;
            _maxBid = maxBid;
            _startingSeat = ((startingSeat % playerCount) + playerCount) % playerCount;
            _lastPasserSeat = _startingSeat;
        }

        /// <summary>
        /// True once every seat has acted (so <see cref="TryGetNextSeat"/> will
        /// return false and <see cref="Resolve"/> is ready).
        /// </summary>
        public bool IsComplete => _index >= _playerCount;

        /// <summary>
        /// Hand out the next seat to ask and the <see cref="BiddingContext"/> it
        /// should see (current high bid so far + the match's max bid). Returns
        /// false when every seat has already acted. A seat handed out here must
        /// be answered with <see cref="Record"/> before the next call.
        /// </summary>
        public bool TryGetNextSeat(out int seat, out BiddingContext context)
        {
            if (_pendingSeat >= 0)
            {
                throw new InvalidOperationException(
                    $"Seat {_pendingSeat} was handed out but its bid was not recorded yet.");
            }

            if (IsComplete)
            {
                seat = -1;
                context = null;
                return false;
            }

            seat = (_startingSeat + _index) % _playerCount;
            context = new BiddingContext(_highBid > 0 ? _highBid : (int?)null, _maxBid);
            _pendingSeat = seat;
            return true;
        }

        /// <summary>
        /// Record the bid the given seat made. The seat must be the one most
        /// recently returned by <see cref="TryGetNextSeat"/>. A numeric bid that
        /// is a pass, below the current minimum, or above the max is treated as a
        /// pass (same rule the engine always applied). Advances to the next seat.
        /// </summary>
        public void Record(int seat, Bid bid)
        {
            if (_pendingSeat < 0)
            {
                throw new InvalidOperationException("Record called before TryGetNextSeat.");
            }

            if (seat != _pendingSeat)
            {
                throw new ArgumentException(
                    $"Recorded seat {seat} does not match the seat handed out ({_pendingSeat}).",
                    nameof(seat));
            }

            int minLegal = (_highBid > 0 ? _highBid : 0) + 1;
            bool valid = !bid.IsPass && bid.Value >= minLegal && bid.Value <= _maxBid;

            if (valid)
            {
                _highBid = bid.Value;
                _highSeat = seat;
            }
            else
            {
                _lastPasserSeat = seat;
            }

            _pendingSeat = -1;
            _index++;
        }

        /// <summary>
        /// Resolve the winner after every seat has acted. If someone bid, that is
        /// the winner at their bid; if everyone passed, the last seat to pass is
        /// forced to a contract of 1. Throws if called before the sequence is
        /// complete.
        /// </summary>
        public BiddingOutcome Resolve()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Resolve called before every seat acted.");
            }

            if (_highSeat >= 0)
            {
                return new BiddingOutcome(_highSeat, _highBid, forcedToOne: false);
            }

            // Everyone passed — the LAST seat to pass is forced to 1.
            return new BiddingOutcome(_lastPasserSeat, 1, forcedToOne: true);
        }
    }

    /// <summary>
    /// The result of a completed <see cref="BiddingSequence"/>: which seat won,
    /// at what contract value, and whether that contract was the forced-1
    /// fallback (everyone passed) rather than a bid the seat actually placed.
    /// </summary>
    public readonly struct BiddingOutcome
    {
        /// <summary>Seat that won the bid (and becomes the opening leader).</summary>
        public int WinnerSeat { get; }

        /// <summary>The winning contract value.</summary>
        public int WinningBid { get; }

        /// <summary>
        /// True when every seat passed and the winner was forced to a contract
        /// of 1 rather than placing a bid.
        /// </summary>
        public bool ForcedToOne { get; }

        public BiddingOutcome(int winnerSeat, int winningBid, bool forcedToOne)
        {
            WinnerSeat = winnerSeat;
            WinningBid = winningBid;
            ForcedToOne = forcedToOne;
        }
    }
}
