using System.Collections.Generic;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// One card played into the current trick, paired with who played it.
    /// </summary>
    public readonly struct PlayedCard
    {
        public RoundParticipant Participant { get; }
        public Card Card { get; }

        public PlayedCard(RoundParticipant participant, Card card)
        {
            Participant = participant;
            Card = card;
        }
    }

    /// <summary>
    /// Read-only view of the trick currently in progress, handed to a
    /// controller so it can decide what to play without being able to mutate
    /// the trick itself.
    /// </summary>
    public interface ITrickContext
    {
        /// <summary>Cards played so far this trick, in play order.</summary>
        IReadOnlyList<PlayedCard> Plays { get; }

        /// <summary>True until the first card of the trick has been played.</summary>
        bool IsEmpty { get; }

        /// <summary>
        /// The suit led this trick (the suit of the first card played).
        /// Only meaningful when <see cref="IsEmpty"/> is false.
        /// </summary>
        Suit LedSuit { get; }

        /// <summary>
        /// The round's trump suit, or null if the mode has no trump. Public
        /// information (declared by the bid winner before play), exposed so
        /// controllers can make trump-aware decisions.
        /// </summary>
        Suit? Trump { get; }

        /// <summary>
        /// Number of cards still in the draw pile. Public information, exposed
        /// so controllers can tell whether the no-follow-suit draw phase is
        /// still in effect (pile &gt; 0).
        /// </summary>
        int DrawPileCount { get; }
    }

    /// <summary>
    /// A single trick: the cards each participant plays in turn, and the rule
    /// for who wins it.
    ///
    /// Winner rule (see <see cref="DetermineWinner"/>):
    ///   - If any trump was played, the HIGHEST trump wins (trump beats every
    ///     non-trump regardless of the led suit).
    ///   - Otherwise the highest card of the LED suit wins; off-suit non-trump
    ///     cards cannot win (they are discards).
    /// </summary>
    public sealed class Trick : ITrickContext
    {
        private readonly List<PlayedCard> _plays = new List<PlayedCard>(4);

        public IReadOnlyList<PlayedCard> Plays => _plays;

        public bool IsEmpty => _plays.Count == 0;

        public Suit LedSuit => _plays[0].Card.Suit;

        /// <summary>
        /// The round's trump suit as seen by controllers, set once per trick by
        /// the engine via <see cref="SetTrump"/> (trump is declared before play,
        /// so it is fixed for the whole round).
        /// </summary>
        public Suit? Trump { get; private set; }

        /// <summary>Sets the trump suit the controllers will see for this trick.</summary>
        public void SetTrump(Suit? trump) => Trump = trump;

        /// <summary>Cards left in the draw pile, as seen by controllers.</summary>
        public int DrawPileCount { get; private set; }

        /// <summary>Sets the draw-pile count the controllers will see for this trick.</summary>
        public void SetDrawPileCount(int count) => DrawPileCount = count;

        /// <summary>Records a participant's play into the trick.</summary>
        public void Add(RoundParticipant participant, Card card)
        {
            _plays.Add(new PlayedCard(participant, card));
        }

        /// <summary>Total card points lying in this trick (the reward for winning it).</summary>
        public int PointValue
        {
            get
            {
                int total = 0;
                foreach (PlayedCard play in _plays)
                {
                    total += play.Card.Points;
                }

                return total;
            }
        }

        /// <summary>
        /// The participant who wins the trick, given the round's
        /// <paramref name="trump"/> suit (null if the round has no trump).
        /// Returns null only if no cards have been played yet.
        /// </summary>
        public RoundParticipant DetermineWinner(Suit? trump)
        {
            if (_plays.Count == 0)
            {
                return null;
            }

            // The "contested suit" is trump if any trump was played, else the
            // led suit. Only cards of that suit can win.
            bool trumpPlayed = trump.HasValue && AnyOfSuit(trump.Value);
            Suit contested = trumpPlayed ? trump.Value : LedSuit;

            PlayedCard best = default;
            bool haveBest = false;

            foreach (PlayedCard play in _plays)
            {
                if (play.Card.Suit != contested)
                {
                    continue;
                }

                if (!haveBest || play.Card.Strength > best.Card.Strength)
                {
                    best = play;
                    haveBest = true;
                }
            }

            // The led suit is always present (the first play), and trump is only
            // the contested suit when some trump was played, so a winner always
            // exists past the empty-trick guard above.
            return best.Participant;
        }

        private bool AnyOfSuit(Suit suit)
        {
            foreach (PlayedCard play in _plays)
            {
                if (play.Card.Suit == suit)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
