using System;
using System.Collections.Generic;

namespace CruceChaos.Core.Cards
{
    /// <summary>
    /// A Cruce deck: the 24 distinct cards (4 suits × 6 ranks), with
    /// shuffling and top-of-pile drawing.
    ///
    /// Pure C#, no Unity dependency. Shuffling uses an injectable
    /// <see cref="System.Random"/> so tests (and reproducible games) can seed
    /// it deterministically.
    ///
    /// The "top" of the deck is the END of the internal list, so drawing is an
    /// O(1) remove-from-end rather than a shift of every remaining card.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<Card> _cards;
        private readonly Random _rng;

        /// <summary>Cards still left to draw.</summary>
        public int Count => _cards.Count;

        /// <summary>
        /// Builds a full, ordered 24-card deck. Call <see cref="Shuffle"/>
        /// before dealing. Pass a seed for reproducible shuffles; omit it for
        /// a time-seeded random game.
        /// </summary>
        public Deck(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _cards = new List<Card>(24);
            Reset();
        }

        /// <summary>Refills the deck to all 24 cards in a fixed order.</summary>
        public void Reset()
        {
            _cards.Clear();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _cards.Add(new Card(suit, rank));
                }
            }
        }

        /// <summary>In-place Fisher–Yates shuffle.</summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>
        /// Removes and returns the top card. Throws if the deck is empty;
        /// callers that may hit an empty deck should check <see cref="Count"/>
        /// first.
        /// </summary>
        public Card Draw()
        {
            if (_cards.Count == 0)
            {
                throw new InvalidOperationException("Cannot draw from an empty deck.");
            }

            int top = _cards.Count - 1;
            Card card = _cards[top];
            _cards.RemoveAt(top);
            return card;
        }

        /// <summary>Draws <paramref name="count"/> cards off the top.</summary>
        public List<Card> Draw(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot draw a negative number of cards.");
            }

            if (count > _cards.Count)
            {
                throw new InvalidOperationException($"Cannot draw {count} cards; only {_cards.Count} remain.");
            }

            var drawn = new List<Card>(count);
            for (int i = 0; i < count; i++)
            {
                drawn.Add(Draw());
            }

            return drawn;
        }
    }
}
