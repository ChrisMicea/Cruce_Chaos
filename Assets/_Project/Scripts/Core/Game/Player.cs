namespace CruceChaos.Core.Game
{
    /// <summary>
    /// A participant for the whole match: a stable identity and the match
    /// score accumulated across rounds. 
    /// One instance per player, created once and reused for every round.
    ///
    /// Per-round state (the cards in hand, points captured this round) does
    /// NOT live here; see <see cref="RoundParticipant"/>.
    /// </summary>
    public sealed class Player
    {
        public string Name { get; }

        /// <summary>
        /// Running match score across rounds, in the Cruce 1–6 scoring units
        /// (NOT card points). Updated once per round by the scoring logic.
        /// </summary>
        public int MatchScore { get; private set; }

        public Player(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Applies a round result to the match score.
        /// </summary>
        public void ApplyRoundScore(int delta) => MatchScore += delta;

        public override string ToString() => Name;
    }
}
