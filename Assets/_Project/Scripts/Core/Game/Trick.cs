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
    }

    /// <summary>
    /// A single trick: the cards each participant plays in turn, and the rule
    /// for who wins it.
    ///
    /// SIMPLIFIED (no-trump) winner rule for this first milestone: the highest
    /// <see cref="Card.Strength"/> among cards of the LED suit wins. Cards of
    /// any other suit cannot win, because without trump an off-suit card is
    /// just a discard. Trump will later add a second, higher-priority suit that
    /// can beat the led suit; that logic will slot into <see cref="Winner"/>.
    /// </summary>
    public sealed class Trick : ITrickContext
    {
        private readonly List<PlayedCard> _plays = new List<PlayedCard>(4);

        public IReadOnlyList<PlayedCard> Plays => _plays;

        public bool IsEmpty => _plays.Count == 0;

        public Suit LedSuit => _plays[0].Card.Suit;

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
        /// The participant who wins the trick under the simplified no-trump
        /// rule. Returns null only if no cards have been played yet.
        /// </summary>
        public RoundParticipant Winner
        {
            get
            {
                if (_plays.Count == 0)
                {
                    return null;
                }

                Suit led = LedSuit;
                PlayedCard best = _plays[0];

                foreach (PlayedCard play in _plays)
                {
                    // Only cards of the led suit are in contention (no trump yet).
                    if (play.Card.Suit != led)
                    {
                        continue;
                    }

                    if (play.Card.Strength > best.Card.Strength)
                    {
                        best = play;
                    }
                }

                return best.Participant;
            }
        }
    }
}
