using System.Collections.Generic;
using System.Linq;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// The entity that OWNS captured points in a round — an individual player in
    /// free-for-all modes, or a team of players in 2v2.
    ///
    /// This is a VIEW over its member participants, not a second point
    /// accumulator: captured points live on each <see cref="RoundParticipant"/>
    /// (the single source of truth), and <see cref="CapturedPoints"/> is their
    /// computed sum. The indirection is the seam that lets team scoring slot in
    /// without touching the trick loop — the engine awards a won trick's points
    /// to the winning participant, and the unit total is always just the sum of
    /// its members. In individual play a unit wraps one participant (so its
    /// total equals that participant's); in team play several participants share
    /// a unit, so their points pool automatically.
    ///
    /// Relationship to the rest of the model:
    ///   Player : RoundParticipant :: Team : ScoringUnit
    /// A ScoringUnit is the per-round projection of a team, exactly as a
    /// RoundParticipant is the per-round projection of a Player. It does NOT
    /// overlap with Player — Player holds the persistent, cross-round MATCH
    /// score in 1–6 units, whereas a unit holds a transient, per-round CARD
    /// points sum. They meet only later, when scoring conversion turns a unit's
    /// card points into a match-score delta applied to every Player on the team.
    /// </summary>
    public sealed class ScoringUnit
    {
        /// <summary>Team index this unit represents (its id within the round).</summary>
        public int TeamIndex { get; }

        /// <summary>Participants whose captured points this unit sums over.</summary>
        public IReadOnlyList<RoundParticipant> Members => _members;
        private readonly List<RoundParticipant> _members = new List<RoundParticipant>();

        /// <summary>
        /// Card points captured by this unit this round — the SUM of its
        /// members' captured points. Computed, not stored, so there is one
        /// source of truth (the participants).
        /// </summary>
        public int CapturedPoints => _members.Sum(m => m.CapturedPoints);

        /// <summary>Marriage bonus points for this unit — sum of members' marriage points.</summary>
        public int MarriagePoints => _members.Sum(m => m.MarriagePoints);

        /// <summary>
        /// Total points for scoring = captured card points + marriage bonuses.
        /// This is the figure the round scorer divides by 33, so marriages let a
        /// side exceed 120 and make higher bids.
        /// </summary>
        public int TotalPoints => CapturedPoints + MarriagePoints;

        public ScoringUnit(int teamIndex)
        {
            TeamIndex = teamIndex;
        }

        public void AddMember(RoundParticipant participant) => _members.Add(participant);

        /// <summary>
        /// A display label: the single member's name for individual units, or a
        /// slash-joined list for teammates.
        /// </summary>
        public string Label => string.Join(" / ", _members.Select(m => m.Player.Name));

        public override string ToString() => $"{Label} ({CapturedPoints} pts)";
    }
}
