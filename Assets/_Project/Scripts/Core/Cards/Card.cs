using System;

namespace CruceChaos.Core.Cards
{
    /// <summary>
    /// A single Cruce card: an immutable (Suit, Rank) pair.
    ///
    /// This is a pure-domain value type with no Unity dependency. Two cards
    /// are equal when they share the same suit and rank, so cards can be
    /// compared, used as dictionary keys, and checked for containment in a
    /// hand without any reference bookkeeping.
    /// </summary>
    [Serializable]
    public readonly struct Card : IEquatable<Card>
    {
        public Suit Suit { get; }
        public Rank Rank { get; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>
        /// Card-point value counted toward a player's score at the end of a
        /// round: Ace=11, Ten=10, Four=4, Three=3, Two=2, Nine=0.
        /// </summary>
        public int Points
        {
            get
            {
                switch (Rank)
                {
                    case Rank.Ace: return 11;
                    case Rank.Ten: return 10;
                    case Rank.Four: return 4;
                    case Rank.Three: return 3;
                    case Rank.Two: return 2;
                    case Rank.Nine: return 0;
                    default: return 0;
                }
            }
        }

        /// <summary>
        /// Trick-taking strength. Higher beats lower WITHIN the same suit (or
        /// when comparing two trumps). Derived from the <see cref="Rank"/>
        /// ordinal so the ordering lives in exactly one place.
        /// </summary>
        public int Strength => (int)Rank;

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => ((int)Suit * 6) + (int)Rank;

        public static bool operator ==(Card left, Card right) => left.Equals(right);

        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        public override string ToString() => $"{Rank} of {Suit}";
    }
}
