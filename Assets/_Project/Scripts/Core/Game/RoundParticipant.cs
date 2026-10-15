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

        public RoundParticipant(Player player)
        {
            Player = player;
            Hand = new Hand();
        }

        /// <summary>Adds the card points from a won trick to this round's tally.</summary>
        public void AddCapturedPoints(int points) => CapturedPoints += points;

        public override string ToString() => $"{Player.Name} ({CapturedPoints} pts)";
    }
}
