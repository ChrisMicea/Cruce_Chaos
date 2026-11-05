using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// One player's state for a single round: the hand they hold and the card
    /// points they have captured from tricks won this round. Created fresh at
    /// the start of each round and discarded at the end.
    ///
    /// The <see cref="Player"/> link ties this transient state back to the
    /// persistent match-level identity and score.
    /// </summary>
    public sealed class RoundParticipant
    {
        public Player Player { get; }

        public Hand Hand { get; }

        /// <summary>
        /// Card points captured from tricks this round (A=11, 10=10, 4=4, 3=3,
        /// 2=2, 9=0). A round distributes all 120 points among participants.
        /// </summary>
        public int CapturedPoints { get; private set; }

        /// <summary>
        /// Bonus points from marriages (anunțări) announced this round: 40 for a
        /// trump marriage, 20 otherwise. Separate from captured card points, but
        /// summed into the team's point total for scoring (which is how a side
        /// can exceed the 120 raw ceiling to make a bid of 4–6).
        /// </summary>
        public int MarriagePoints { get; private set; }

        public RoundParticipant(Player player)
        {
            Player = player;
            Hand = new Hand();
        }

        /// <summary>Adds the card points from a won trick to this round's tally.</summary>
        public void AddCapturedPoints(int points) => CapturedPoints += points;

        /// <summary>Adds a marriage bonus (20 or 40) to this round's tally.</summary>
        public void AddMarriagePoints(int points) => MarriagePoints += points;

        public override string ToString() => $"{Player.Name} ({CapturedPoints} pts)";
    }
}
