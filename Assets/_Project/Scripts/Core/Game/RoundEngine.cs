using System;
using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Players;
using CruceChaos.Core.Rules;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// Drives one full round of an N-player Cruce game, configured by a
    /// <see cref="MatchConfig"/>:
    ///
    ///   1. Shuffle and deal CardsPerPlayer to each seat; resolve any leftover
    ///      per the config's <see cref="DealStrategy"/>.
    ///   2. Loop: each player plays one card into the trick; the trick winner
    ///      takes the card points and leads next; for DrawPilePerTrick, every
    ///      player then draws one (winner first) until the pile is empty.
    ///   3. Continue until all hands are empty.
    ///   4. Return a <see cref="RoundResult"/> (per-team tallies + bid + trump).
    ///
    /// Captured points are always awarded to the winning PARTICIPANT; a team's
    /// total is the computed sum over its members (see <see cref="ScoringUnit"/>),
    /// so individual and team play share one trick loop.
    ///
    /// Full flow: deal → bidding → the bid winner declares trump → (draft modes)
    /// reveal-and-draft → play all tricks. Returns a <see cref="RoundResult"/>.
    /// Converting a round's points into match score is the caller's job (see
    /// <c>MatchEngine</c> / <c>IRoundScorer</c>).
    /// </summary>
    public sealed class RoundEngine
    {
        private readonly MatchConfig _config;
        private readonly Deck _deck;
        private readonly RoundParticipant[] _participants;
        private readonly IPlayerController[] _controllers;
        private readonly ScoringUnit[] _scoringUnits;      // one per team
        private readonly ScoringUnit[] _unitOfSeat;        // seat -> its scoring unit
        private readonly LegalMoveEvaluator _legalMoves;
        private readonly Action<string> _log;

        private readonly int _playerCount;

        /// <summary>Seat index of whoever leads the next trick.</summary>
        private int _leaderIndex;

        /// <summary>Winning bid value and the seat that made it (set during bidding).</summary>
        private int? _winningBid;
        private int _bidWinnerSeat = -1;

        /// <summary>True until the first card of the round is played (for the opening-lead rule).</summary>
        private bool _firstPlayOfRound = true;

        /// <summary>
        /// The round's trump suit. Declared by the bid winner before play (see
        /// <see cref="DeclareTrump"/>); null until then, or if the mode has no
        /// trump.
        /// </summary>
        private Suit? _trump;

        /// <summary>Seat that bids first this round (rotated across rounds by the match loop).</summary>
        private readonly int _startingBidderSeat;

        /// <summary>
        /// Creates the engine for one round.
        /// </summary>
        /// <param name="players">The match-level players, one per seat; length must equal config.PlayerCount.</param>
        /// <param name="controllers">Decision makers, paired by seat index with players.</param>
        /// <param name="config">The match configuration (player count, deal sizes, teams, deal strategy, rule options).</param>
        /// <param name="ruleSet">The ordered legality rules for this round.</param>
        /// <param name="seed">Optional RNG seed for a reproducible round.</param>
        /// <param name="log">Optional play-by-play log callback; null to suppress.</param>
        /// <param name="startingBidderSeat">Seat that bids first (match loop rotates this across rounds).</param>
        public RoundEngine(
            Player[] players,
            IPlayerController[] controllers,
            MatchConfig config,
            RuleSet ruleSet,
            int? seed = null,
            Action<string> log = null,
            int startingBidderSeat = 0)
        {
            _config = config;
            _playerCount = config.PlayerCount;
            _startingBidderSeat = ((startingBidderSeat % config.PlayerCount) + config.PlayerCount) % config.PlayerCount;

            if (players.Length != _playerCount || controllers.Length != _playerCount)
            {
                throw new ArgumentException(
                    $"Expected {_playerCount} players and controllers, got {players.Length} / {controllers.Length}.");
            }

            _log = log ?? (_ => { });
            _legalMoves = new LegalMoveEvaluator(ruleSet, msg => _log(msg));
            _deck = new Deck(seed);
            _controllers = controllers;

            // One participant per seat.
            _participants = new RoundParticipant[_playerCount];
            for (int seat = 0; seat < _playerCount; seat++)
            {
                _participants[seat] = new RoundParticipant(players[seat]);
            }

            // Build scoring units from the team layout, and map each seat to its
            // unit. A unit per distinct team index; each participant joins the
            // unit for its seat's team. Individual play => one unit per seat.
            var unitByTeam = new Dictionary<int, ScoringUnit>();
            _unitOfSeat = new ScoringUnit[_playerCount];
            for (int seat = 0; seat < _playerCount; seat++)
            {
                int team = config.TeamOfSeat[seat];
                if (!unitByTeam.TryGetValue(team, out ScoringUnit unit))
                {
                    unit = new ScoringUnit(team);
                    unitByTeam[team] = unit;
                }

                unit.AddMember(_participants[seat]);
                _unitOfSeat[seat] = unit;
            }

            _scoringUnits = new List<ScoringUnit>(unitByTeam.Values).ToArray();
            _leaderIndex = 0;
        }

        /// <summary>
        /// Run the round to completion: deal, bid, play all tricks. Returns the
        /// <see cref="RoundResult"/> with per-team tallies and the recorded
        /// (not yet scored) winning bid and trump.
        /// </summary>
        public RoundResult Play()
        {
            Deal();
            RunBidding();
            DeclareTrump();
            RunDraftIfNeeded();
            PlayAllTricks();
            int bidWinnerTeam = _bidWinnerSeat >= 0 ? _config.TeamOfSeat[_bidWinnerSeat] : -1;
            return new RoundResult(_scoringUnits, _winningBid, _bidWinnerSeat, bidWinnerTeam, _trump);
        }

        /// <summary>Participants in seat order (read-only), e.g. for reporting.</summary>
        public IReadOnlyList<RoundParticipant> Participants => _participants;

        // --- dealing --------------------------------------------------------

        private void Deal()
        {
            _deck.Reset();
            _deck.Shuffle();

            for (int seat = 0; seat < _playerCount; seat++)
            {
                List<Card> dealt = _deck.Draw(_config.CardsPerPlayer);
                _participants[seat].Hand.AddRange(dealt);
                _log($"{_participants[seat].Player.Name} receives: {_participants[seat].Hand}");
            }

            // Leftover (if any) stays in the deck for now. DrawPilePerTrick draws
            // from it per trick; RevealAndDraft distributes it after trump is
            // declared (see RunDraftIfNeeded); DealAll leaves none; BlindSurprise
            // is not yet implemented.
            if (_config.DealStrategy == DealStrategy.BlindSurprise)
            {
                throw new NotImplementedException("BlindSurprise deal strategy is not yet implemented.");
            }

            _log(_config.DealStrategy == DealStrategy.DrawPilePerTrick
                ? $"Draw pile: {_deck.Count} cards remaining.\n"
                : $"Leftover in deck: {_deck.Count}.\n");
        }

        // --- trump declaration & draft -------------------------------------

        /// <summary>
        /// The bid winner declares the round's trump suit, after bidding and
        /// before the reveal/draft. Replaces the old "first card led sets trump".
        /// </summary>
        private void DeclareTrump()
        {
            RoundParticipant winner = _participants[_bidWinnerSeat];
            _trump = _controllers[_bidWinnerSeat].DeclareTrump(winner);
            _log($"  {winner.Player.Name} declares trump: {_trump}\n");
        }

        /// <summary>
        /// RevealAndDraft only: reveal the leftover cards publicly, then the bid
        /// winner picks one, then each other player picks one in clockwise order,
        /// until the leftover is exhausted (leftover count == player count, so
        /// each drafts exactly one). Trump is already declared, so controllers
        /// may draft trump-aware.
        /// </summary>
        private void RunDraftIfNeeded()
        {
            if (_config.DealStrategy != DealStrategy.RevealAndDraft)
            {
                return;
            }

            // Reveal everything remaining in the deck (public information).
            var available = new List<Card>(_deck.Draw(_deck.Count));
            _log($"--- Draft --- revealed: {string.Join(", ", available)}");

            // Bid winner drafts first, then clockwise.
            for (int i = 0; i < _playerCount && available.Count > 0; i++)
            {
                int seat = (_bidWinnerSeat + i) % _playerCount;
                RoundParticipant p = _participants[seat];
                Card picked = _controllers[seat].ChooseDraftCard(p, available, _trump.Value);

                if (!available.Remove(picked))
                {
                    // Controller returned a card not on offer — defensively take
                    // the first available instead of trusting the bad pick.
                    picked = available[0];
                    available.RemoveAt(0);
                }

                p.Hand.Add(picked);
                _log($"  {p.Player.Name} drafts {picked}");
            }

            _log(string.Empty);
        }

        // --- bidding --------------------------------------------------------

        /// <summary>
        /// Run the bidding phase. Seats bid in order starting at
        /// <see cref="_startingBidderSeat"/> (rotated per round by the match
        /// loop); a pass drops a seat out; a numeric bid must exceed the current
        /// high bid and not exceed the configured maximum (an out-of-range bid is
        /// treated as a pass). Bidding ends when at most one seat is still in. If
        /// every seat passes, the FORCED-1 rule applies: the last player to pass
        /// is forced to a contract of 1. The winner becomes the opening leader.
        /// </summary>
        private void RunBidding()
        {
            _log("--- Bidding ---");

            bool[] active = new bool[_playerCount];
            for (int s = 0; s < _playerCount; s++)
            {
                active[s] = true;
            }

            int highBid = 0;          // 0 == no numeric bid yet
            int highSeat = -1;
            int activeCount = _playerCount;
            int lastPasserSeat = _startingBidderSeat;  // seat of the most recent pass (for forced-1)

            int seat = _startingBidderSeat;
            // Cycle through seats until at most one remains active. A seat that
            // is the sole remaining active bidder with a standing bid wins.
            while (activeCount > 1 || (activeCount >= 1 && highSeat < 0))
            {
                if (active[seat])
                {
                    // If this seat already holds the high bid and is the only
                    // one left active, it wins — stop.
                    if (activeCount == 1 && highSeat == seat)
                    {
                        break;
                    }

                    var ctx = new BiddingContext(highBid > 0 ? highBid : (int?)null, _config.MaxBid);
                    Bid bid = _controllers[seat].PlaceBid(_participants[seat], ctx);

                    bool valid = !bid.IsPass && bid.Value >= ctx.MinLegalBid && bid.Value <= _config.MaxBid;

                    if (valid)
                    {
                        highBid = bid.Value;
                        highSeat = seat;
                        _log($"  {_participants[seat].Player.Name} bids {highBid}");
                    }
                    else
                    {
                        active[seat] = false;
                        activeCount--;
                        lastPasserSeat = seat;
                        _log($"  {_participants[seat].Player.Name} passes");
                    }
                }

                // Safety: if nobody is active and nobody ever bid, break to the
                // forced-1 handling below.
                if (activeCount == 0)
                {
                    break;
                }

                seat = (seat + 1) % _playerCount;
            }

            if (highSeat < 0)
            {
                // Everyone passed — the LAST player who passed is forced to 1.
                highSeat = lastPasserSeat;
                highBid = 1;
                _log($"  All passed — {_participants[highSeat].Player.Name} (last to pass) is forced to 1");
            }

            _winningBid = highBid;
            _bidWinnerSeat = highSeat;
            _leaderIndex = highSeat;

            _log($"  => {_participants[highSeat].Player.Name} wins the bid at {highBid} and leads.\n");
        }

        // --- trick loop -----------------------------------------------------

        private void PlayAllTricks()
        {
            int trickNumber = 0;

            while (AnyHandNonEmpty())
            {
                trickNumber++;
                _log($"--- Trick {trickNumber} ---");

                Trick trick = PlayOneTrick();
                ResolveTrick(trick);
                DrawFromPile();
            }
        }

        private bool AnyHandNonEmpty()
        {
            foreach (RoundParticipant p in _participants)
            {
                if (!p.Hand.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        private Trick PlayOneTrick()
        {
            Trick trick = new Trick();
            trick.SetTrump(_trump);  // trump was declared before play; fixed all round
            // Draw-pile size is fixed for the duration of a trick.
            trick.SetDrawPileCount(_deck.Count);

            for (int i = 0; i < _playerCount; i++)
            {
                int seat = (_leaderIndex + i) % _playerCount;
                RoundParticipant participant = _participants[seat];
                IPlayerController controller = _controllers[seat];

                // The opening lead is the bid winner's very first play of the
                // round; the LeadTrumpOnOpeningRule uses this to force a trump.
                bool isOpeningLead = _firstPlayOfRound && trick.IsEmpty;

                // Marriage announcement: only when leading. The engine decides,
                // awards the bonus, and stamps the suit on the context so the
                // AnnouncedMarriageLeadRule restricts the lead to its 3/4.
                Suit? announcedSuit = null;
                if (trick.IsEmpty)
                {
                    announcedSuit = ResolveAnnouncement(participant, controller, isOpeningLead);
                }

                var context = new LegalMoveContext(
                    participant.Hand, trick, _trump, drawPileCount: _deck.Count,
                    isOpeningLead: isOpeningLead, announcedSuit: announcedSuit);
                IReadOnlyList<Card> legalMoves = _legalMoves.LegalMoves(context);

                Card chosen = controller.ChooseCard(participant, legalMoves, trick);
                participant.Hand.Remove(chosen);
                trick.Add(participant, chosen);
                _firstPlayOfRound = false;

                _log($"  {participant.Player.Name} plays {chosen}");
            }

            return trick;
        }

        /// <summary>
        /// Ask the leading controller whether to announce a marriage, award the
        /// bonus if it does, and return the announced suit (or null).
        ///
        /// On the OPENING lead the lead must be trump, so only a trump marriage
        /// can be announced there; other leads allow any held marriage. The
        /// bonus is 40 for a trump marriage, 20 otherwise, added to the
        /// participant's marriage tally (which feeds the team's point total).
        /// </summary>
        private Suit? ResolveAnnouncement(RoundParticipant participant, IPlayerController controller, bool isOpeningLead)
        {
            List<Suit> available = participant.Hand.MarriageSuits().ToList();

            // On the opening lead only a trump marriage is compatible with the
            // forced-trump-lead rule.
            if (isOpeningLead)
            {
                available = available.Where(s => _trump.HasValue && s == _trump.Value).ToList();
            }

            if (available.Count == 0)
            {
                return null;
            }

            Suit? announced = controller.AnnounceMarriage(participant, available, _trump);
            if (announced == null || !available.Contains(announced.Value))
            {
                return null;
            }

            int bonus = (_trump.HasValue && announced.Value == _trump.Value) ? 40 : 20;
            participant.AddMarriagePoints(bonus);
            _log($"  {participant.Player.Name} announces a marriage in {announced.Value} (+{bonus})");

            return announced;
        }

        /// <summary>
        /// Award the trick's points to the winning participant (its scoring unit
        /// total updates automatically), and set the winner as next leader.
        /// </summary>
        private void ResolveTrick(Trick trick)
        {
            RoundParticipant winner = trick.DetermineWinner(_trump);
            int points = trick.PointValue;

            winner.AddCapturedPoints(points);
            _leaderIndex = Array.IndexOf(_participants, winner);

            ScoringUnit unit = _unitOfSeat[_leaderIndex];
            _log($"  => {winner.Player.Name} wins the trick ({points} pts). " +
                 $"Unit [{unit.Label}] total: {unit.CapturedPoints}\n");
        }

        /// <summary>
        /// For DrawPilePerTrick: if cards remain, every player draws one,
        /// starting with the trick winner (now the leader), going clockwise.
        /// </summary>
        private void DrawFromPile()
        {
            if (_config.DealStrategy != DealStrategy.DrawPilePerTrick || _deck.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _playerCount && _deck.Count > 0; i++)
            {
                int seat = (_leaderIndex + i) % _playerCount;
                Card drawn = _deck.Draw();
                _participants[seat].Hand.Add(drawn);
                _log($"  {_participants[seat].Player.Name} draws {drawn}");
            }

            _log($"  Draw pile: {_deck.Count} remaining.\n");
        }
    }
}
