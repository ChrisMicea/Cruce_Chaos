using System.Collections.Generic;
using System.Linq;

namespace CruceChaos.Core.Cards
{
    /// <summary>
    /// The set of cards a single player is holding.
    ///
    /// Pure C#, no Unity dependency. A hand is an ordered, mutable collection:
    /// cards are added as they are dealt or drawn, and removed as they are
    /// played. Order is preserved only for display convenience; the game
    /// rules never depend on the position of a card within a hand.
    ///
    /// NOTE: Cruce has no duplicate cards (every (suit, rank) pair is unique in
    /// the 24-card deck), so a hand never legitimately holds the same card
    /// twice. <see cref="Remove"/> removes a single matching card regardless.
    /// </summary>
    public sealed class Hand
    {
        private readonly List<Card> _cards;

        public Hand()
        {
            _cards = new List<Card>(8);
        }

        public Hand(IEnumerable<Card> cards)
        {
            _cards = new List<Card>(cards);
        }

        /// <summary>Number of cards currently held.</summary>
        public int Count => _cards.Count;

        /// <summary>True when the hand holds no cards.</summary>
        public bool IsEmpty => _cards.Count == 0;

        /// <summary>
        /// Read-only view of the cards, for display and for rule queries.
        /// Callers cannot mutate the hand through this; use <see cref="Add"/>
        /// and <see cref="Remove"/> instead.
        /// </summary>
        public IReadOnlyList<Card> Cards => _cards;

        /// <summary>Adds a single card to the hand.</summary>
        public void Add(Card card) => _cards.Add(card);

        /// <summary>Adds several cards (e.g. the initial 8-card deal).</summary>
        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);

        /// <summary>True if the hand contains the given card.</summary>
        public bool Contains(Card card) => _cards.Contains(card);

        /// <summary>
        /// Removes one matching card from the hand. Returns true if a card was
        /// removed, false if the hand did not contain it.
        /// </summary>
        public bool Remove(Card card) => _cards.Remove(card);

        /// <summary>
        /// All cards of a given suit currently in the hand. A convenience query
        /// for UI and AI; the legality rules currently filter inline.
        /// </summary>
        public IEnumerable<Card> CardsOfSuit(Suit suit) => _cards.Where(c => c.Suit == suit);

        /// <summary>True if the hand holds at least one card of the suit.</summary>
        public bool HasSuit(Suit suit) => _cards.Any(c => c.Suit == suit);

        /// <summary>
        /// True if the hand holds a MARRIAGE in the given suit — both the Three
        /// and the Four of that suit (the treiar + pătrar announcement pair).
        /// </summary>
        public bool HasMarriage(Suit suit) =>
            _cards.Any(c => c.Suit == suit && c.Rank == Rank.Three) &&
            _cards.Any(c => c.Suit == suit && c.Rank == Rank.Four);

        /// <summary>The suits in which the hand currently holds a full marriage.</summary>
        public IEnumerable<Suit> MarriageSuits()
        {
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)).Cast<Suit>())
            {
                if (HasMarriage(suit))
                {
                    yield return suit;
                }
            }
        }

        public override string ToString() => string.Join(", ", _cards);
    }
}
