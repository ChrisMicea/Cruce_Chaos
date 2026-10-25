using System;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// A single bidding decision: either PASS, or a numeric contract value.
    ///
    /// The value is an arbitrary positive integer, NOT capped at 6. Vanilla
    /// Cruce caps contracts at 6, but that cap lives in <see cref="MatchConfig"/>
    /// (so power-ups can raise it); the <see cref="Bid"/> type itself only
    /// distinguishes "pass" from "a number". The engine validates a numeric bid
    /// against the configured maximum and against the current high bid.
    ///
    /// UI note: a bid UI is expected to offer at least PASS and "raise by 1",
    /// and may offer a custom numeric entry — hence the model carries a free
    /// integer rather than a fixed set of choices.
    /// </summary>
    public readonly struct Bid : IEquatable<Bid>
    {
        /// <summary>True if this is a pass (no contract).</summary>
        public bool IsPass { get; }

        /// <summary>The contract value; meaningful only when <see cref="IsPass"/> is false.</summary>
        public int Value { get; }

        private Bid(bool isPass, int value)
        {
            IsPass = isPass;
            Value = value;
        }

        /// <summary>A pass.</summary>
        public static Bid Pass => new Bid(true, 0);

        /// <summary>A numeric contract of <paramref name="value"/> (must be positive).</summary>
        public static Bid Of(int value)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A numeric bid must be at least 1.");
            }

            return new Bid(false, value);
        }

        public bool Equals(Bid other) => IsPass == other.IsPass && Value == other.Value;
        public override bool Equals(object obj) => obj is Bid b && Equals(b);
        public override int GetHashCode() => IsPass ? -1 : Value;

        public override string ToString() => IsPass ? "PASS" : $"bid {Value}";
    }
}
