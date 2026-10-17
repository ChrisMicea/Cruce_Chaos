using System;
using System.Collections.Generic;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Players;
using CruceChaos.Core.Rules;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// Drives one full round of a 2-player "cu carti de jos" Cruce game:
    ///
    ///   1. Shuffle and deal 8 cards each; 8 remain as the draw pile.
    ///   2. Loop:
    ///      a. Each player plays one card into the trick.
    ///      b. The trick winner takes the card points and leads next.
    ///      c. If the draw pile is not empty, both draw one card (winner first).
    ///   3. Continue until all cards have been played.
    ///   4. Report each player's captured card-points (out of 120).
    ///
    /// Simplified for the first milestone: NO trump, NO follow-suit
    /// enforcement, NO bidding. All cards are always legal to play.
    /// </summary>
    public sealed class RoundEngine
    {
        private readonly Deck _deck;
        private readonly RoundParticipant[] _participants;
        private readonly IPlayerController[] _controllers;
        private readonly LegalMoveEvaluator _legalMoves;
        private readonly Action<string> _log;

        /// <summary>Index into <see cref="_participants"/> of whoever leads the next trick.</summary>
        private int _leaderIndex;

        /// <summary>
        /// The round's trump suit. Null until established. Per the current
        /// rules, trump is set to the suit of the very first card played in the
        /// round (see <see cref="EstablishTrumpIfUnset"/>). Isolating trump
        /// establishment in one method means a future variant (e.g. the winning
        /// bidder declares trump before any card is played) only changes WHERE
        /// that method is called, not any of the code that reads _trump.
        /// </summary>
        private Suit? _trump;

        /// <summary>
        /// Creates the engine for one round.
        ///
        /// <paramref name="players"/> and <paramref name="controllers"/> must
        /// be of length 2, paired by index (players[0] is controlled by
        /// controllers[0], etc).
        /// </summary>
        /// <param name="players">The two match-level players.</param>
        /// <param name="controllers">The decision makers for each player.</param>
        /// <param name="ruleSet">
        /// The ordered legality rules in play for this round (built by
        /// <see cref="RuleSetFactory"/> from the mode + player options).
        /// </param>
        /// <param name="seed">Optional RNG seed for a reproducible round.</param>
        /// <param name="log">
        /// Optional callback to receive human-readable play-by-play messages.
        /// Pass null to suppress output.
        /// </param>
        public RoundEngine(Player[] players, IPlayerController[] controllers, RuleSet ruleSet, int? seed = null, Action<string> log = null)
        {
            if (players.Length != 2 || controllers.Length != 2)
            {
                throw new ArgumentException("This engine supports exactly 2 players.");
            }

            // Forward the evaluator's safety-guard warnings into our log. The
            // lambda defers to _log at call time, so it is fine that _log is
            // assigned a few lines below this.
            _legalMoves = new LegalMoveEvaluator(ruleSet, msg => _log(msg));
            _deck = new Deck(seed);
            _participants = new RoundParticipant[]
            {
                new RoundParticipant(players[0]),
                new RoundParticipant(players[1])
            };
            _controllers = controllers;
            // Null object pattern: if no logger was supplied, fall back to a
            // do-nothing lambda so every _log(...) call site is safe to call
            // without a null check. '??' picks the left side unless it is null;
            // '(_ => { })' is an Action<string> that ignores its argument ('_'
            // is a throwaway name) and has an empty body, so it logs nothing.
            _log = log ?? (_ => { });
            _leaderIndex = 0;
        }

        /// <summary>
        /// Run the round from start to finish and return the two
        /// <see cref="RoundParticipant"/> objects with their final captured
        /// point tallies.
        /// </summary>
        public RoundParticipant[] Play()
        {
            Deal();
            PlayAllTricks();
            return _participants;
        }

        /// <summary>
        /// Shuffle, deal 8 cards to each participant. The remaining 8 form the
        /// draw pile (they stay in the Deck).
        /// </summary>
        private void Deal()
        {
            _deck.Reset();
            _deck.Shuffle();

            for (int i = 0; i < 2; i++)
            {
                List<Card> dealt = _deck.Draw(8);
                _participants[i].Hand.AddRange(dealt);
                _log($"{_participants[i].Player.Name} receives: {_participants[i].Hand}");
            }

            _log($"Draw pile: {_deck.Count} cards remaining.\n");
        }

        /// <summary>
        /// Play tricks until both hands are empty. After each trick, if the
        /// draw pile has cards, both players draw one.
        /// </summary>
        private void PlayAllTricks()
        {
            int trickNumber = 0;

            while (!_participants[0].Hand.IsEmpty || !_participants[1].Hand.IsEmpty)
            {
                trickNumber++;
                _log($"--- Trick {trickNumber} ---");

                Trick trick = PlayOneTrick();
                ResolveTrick(trick);
                DrawFromPile();
            }
        }

        /// <summary>
        /// Sets the round's trump suit from the first card played, if it has not
        /// been set yet. Returns true if this call established trump (so the
        /// caller can log it), false if trump was already set.
        ///
        /// This is the single point of trump establishment; see the field doc
        /// on <see cref="_trump"/> for why that matters for future variants.
        /// </summary>
        private bool EstablishTrumpIfUnset(Card firstCard)
        {
            if (_trump.HasValue)
            {
                return false;
            }

            _trump = firstCard.Suit;
            return true;
        }

        /// <summary>
        /// Each participant plays one card, starting with the current leader.
        /// </summary>
        private Trick PlayOneTrick()
        {
            Trick trick = new Trick();
            trick.SetTrump(_trump);
            // Draw-pile size is fixed for the duration of a trick (draws happen
            // between tricks), so set it once here for controllers to read.
            trick.SetDrawPileCount(_deck.Count);

            for (int i = 0; i < 2; i++)
            {
                int seat = (_leaderIndex + i) % 2;
                RoundParticipant participant = _participants[seat];
                IPlayerController controller = _controllers[seat];

                // Legality is computed by the rule pipeline from the current
                // state (hand, trick, trump, draw-pile size), so the engine
                // stays mode-agnostic.
                var context = new LegalMoveContext(participant.Hand, trick, _trump, drawPileCount: _deck.Count);
                IReadOnlyList<Card> legalMoves = _legalMoves.LegalMoves(context);

                Card chosen = controller.ChooseCard(participant, legalMoves, trick);
                participant.Hand.Remove(chosen);
                trick.Add(participant, chosen);

                // The first card played in the whole round fixes the trump suit.
                bool trumpJustSet = EstablishTrumpIfUnset(chosen);
                if (trumpJustSet)
                {
                    // Reflect the newly-established trump to later players in
                    // this same trick (the leader had no trump info yet, which
                    // is correct — they are the one who just set it).
                    trick.SetTrump(_trump);
                }

                _log($"  {participant.Player.Name} plays {chosen}" +
                     (trumpJustSet ? $"   (trump is now {chosen.Suit})" : string.Empty));
            }

            return trick;
        }

        /// <summary>
        /// Award the trick's card points to the winner and set them as the
        /// next trick's leader.
        /// </summary>
        private void ResolveTrick(Trick trick)
        {
            RoundParticipant winner = trick.DetermineWinner(_trump);
            int points = trick.PointValue;

            winner.AddCapturedPoints(points);
            _leaderIndex = Array.IndexOf(_participants, winner);

            _log($"  => {winner.Player.Name} wins the trick ({points} pts). " +
                 $"Running total: {winner.CapturedPoints}\n");
        }

        /// <summary>
        /// If cards remain in the draw pile, both players draw one (winner of
        /// the last trick draws first).
        /// </summary>
        private void DrawFromPile()
        {
            if (_deck.Count == 0)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                int seat = (_leaderIndex + i) % 2;
                Card drawn = _deck.Draw();
                _participants[seat].Hand.Add(drawn);
                _log($"  {_participants[seat].Player.Name} draws {drawn}");
            }

            _log($"  Draw pile: {_deck.Count} remaining.\n");
        }
    }
}
