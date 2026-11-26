using CruceChaos.Core.Cards;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Extension methods for Card to simplify network serialization.
    /// 
    /// Usage:
    ///   Card card = new Card(Suit.Red, Rank.Ace);
    ///   string data = card.ToNetworkString();
    ///   Card parsed = data.ToCard();
    /// 
    /// These are convenience wrappers around NetworkCardSync's static methods.
    /// </summary>
    public static class CardNetworkExtensions
    {
        /// <summary>
        /// Serializes this Card to a network-transmittable string.
        /// Format: "Suit:Rank" (e.g., "1:5" for Red Ace)
        /// </summary>
        public static string ToNetworkString(this Card card)
        {
            return NetworkCardSync.SerializeCard(card);
        }

        /// <summary>
        /// Deserializes a network string to a Card.
        /// Returns the parsed Card if successful.
        /// Throws an exception if the string is invalid.
        /// </summary>
        public static Card ToCard(this string networkString)
        {
            if (NetworkCardSync.TryParseCard(networkString, out Card card))
            {
                return card;
            }

            throw new System.ArgumentException(
                $"Invalid card network string: {networkString}");
        }

        /// <summary>
        /// Attempts to deserialize a network string to a Card.
        /// Returns true if successful, false otherwise.
        /// Safe version that doesn't throw exceptions.
        /// </summary>
        public static bool TryToCard(this string networkString, out Card card)
        {
            return NetworkCardSync.TryParseCard(networkString, out card);
        }
    }
}
