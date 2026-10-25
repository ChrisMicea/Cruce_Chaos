using System;
using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Players;
using CruceChaos.Core.Rules;
using CruceChaos.Core.Game.Scoring;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// Plays a full match: repeated rounds via <see cref="RoundEngine"/> until a
    /// team reaches the target score AND leads the runner-up by at least 2.
    /// Rotates the starting bidder each round (fairness — whoever opens has an
    /// advantage under these rules), converts each round's captured points to
    /// match-score deltas with an <see cref="IRoundScorer"/>, and applies each
    /// team's delta in lockstep to every <see cref="Player.MatchScore"/> on that
    /// team.
    ///
    /// Team scoring is representation (a): teammates' Player.MatchScore always
    /// move together, so a team's score equals any member's score.
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly Player[] _players;
        private readonly IPlayerController[] _controllers;
        private readonly MatchConfig _config;
        private readonly IRoundScorer _scorer;
        private readonly int? _seed;
        private readonly Action<string> _log;

        /// <summary>Distinct team indices present, in ascending order.</summary>
        private readonly int[] _teams;

        /// <summary>Seats grouped by team index.</summary>
        private readonly Dictionary<int, List<int>> _seatsByTeam;

        public MatchEngine(
            Player[] players,
            IPlayerController[] controllers,
            MatchConfig config,
            IRoundScorer scorer = null,
            int? seed = null,
            Action<string> log = null)
        {
            _players = players;
            _controllers = controllers;
            _config = config;
            _scorer = scorer ?? new StandardRoundScorer();
            _seed = seed;
            _log = log ?? (_ => { });

            _teams = config.TeamOfSeat.Distinct().OrderBy(t => t).ToArray();
            _seatsByTeam = new Dictionary<int, List<int>>();
            for (int seat = 0; seat < config.PlayerCount; seat++)
            {
                int team = config.TeamOfSeat[seat];
                if (!_seatsByTeam.TryGetValue(team, out List<int> seats))
                {
                    seats = new List<int>();
                    _seatsByTeam[team] = seats;
                }

                seats.Add(seat);
            }
        }

        /// <summary>Play the match to completion and return the result.</summary>
        public MatchResult Play()
        {
            int roundNumber = 0;
            int winningTeam;

            while (!IsMatchOver(out winningTeam))
            {
                roundNumber++;
                // Rotate the starting bidder each round.
                int startingBidder = (roundNumber - 1) % _config.PlayerCount;

                _log($"========== Round {roundNumber} (opens: {_players[startingBidder].Name}) ==========");

                RuleSet ruleSet = BuildRuleSet();
                // Vary the per-round seed so successive rounds differ but the
                // match stays reproducible for a given match seed.
                int? roundSeed = _seed.HasValue ? _seed.Value + roundNumber : (int?)null;

                var round = new RoundEngine(
                    _players, _controllers, _config, ruleSet, roundSeed, _log, startingBidder);
                RoundResult result = round.Play();

                ApplyScores(result);
                LogStandings();
            }

            return new MatchResult(winningTeam, SeatsOfTeam(winningTeam).Select(s => _players[s]).ToArray(), TeamScore(winningTeam));
        }

        private RuleSet BuildRuleSet()
        {
            return _config.DealStrategy == DealStrategy.DrawPilePerTrick
                ? RuleSetFactory.DrawPileRuleSet(_config.RuleOptions)
                : RuleSetFactory.NoDrawPile(_config.RuleOptions);
        }

        /// <summary>Apply each team's round delta in lockstep to its members.</summary>
        private void ApplyScores(RoundResult result)
        {
            IReadOnlyDictionary<int, int> deltas = _scorer.ScoreRound(result);

            foreach (KeyValuePair<int, int> kv in deltas)
            {
                int team = kv.Key;
                int delta = kv.Value;
                foreach (int seat in SeatsOfTeam(team))
                {
                    _players[seat].ApplyRoundScore(delta);
                }

                _log($"  Team [{TeamLabel(team)}] round delta {delta:+0;-0;0} -> {TeamScore(team)}");
            }
        }

        /// <summary>
        /// Match ends when the leading team is at or past the target AND at
        /// least 2 clear of the runner-up (so a 1-point gap keeps play going).
        /// </summary>
        private bool IsMatchOver(out int winningTeam)
        {
            winningTeam = -1;

            // Teams ranked by score, highest first.
            int[] ranked = _teams.OrderByDescending(TeamScore).ToArray();
            int leader = ranked[0];
            int leaderScore = TeamScore(leader);
            int runnerUpScore = ranked.Length > 1 ? TeamScore(ranked[1]) : int.MinValue;

            if (leaderScore >= _config.TargetScore && (leaderScore - runnerUpScore) >= 2)
            {
                winningTeam = leader;
                return true;
            }

            return false;
        }

        private IEnumerable<int> SeatsOfTeam(int team) => _seatsByTeam[team];

        /// <summary>A team's score = any member's Player.MatchScore (they move in lockstep).</summary>
        private int TeamScore(int team) => _players[_seatsByTeam[team][0]].MatchScore;

        private string TeamLabel(int team) =>
            string.Join(" / ", SeatsOfTeam(team).Select(s => _players[s].Name));

        private void LogStandings()
        {
            string standings = string.Join("  |  ",
                _teams.OrderByDescending(TeamScore).Select(t => $"{TeamLabel(t)}: {TeamScore(t)}"));
            _log($"  Standings: {standings}\n");
        }
    }

    /// <summary>The outcome of a completed match.</summary>
    public sealed class MatchResult
    {
        public int WinningTeam { get; }
        public Player[] Winners { get; }
        public int WinningScore { get; }

        public MatchResult(int winningTeam, Player[] winners, int winningScore)
        {
            WinningTeam = winningTeam;
            Winners = winners;
            WinningScore = winningScore;
        }
    }
}
