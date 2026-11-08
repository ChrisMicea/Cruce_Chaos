using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Game.Scoring;
using CruceChaos.Core.Players;
using CruceChaos.Core.Players.AI.GreedyHeuristic;
using CruceChaos.Core.Rules;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Central scene controller for 1-human-vs-1-AI Cruce.
    ///
    /// Drives the full round pipeline manually as a coroutine so every async
    /// human decision (bid, trump declaration, marriage announcement, card play)
    /// can pause and wait for UI input before the engine advances.
    ///
    /// Flow per round:
    ///   Deal → Bid → DeclareTrump (bid winner) → PlayAllTricks → Score
    ///
    /// Each trick:
    ///   (optional marriage announce if leading) → PlayCard → resolve → draw
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Inspector wiring ──────────────────────────────────────────────────

        [Header("Data")]
        [SerializeField] private CardSpriteRegistry _spriteRegistry;

        [Header("Prefabs")]
        [SerializeField] private GameObject _cardPrefab;

        [Header("Scene References")]
        [SerializeField] private RectTransform      _humanHandArea;

        [Tooltip("Face-down hand areas for the non-human seats, in seat order: " +
                 "element 0 = seat 1, element 1 = seat 2, element 2 = seat 3. " +
                 "Length must be PlayerCount-1 (one for 1v1, three for 2v2). " +
                 "Clockwise screen layout: seat 1 = LEFT, seat 2 = TOP, seat 3 = RIGHT.")]
        [SerializeField] private RectTransform[]    _opponentHandAreas;

        [Tooltip("Centre-screen area where the revealed leftover cards appear " +
                 "during the draft (\"surprises\") phase. Only needed for the 2v2 " +
                 "draft variant; leave unassigned for 1v1 / classic 2v2.")]
        [SerializeField] private RectTransform      _draftRevealArea;
        [SerializeField] private TrickAreaView      _trickAreaView;
        [SerializeField] private ScoreHUD           _scoreHUD;
        [SerializeField] private HumanPlayerController _humanController;

        [Header("UI Panels")]
        [Tooltip("Panel shown during the bidding phase so the human can choose a bid.")]
        [SerializeField] private BidPanel           _bidPanel;

        [Tooltip("Panel shown so the bid winner can choose trump suit.")]
        [SerializeField] private TrumpPickerPanel   _trumpPickerPanel;

        [Tooltip("Panel shown when the human can announce a marriage.")]
        [SerializeField] private MarriagePanel      _marriagePanel;

        [Header("Card Back Sprite")]
        [SerializeField] private Sprite _cardBackSprite;

        [Header("Timing")]
        [SerializeField] [Range(0.3f, 2f)]  private float _aiThinkDelay       = 0.8f;
        [SerializeField] [Range(0.3f, 2f)]  private float _trickResolvePause  = 1.0f;

        [Tooltip("How long an AI 'thinks' before each bid/pass. Also the settle " +
                 "pause after every seat's bid/pass so their sounds don't " +
                 "overlap. Temporary pacing until the timer system lands.")]
        [SerializeField] [Range(0.5f, 3f)]  private float _bidStepDelay       = 1.2f;

        [Header("Hand Fan Layout")]
        [Tooltip("Angle between adjacent cards in degrees. Controls density regardless of hand size.")]
        [SerializeField] [Range(1f, 15f)] private float _fanAnglePerCard = 4f;
        [Tooltip("Maximum total spread in degrees — prevents wide gaps with few cards.")]
        [SerializeField] [Range(5f, 60f)]  private float _fanMaxAngle    = 28f;
        [Tooltip("Radius of the imaginary circle the cards sit on. Larger = flatter arc.")]
        [SerializeField] [Range(800f, 4000f)] private float _fanRadius   = 2200f;
        [Tooltip("Vertical offset so cards peek up from the bottom of the screen.")]
        [SerializeField] private float _fanYOffset = 120f;

        [Header("Game Rules")]
        [SerializeField] private bool _mustRaiseWhenFollowing = false;

        [Header("Sort / Shuffle")]
        [SerializeField] private bool _autoSort = false;
        [Tooltip("The auto-sort toggle button's highlight overlay — turns gold when ON.")]
        [SerializeField] private UnityEngine.UI.Image _autoSortHighlight;
        [SerializeField] private MatchManager _matchManager;

        [Header("Logging")]
        [SerializeField] private GameLogger _gameLogger;

        [Header("Keyboard Input")]
        [SerializeField] private KeyboardInputHandler _keyboardInput;

        [Header("Audio")]
        [SerializeField] private SoundManager _sound;

        [Header("Scene Navigation")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        // ── Runtime state ─────────────────────────────────────────────────────

        // Seat model: seat 0 is ALWAYS the human; seats 1..N-1 are AI.
        // These arrays are sized by MatchConfig.PlayerCount at round start, so
        // the same code drives 1v1 (2 seats) and 2v2 (4 seats).
        private Player[]            _players;
        private IPlayerController[] _controllers;
        private int                 _playerCount;

        // Convenience accessors for seat 0 (the human) — the UI only ever drives
        // this seat directly, so keeping named getters avoids churn in the many
        // places that touch the human's participant/views.
        private RoundParticipant _humanParticipant => _participants != null ? _participants[0] : null;

        // Per-round
        private MatchConfig         _config;
        private RoundParticipant[]  _participants;      // one per seat
        private LegalMoveEvaluator  _legalMoveEvaluator;

        // Core's round scorer — the single source of truth for VP deltas. The
        // Unity layer builds a RoundResult and asks this; it never recomputes
        // the formula itself.
        private readonly Core.Game.Scoring.IRoundScorer _scorer
            = new Core.Game.Scoring.StandardRoundScorer();
        private Deck                _deck;
        private Trick               _currentTrick;
        private Suit?               _trump;
        private int                 _leaderIndex;
        private int                 _trickCount;
        private bool                _firstPlayOfRound;

        // Bidding state
        private int?  _winningBid;
        private int   _bidWinnerSeat; // 0 = human, 1 = AI

        private readonly List<CardView>   _humanCardViews  = new List<CardView>();

        // Face-down card-back views per NON-HUMAN seat, keyed by seat index
        // (1..N-1). Each list holds one back per card still in that seat's hand,
        // so playing a card removes a back from the correct fan.
        private readonly Dictionary<int, List<GameObject>> _opponentCardBacks
            = new Dictionary<int, List<GameObject>>();
        // Tracks the display order of human cards independently of Hand.Cards order.
        // Sort/shuffle reorder this list; new cards drawn are appended at the end.
        private readonly List<Card>       _handDisplayOrder = new List<Card>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            // The mode chosen in the menu (defaults to OneVsOne if the scene was
            // entered directly). It fixes the player count and team layout for
            // the whole match, so players/controllers are built here, once.
            GameMode mode = GameSelection.SelectedMode;
            var ruleOptions = new RuleSetFactory.Options
            {
                MustRaiseWhenFollowing = _mustRaiseWhenFollowing
            };
            _config      = GameModes.DefaultConfig(mode, ruleOptions);
            _playerCount = _config.PlayerCount;

            // Build the scoreboard: one slot per team, in team-index order. The
            // slot name is the team's label — for individual play it's the seat
            // name ("You" / "Opponent A"); for 2v2 it's "Your Team"/"Opponents".
            int[] teams = _config.TeamOfSeat.Distinct().OrderBy(t => t).ToArray();
            var slots = teams.Select(t => new ScoreHUD.Slot(t, TeamDisplayName(t))).ToArray();
            _scoreHUD?.SetScoreboard(slots);
            // MatchManager uses the SAME slots/order so its match-score labels
            // line up with the ScoreHUD's running-score labels.
            _matchManager?.SetScoreboard(slots, MyTeamIndex());

            // Seat 0 = human; every other seat = its own AI controller.
            _players     = new Player[_playerCount];
            _controllers = new IPlayerController[_playerCount];
            for (int seat = 0; seat < _playerCount; seat++)
            {
                _players[seat]     = new Player(SeatName(seat));
                _controllers[seat] = seat == 0
                    ? (IPlayerController)_humanController
                    : new GreedyHeuristicController();
            }

            _gameLogger?.LogMatchStart();
            _roundNumber  = 0;
            StartRound();
        }

        /// <summary>
        /// Display name for a seat. Seat 0 is the human ("You"); the other seats
        /// are AI. In team modes seat 2 is the human's partner (across the
        /// table), so it is named as a teammate rather than a plain opponent.
        /// </summary>
        private string SeatName(int seat)
        {
            if (seat == 0) return "You";
            if (_playerCount == 2) return "AI";

            // 2v2 (team play): seat 2 is the human's partner across the table;
            // seats 1 & 3 are the opponents.
            if (_config != null && _config.IsTeamPlay)
            {
                if (seat == 2) return "Partner";
                return seat == 1 ? "Opponent A" : "Opponent B";
            }

            // Individual play (1v1v1, 4-player free-for-all): every non-human
            // seat is an independent opponent, labelled A, B, C in seat order.
            return $"Opponent {(char)('A' + seat - 1)}";
        }

        private int _roundNumber = 0;

        // ── Round setup ───────────────────────────────────────────────────────

        private void StartRound()
        {
            _roundNumber++;
            _trump          = null;
            _leaderIndex    = 0;
            _trickCount     = 0;
            _winningBid     = null;
            _bidWinnerSeat  = 0;
            _firstPlayOfRound = true;

            var ruleOptions = new RuleSetFactory.Options
            {
                MustRaiseWhenFollowing = _mustRaiseWhenFollowing
            };

            // Fresh participants for this round (one per seat). _config and the
            // player/controller arrays were built once in Start() from the
            // selected mode and stay fixed for the whole match.
            _participants = new RoundParticipant[_playerCount];
            for (int seat = 0; seat < _playerCount; seat++)
                _participants[seat] = new RoundParticipant(_players[seat]);

            // Draw-pile modes (1v1) use the draw-phase ruleset; no-draw-pile
            // modes (2v2 / draft) apply follow-suit & trump from trick one.
            var ruleSet = _config.DealStrategy == DealStrategy.DrawPilePerTrick
                ? RuleSetFactory.DrawPileRuleSet(ruleOptions)
                : RuleSetFactory.NoDrawPile(ruleOptions);
            _legalMoveEvaluator = new LegalMoveEvaluator(ruleSet,
                msg => Debug.LogWarning("[LegalMoves] " + msg));

            _deck = new Deck();
            _deck.Shuffle();
            _sound?.PlayDeckShuffle();

            // Clear the display order BEFORE dealing: DealCards repopulates it
            // for the human's new hand, so the later ApplyAutoSort() has cards to
            // sort. (Clearing it after the deal would wipe the new hand and the
            // auto-sort toggle would silently stop working on fresh rounds.)
            _handDisplayOrder.Clear();

            for (int seat = 0; seat < _playerCount; seat++)
                DealCards(_config.CardsPerPlayer, _participants[seat]);

            _gameLogger?.LogRoundStart(_roundNumber);
            _gameLogger?.LogDeal(
                _humanParticipant.Hand.ToString(),
                $"{_config.CardsPerPlayer} cards (face down) × {_playerCount - 1} opponents",
                _deck.Count);

            ClearAllCardViews();
            _trickAreaView?.ClearTrick();
            _scoreHUD?.HideResult();
            _scoreHUD?.SetTrump(null);
            _scoreHUD?.SetDeckCount(_deck.Count);
            // Reset every team's running score label to 0 for the new round.
            foreach (int t in _config.TeamOfSeat.Distinct())
                _scoreHUD?.SetTeamPoints(t, 0);

            ApplyAutoSort();
            RebuildHumanHand();
            RebuildAIHand();

            StartCoroutine(RoundLoop());
        }

        // ── Round loop ────────────────────────────────────────────────────────

        private IEnumerator RoundLoop()
        {
            // 1. Bidding phase
            yield return StartCoroutine(BiddingPhase());

            // 2. Bid winner declares trump
            yield return StartCoroutine(DeclareTrumpPhase());

            // 2b. Reveal-and-draft ("surprises" mode) — only when the config says
            // so. Trump is already declared, so drafting can be trump-aware.
            if (_config.DealStrategy == DealStrategy.RevealAndDraft)
                yield return StartCoroutine(DraftPhase());

            // 3. Play all tricks
            yield return StartCoroutine(TrickLoop());

            // 4. Score — delegate entirely to Core. Build the per-team scoring
            // units and a RoundResult, then let StandardRoundScorer compute the
            // VP deltas. The Unity layer no longer duplicates the formula.
            ScoringUnit[] units = BuildScoringUnits();
            RoundResult   result = BuildRoundResult(units);
            IReadOnlyDictionary<int, int> deltas = _scorer.ScoreRound(result);

            if (_matchManager != null)
            {
                // Hand the Core-computed deltas straight to the match tracker.
                _matchManager.ReportRoundResult(deltas);

                // Build one result-banner row per team, in team-index order.
                int[] teams = _config.TeamOfSeat.Distinct().OrderBy(t => t).ToArray();
                var rows = teams.Select(t => new ScoreHUD.ResultRow(
                    teamIndex:   t,
                    totalPoints: units.Where(u => u.TeamIndex == t).Sum(u => u.TotalPoints),
                    vpDelta:     deltas.TryGetValue(t, out int d) ? d : 0,
                    matchScore:  _matchManager.TeamScore(t),
                    wonBid:      result.BidWinnerTeam == t)).ToArray();

                _scoreHUD?.ShowResult(rows, _winningBid);

                // Round win/lose feedback: positive VP for the human's team this
                // round = won, otherwise lost. Skipped when the match just ended,
                // so the match fanfare (played by MatchManager) isn't stepped on.
                if (!_matchManager.IsMatchOver)
                {
                    int myDelta = deltas.TryGetValue(MyTeamIndex(), out int md) ? md : 0;
                    if (myDelta > 0) _sound?.PlayRoundWon();
                    else             _sound?.PlayRoundLost();
                }

                // Wait so the player can read the result.
                yield return new WaitForSeconds(3f);

                if (_matchManager.IsMatchOver)
                    yield break;

                StartRound();
            }
            else
            {
                // No match manager: still show a per-team banner with zero match
                // scores and no deltas.
                int[] teams = _config.TeamOfSeat.Distinct().OrderBy(t => t).ToArray();
                var rows = teams.Select(t => new ScoreHUD.ResultRow(
                    teamIndex:   t,
                    totalPoints: units.Where(u => u.TeamIndex == t).Sum(u => u.TotalPoints),
                    vpDelta:     deltas.TryGetValue(t, out int d) ? d : 0,
                    matchScore:  0,
                    wonBid:      result.BidWinnerTeam == t)).ToArray();
                _scoreHUD?.ShowResult(rows, _winningBid);
            }
        }

        // ── Scoring (delegates to Core) ───────────────────────────────────────

        /// <summary>The team index the human (seat 0) belongs to.</summary>
        private int MyTeamIndex() => _config.TeamOfSeat[0];

        /// <summary>
        /// Display name for a whole team (scoreboard slot). In individual play a
        /// team is one seat, so its name is that seat's name ("You" / "Opponent
        /// A"). In 2v2 the two teams are the human's side and the opponents'.
        /// </summary>
        private string TeamDisplayName(int team)
        {
            if (_config.IsTeamPlay)
                return team == MyTeamIndex() ? "Your Team" : "Opponents";

            // Individual: find the (single) seat on this team and use its name.
            for (int seat = 0; seat < _playerCount; seat++)
                if (_config.TeamOfSeat[seat] == team)
                    return SeatName(seat);
            return $"Team {team}";
        }

        /// <summary>
        /// Builds the per-team <see cref="ScoringUnit"/>s from this round's
        /// participants and team layout — the same construction RoundEngine does
        /// internally. One unit per distinct team; each participant joins its
        /// seat's team unit (so individual play = one member per unit, team play
        /// pools members). Used to feed Core's scorer.
        /// </summary>
        private ScoringUnit[] BuildScoringUnits()
        {
            var unitByTeam = new Dictionary<int, ScoringUnit>();
            for (int seat = 0; seat < _playerCount; seat++)
            {
                int team = _config.TeamOfSeat[seat];
                if (!unitByTeam.TryGetValue(team, out ScoringUnit unit))
                {
                    unit = new ScoringUnit(team);
                    unitByTeam[team] = unit;
                }
                unit.AddMember(_participants[seat]);
            }
            return unitByTeam.Values.ToArray();
        }

        /// <summary>
        /// Packages this round's outcome into a Core <see cref="RoundResult"/>
        /// so Core's <see cref="StandardRoundScorer"/> can compute the VP deltas.
        /// The Unity layer no longer re-implements the scoring formula.
        /// </summary>
        private RoundResult BuildRoundResult(ScoringUnit[] units)
        {
            int bidWinnerTeam = _bidWinnerSeat >= 0
                ? _config.TeamOfSeat[_bidWinnerSeat]
                : -1;
            return new RoundResult(units, _winningBid, _bidWinnerSeat, bidWinnerTeam, _trump);
        }

        // ── Bidding phase ─────────────────────────────────────────────────────

        private IEnumerator BiddingPhase()
        {
            int highBid      = 0;
            int highSeat     = -1;
            bool[] active    = new bool[_playerCount];
            for (int s = 0; s < _playerCount; s++) active[s] = true;
            int activeCount  = _playerCount;

            // Opening bidder rotates each round. Delegate to Core's shared rule
            // (MatchConfig.StartingBidderSeat) rather than re-deriving the
            // formula here — the Unity layer previously hardcoded seat 0 (the
            // human always opened), which diverged from Core. Seat 0 is always
            // the human, so the per-seat "is this the human?" checks below still
            // hold regardless of who opens.
            int startingBidder = _config.StartingBidderSeat(_roundNumber);
            int lastPasser     = startingBidder;

            int seat = startingBidder;

            while (activeCount > 1 || (activeCount >= 1 && highSeat < 0))
            {
                if (active[seat])
                {
                    if (activeCount == 1 && highSeat == seat) break;

                    var ctx = new BiddingContext(highBid > 0 ? highBid : (int?)null, _config.MaxBid);
                    Bid bid = Bid.Pass;

                    if (seat == 0) // human
                    {
                        bool bidDone = false;
                        _humanController.BeginBid(ctx, b => { bid = b; bidDone = true; });
                        _bidPanel?.Show(ctx, _humanController, () => { });
                        _keyboardInput?.BeginBidInput();
                        yield return new WaitUntil(() => bidDone);
                        _keyboardInput?.EndBidInput();
                        _bidPanel?.Hide();
                    }
                    else // AI
                    {
                        yield return new WaitForSeconds(_bidStepDelay);
                        bid = _controllers[seat].PlaceBid(_participants[seat], ctx);
                    }

                    bool valid = !bid.IsPass
                                 && bid.Value >= ctx.MinLegalBid
                                 && bid.Value <= _config.MaxBid;

                    if (valid)
                    {
                        highBid  = bid.Value;
                        highSeat = seat;
                        _sound?.PlayBidPlaced();
                        _scoreHUD?.ShowBidInfo($"{SeatName(seat)} bid {highBid}");
                        _gameLogger?.LogBid(SeatName(seat), highBid);
                    }
                    else
                    {
                        active[seat] = false;
                        activeCount--;
                        lastPasser   = seat;
                        _sound?.PlayBidPassed();
                        _scoreHUD?.ShowBidInfo($"{SeatName(seat)} passed");
                        _gameLogger?.LogBid(SeatName(seat), null);
                    }

                    // Settle pause after this seat's bid/pass so its sound and
                    // the HUD update land before the next seat acts — stops
                    // consecutive bid/pass sounds from overlapping. (Temporary
                    // pacing until the timer system replaces it.)
                    yield return new WaitForSeconds(_bidStepDelay);
                }

                if (activeCount == 0) break;
                seat = (seat + 1) % _playerCount;
            }

            if (highSeat < 0)
            {
                highSeat = lastPasser;
                highBid  = 1;
                _scoreHUD?.ShowBidInfo($"{SeatName(highSeat)} forced to bid 1");
                _gameLogger?.LogForcedBid(SeatName(highSeat));
            }

            _winningBid    = highBid;
            _bidWinnerSeat = highSeat;
            _leaderIndex   = highSeat;

            string bidWinnerName = SeatName(highSeat);
            _scoreHUD?.ShowPersistentInfo($"{bidWinnerName} won the bid at {highBid}");
            _gameLogger?.LogBidWinner(bidWinnerName, highBid);
            Debug.Log($"[Cruce] Bid won by {bidWinnerName} (seat {highSeat}) at {highBid}");
            yield return new WaitForSeconds(1f);
        }

        // ── Trump declaration ─────────────────────────────────────────────────

        private IEnumerator DeclareTrumpPhase()
        {
            if (_bidWinnerSeat == 0) // human declares
            {
                Suit chosen = default;
                bool done   = false;
                _humanController.BeginDeclareTrump(suit => { chosen = suit; done = true; });
                _trumpPickerPanel?.Show(suit => { _humanController.SubmitTrump(suit); });
                yield return new WaitUntil(() => done);
                _trumpPickerPanel?.Hide();
                _trump = chosen;
            }
            else // AI (any non-human seat) declares
            {
                yield return new WaitForSeconds(_aiThinkDelay * 0.5f);
                _trump = _controllers[_bidWinnerSeat].DeclareTrump(_participants[_bidWinnerSeat]);
            }

            string suitName = SuitDisplayName(_trump);
            string declarer = _bidWinnerSeat == 0
                ? "You declare"
                : $"{SeatName(_bidWinnerSeat)} declares";
            _scoreHUD?.ShowPersistentInfo($"{declarer} trump: {suitName}");
            _scoreHUD?.SetTrump(_trump);
            _trickAreaView?.SetTrump(_trump);
            ApplyTrumpHighlights();
            _gameLogger?.LogTrumpDeclared(SeatName(_bidWinnerSeat), suitName);
            Debug.Log($"[Cruce] Trump declared: {_trump}");
            yield return new WaitForSeconds(1.5f);
            _scoreHUD?.ClearPersistentInfo();
        }

        // ── Draft phase (reveal-and-draft / "surprises") ──────────────────────

        // Clickable CardViews for the revealed leftover, kept so they can be
        // cleared when the draft ends.
        private readonly List<CardView> _draftCardViews = new List<CardView>();

        /// <summary>
        /// Reveals the leftover cards and lets each seat draft one — bid winner
        /// first, then clockwise — mirroring RoundEngine.RunDraftIfNeeded. The
        /// human picks by clicking a revealed card; AI seats use their
        /// controller. Trump is already declared, so picks can be trump-aware.
        /// </summary>
        private IEnumerator DraftPhase()
        {
            // Reveal everything left in the deck (public information). For
            // RevealAndDraft the leftover count equals the player count, so each
            // seat drafts exactly one.
            var available = new List<Card>(_deck.Draw(_deck.Count));
            Debug.Log($"[Cruce] Draft reveal: {string.Join(", ", available)}");

            _scoreHUD?.ShowPersistentInfo("Draft: pick one of the revealed cards");
            BuildDraftReveal(available);
            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < _playerCount && available.Count > 0; i++)
            {
                int seat = (_bidWinnerSeat + i) % _playerCount;
                RoundParticipant p = _participants[seat];
                Card picked;

                if (seat == 0) // human picks via the revealed CardViews
                {
                    HighlightDraftChoices(true);
                    bool done = false;
                    Card chosen = default;
                    _humanController.BeginDraft(available, c => { chosen = c; done = true; });
                    yield return new WaitUntil(() => done);
                    HighlightDraftChoices(false);
                    picked = chosen;
                }
                else // AI picks via its controller
                {
                    yield return new WaitForSeconds(_aiThinkDelay * 0.5f);
                    picked = _controllers[seat].ChooseDraftCard(p, available, _trump.Value);
                }

                // Defensive: if a controller returns a card not on offer, take
                // the first available instead (same guard as RoundEngine).
                if (!available.Contains(picked))
                    picked = available[0];

                available.Remove(picked);
                p.Hand.Add(picked);
                _sound?.PlayCardDraw();
                RemoveDraftRevealCard(picked);

                if (seat == 0)
                {
                    // Track and show the human's new card.
                    _handDisplayOrder.Add(picked);
                    ApplyAutoSort();
                    RebuildHumanHand();
                    ApplyTrumpHighlights();
                }
                else
                {
                    // Refresh that opponent's face-down fan (one more card).
                    RebuildAIHand();
                }

                Debug.Log($"[Cruce] {SeatName(seat)} drafts {picked}");
                yield return new WaitForSeconds(0.4f);
            }

            ClearDraftReveal();
            _scoreHUD?.ClearPersistentInfo();
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Spawns a clickable face-up CardView per revealed card.</summary>
        private void BuildDraftReveal(IReadOnlyList<Card> cards)
        {
            ClearDraftReveal();
            if (_draftRevealArea == null || _cardPrefab == null) return;

            int count = cards.Count;
            float spacing = 140f;
            float startX  = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _draftRevealArea);
                CardView cv   = go.GetComponent<CardView>();
                if (cv == null) continue;

                Card card     = cards[i];
                Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(card) : null;
                cv.Initialise(card, sprite, new Vector2(startX + i * spacing, 0f));
                cv.SetLegal(false);                 // not clickable until it's the human's turn
                cv.OnCardClicked += OnDraftCardClicked;
                _draftCardViews.Add(cv);
            }
        }

        /// <summary>Enables/disables clicking on the revealed draft cards.</summary>
        private void HighlightDraftChoices(bool clickable)
        {
            foreach (CardView cv in _draftCardViews)
                if (cv != null) cv.SetLegal(clickable);
        }

        /// <summary>Routes a click on a revealed card to the human controller.</summary>
        private void OnDraftCardClicked(Card card) => _humanController.SubmitDraft(card);

        /// <summary>Removes the drafted card's view from the reveal row.</summary>
        private void RemoveDraftRevealCard(Card card)
        {
            CardView toRemove = null;
            foreach (CardView cv in _draftCardViews)
                if (cv != null && cv.Card == card) { toRemove = cv; break; }
            if (toRemove == null) return;

            _draftCardViews.Remove(toRemove);
            Destroy(toRemove.gameObject);
        }

        private void ClearDraftReveal()
        {
            foreach (CardView cv in _draftCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _draftCardViews.Clear();
        }

        // ── Trick loop ────────────────────────────────────────────────────────

        private IEnumerator TrickLoop()
        {
            while (AnyHandNonEmpty())
            {
                _trickCount++;
                yield return StartCoroutine(PlayOneTrick());
            }
        }

        /// <summary>True while at least one seat still holds a card.</summary>
        private bool AnyHandNonEmpty()
        {
            for (int seat = 0; seat < _playerCount; seat++)
                if (!_participants[seat].Hand.IsEmpty)
                    return true;
            return false;
        }

        private IEnumerator PlayOneTrick()
        {
            _currentTrick = new Trick();
            _currentTrick.SetTrump(_trump);
            _currentTrick.SetDrawPileCount(_deck.Count);
            _trickAreaView?.ClearTrick();

            _gameLogger?.LogTrickStart(_trickCount, SeatName(_leaderIndex));

            for (int i = 0; i < _playerCount; i++)
            {
                int seat    = (_leaderIndex + i) % _playerCount;
                bool isHuman = (seat == 0);
                bool isLeading = (i == 0);
                bool isOpeningLead = _firstPlayOfRound && isLeading;

                if (isHuman)
                    yield return StartCoroutine(HumanPlays(isLeading, isOpeningLead));
                else
                    yield return StartCoroutine(AIPlays(seat, isLeading, isOpeningLead));
            }

            yield return new WaitForSeconds(_trickResolvePause);
            float trickSoundLen = ResolveTrick();

            // Wait for the trick win/lose sting to finish before drawing, so the
            // draw sound doesn't overlap it (1v1 draw-pile flow). Keep the
            // original 0.3s floor for snappiness when the sting is short/absent,
            // and cap it so an over-long clip can't stall the game.
            float drawDelay = Mathf.Clamp(trickSoundLen, 0.3f, 1.5f);
            yield return new WaitForSeconds(drawDelay);
            DrawFromPile();
        }

        // ── Human turn ────────────────────────────────────────────────────────

        private IEnumerator HumanPlays(bool isLeading, bool isOpeningLead)
        {
            // Marriage announcement (only when leading)
            Suit? announcedSuit = null;
            if (isLeading)
            {
                var marriages = new List<Suit>(_humanParticipant.Hand.MarriageSuits());
                if (isOpeningLead)
                    marriages.RemoveAll(s => !(_trump.HasValue && s == _trump.Value));

                if (marriages.Count > 0)
                {
                    bool done = false;
                    _humanController.BeginAnnounceMarriage(marriages, s => { announcedSuit = s; done = true; });
                    // The panel fires this single callback for both a suit pick
                    // and Skip: a chosen suit has a value, Skip passes null.
                    _marriagePanel?.Show(marriages,
                        suit =>
                        {
                            if (suit.HasValue) _humanController.SubmitMarriage(suit.Value);
                            else               _humanController.SubmitNoMarriage();
                        });
                    yield return new WaitUntil(() => done);
                    _marriagePanel?.Hide();

                    if (announcedSuit.HasValue)
                    {
                        int bonus = _trump.HasValue && announcedSuit.Value == _trump.Value ? 40 : 20;
                        _humanParticipant.AddMarriagePoints(bonus);
                        _sound?.PlayMarriageDeclared();
                        _scoreHUD?.ShowMarriageBonus(bonus);
                        _gameLogger?.LogMarriage("You", announcedSuit.Value.ToString(), bonus);
                        Debug.Log($"[Cruce] Human announces marriage in {announcedSuit.Value} (+{bonus})");
                    }
                }
            }

            // Legal move computation
            var context = new LegalMoveContext(
                _humanParticipant.Hand, _currentTrick, _trump, _deck.Count,
                isOpeningLead: isOpeningLead, announcedSuit: announcedSuit);
            IReadOnlyList<Card> legal = _legalMoveEvaluator.LegalMoves(context);

            // Mark cards
            foreach (CardView cv in _humanCardViews)
            {
                bool isLegal = false;
                foreach (Card c in legal) if (c == cv.Card) { isLegal = true; break; }
                cv.SetLegal(isLegal);
            }

            // Wait for click
            Card chosen   = default;
            bool cardDone = false;
            _humanController.BeginTurn(legal, card => { chosen = card; cardDone = true; });
            _keyboardInput?.BeginCardInput();
            yield return new WaitUntil(() => cardDone);
            _keyboardInput?.EndCardInput();
            _humanController.CancelTurn();

            _gameLogger?.LogCardPlayed("You", chosen.ToString());
            PlayCard(0, _humanParticipant, chosen);
            _firstPlayOfRound = false;
        }

        // ── AI turn ───────────────────────────────────────────────────────────

        private IEnumerator AIPlays(int seat, bool isLeading, bool isOpeningLead)
        {
            yield return new WaitForSeconds(_aiThinkDelay);

            RoundParticipant part = _participants[seat];
            IPlayerController ai   = _controllers[seat];

            // Marriage announcement
            Suit? announcedSuit = null;
            if (isLeading)
            {
                var marriages = new List<Suit>(part.Hand.MarriageSuits());
                if (isOpeningLead)
                    marriages.RemoveAll(s => !(_trump.HasValue && s == _trump.Value));

                if (marriages.Count > 0)
                {
                    announcedSuit = ai.AnnounceMarriage(part, marriages, _trump);
                    if (announcedSuit.HasValue)
                    {
                        int bonus = _trump.HasValue && announcedSuit.Value == _trump.Value ? 40 : 20;
                        part.AddMarriagePoints(bonus);
                        _sound?.PlayMarriageDeclared();
                        _scoreHUD?.ShowMarriageBonus(bonus);
                        _gameLogger?.LogMarriage(SeatName(seat), announcedSuit.Value.ToString(), bonus);
                        Debug.Log($"[Cruce] {SeatName(seat)} announces marriage in {announcedSuit.Value} (+{bonus})");
                    }
                }
            }

            var context = new LegalMoveContext(
                part.Hand, _currentTrick, _trump, _deck.Count,
                isOpeningLead: isOpeningLead, announcedSuit: announcedSuit);
            IReadOnlyList<Card> legal = _legalMoveEvaluator.LegalMoves(context);

            Card chosen = ai.ChooseCard(part, legal, _currentTrick);
            _gameLogger?.LogCardPlayed(SeatName(seat), chosen.ToString());
            PlayCard(seat, part, chosen);
            _firstPlayOfRound = false;
        }

        // ── Shared card play ──────────────────────────────────────────────────

        /// <summary>
        /// Plays <paramref name="card"/> from <paramref name="seat"/> into the
        /// current trick and updates the view: seat 0 (human) removes a face-up
        /// CardView and refans; any other seat removes one face-down back from
        /// that seat's own fan.
        /// </summary>
        private void PlayCard(int seat, RoundParticipant participant, Card card)
        {
            participant.Hand.Remove(card);
            _currentTrick.Add(participant, card);
            _sound?.PlayCardPlaced();

            Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(card) : null;
            _trickAreaView?.AddCardToPile(sprite);

            if (seat == 0)
                RemoveCardViewFromHand(card);
            else
                RemoveOpponentCardBack(seat);

            _scoreHUD?.SetDeckCount(_deck.Count);
        }

        // ── Trick resolution & draw ───────────────────────────────────────────

        /// <summary>
        /// Resolves the current trick and returns the length (seconds) of the
        /// win/lose sting it played, so the caller can wait for it to finish
        /// before the next sound (the draw) starts. 0 if nothing played.
        /// </summary>
        private float ResolveTrick()
        {
            RoundParticipant winner = _currentTrick.DetermineWinner(_trump);
            int pts = _currentTrick.PointValue;
            winner.AddCapturedPoints(pts);

            // Next leader is the winning seat (0..N-1), not just human/AI.
            _leaderIndex = Array.IndexOf(_participants, winner);

            // "My side won" = the winning seat is on the human's team. Drives the
            // positive/negative winner flash and keeps the HUD meaningful before
            // the team-HUD relabel in a later step.
            int myTeam = MyTeamIndex();
            bool myTeamWon = _config.TeamOfSeat[_leaderIndex] == myTeam;
            _trickAreaView?.FlashWinner(myTeamWon);
            float trickSoundLen = _sound != null
                ? (myTeamWon ? _sound.PlayTrickWon() : _sound.PlayTrickLost())
                : 0f;

            // Update every team's running total on the HUD (captured + marriage).
            // Display only; authoritative round scoring happens at round end.
            var running = new Dictionary<int, int>();
            for (int s = 0; s < _playerCount; s++)
            {
                int team = _config.TeamOfSeat[s];
                int seatPts = _participants[s].CapturedPoints + _participants[s].MarriagePoints;
                running[team] = (running.TryGetValue(team, out int v) ? v : 0) + seatPts;
            }
            foreach (var kv in running)
                _scoreHUD?.SetTeamPoints(kv.Key, kv.Value);

            Debug.Log($"[Cruce] Trick {_trickCount}: {winner.Player.Name} wins {pts} pts.");
            return trickSoundLen;
        }

        private void DrawFromPile()
        {
            // Only draw-pile modes (1v1) refill hands between tricks. DealAll /
            // RevealAndDraft (2v2 / draft) have no draw pile, so this is a no-op
            // for them — mirrors RoundEngine.DrawFromPile.
            if (_config.DealStrategy != DealStrategy.DrawPilePerTrick || _deck.Count == 0)
                return;

            // Every seat draws one, starting with the trick winner (now leader),
            // going clockwise. Winner draws first.
            for (int i = 0; i < _playerCount && _deck.Count > 0; i++)
            {
                int seat = (_leaderIndex + i) % _playerCount;
                Card drawn = _deck.Draw();
                _participants[seat].Hand.Add(drawn);

                // Track the human's new card at the end of the display order.
                if (seat == 0)
                    _handDisplayOrder.Add(drawn);
            }

            _scoreHUD?.SetDeckCount(_deck.Count);
            _sound?.PlayCardDraw();
            ApplyAutoSort();
            RebuildHumanHand();
            RebuildAIHand();
            ApplyTrumpHighlights();
        }

        // ── Hand view management ──────────────────────────────────────────────

        private void DealCards(int count, RoundParticipant participant)
        {
            var dealt = _deck.Draw(count);
            participant.Hand.AddRange(dealt);
            if (participant == _humanParticipant)
                _handDisplayOrder.AddRange(dealt);
        }

        private void RebuildHumanHand()
        {
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            if (_humanHandArea == null || _cardPrefab == null) return;

            // Use _handDisplayOrder to preserve sort/shuffle across rebuilds.
            // Sync it with the actual hand first (remove any cards no longer held).
            _handDisplayOrder.RemoveAll(c => !_humanParticipant.Hand.Contains(c));
            // Add any cards in hand that aren't tracked yet (safety net).
            foreach (Card c in _humanParticipant.Hand.Cards)
                if (!_handDisplayOrder.Contains(c)) _handDisplayOrder.Add(c);

            int count = _handDisplayOrder.Count;
            if (count == 0) return;

            // ── Fan layout ───────────────────────────────────────────────────
            // Each card sits on a circle of _fanRadius centred at (0, -_fanRadius)
            // relative to the HumanHandArea pivot.
            // angleDeg=0 → card straight up at (0,0); negative angles → left; positive → right.

            float halfAngle = FanAngleForCount(count) * 0.5f;
            float angleStep = count > 1 ? FanAngleForCount(count) / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _humanHandArea);
                CardView cv   = go.GetComponent<CardView>();
                if (cv == null) continue;

                // Angle from centre: left cards negative, right cards positive.
                float angleDeg = count > 1 ? -halfAngle + i * angleStep : 0f;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Circle centre is at (0, -_fanRadius) in local space.
                // Card position = centre + radius * (sin(angle), cos(angle))
                float x = _fanRadius * Mathf.Sin(angleRad);
                float y = -_fanRadius + _fanRadius * Mathf.Cos(angleRad) + _fanYOffset;

                Vector2 basePos = new Vector2(x, y);

                Card card = _handDisplayOrder[i];
                Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(card) : null;
                cv.Initialise(card, sprite, basePos, angleDeg);
                cv.OnCardClicked += _humanController.HandleCardClicked;
                cv.OnIllegalClickAttempt += () => _sound?.PlayIllegalMove();
                _humanCardViews.Add(cv);

                // Left cards render under right cards.
                go.transform.SetSiblingIndex(i);
            }

            // Keep keyboard handler in sync with the new card views.
            _keyboardInput?.SetCardViews(_humanCardViews);
        }

        /// <summary>
        /// Rebuilds the face-down card-back fans for every non-human seat, each
        /// into its own area in <see cref="_opponentHandAreas"/> (indexed by
        /// non-human seat order: element 0 = seat 1, element 1 = seat 2, ...).
        ///
        /// All seats use the same downward-arc fan formula; a per-seat base
        /// rotation then spins the whole fan so it faces the table centre —
        /// 0° for the TOP seat (fan hangs down), +90° for the LEFT seat, -90°
        /// for the RIGHT seat. For 1v1 there is a single opponent area and no
        /// extra rotation, so the fan is identical to the original.
        /// </summary>
        private void RebuildAIHand()
        {
            // Destroy every existing back and clear the per-seat lists.
            foreach (var kv in _opponentCardBacks)
                foreach (GameObject go in kv.Value)
                    if (go != null) Destroy(go);
            _opponentCardBacks.Clear();

            if (_cardPrefab == null || _participants == null) return;

            // One area per non-human seat, in seat order (seat 1 → element 0).
            for (int seat = 1; seat < _playerCount; seat++)
            {
                int areaIndex = seat - 1;
                RectTransform area = (_opponentHandAreas != null
                                      && areaIndex < _opponentHandAreas.Length)
                    ? _opponentHandAreas[areaIndex]
                    : null;
                if (area == null) continue;

                var backs = new List<GameObject>();
                _opponentCardBacks[seat] = backs;

                BuildCardBackFan(seat, area, backs);
            }
        }

        /// <summary>
        /// Lays out one seat's face-down fan into <paramref name="area"/> and
        /// records the created backs in <paramref name="backs"/>.
        /// </summary>
        private void BuildCardBackFan(int seat, RectTransform area, List<GameObject> backs)
        {
            int count = _participants[seat].Hand.Count;
            if (count == 0) return;

            float halfAngle = FanAngleForCount(count) * 0.35f;
            float angleStep = count > 1 ? (halfAngle * 2f) / (count - 1) : 0f;
            float radius    = _fanRadius * 0.5f;

            // Spin the whole fan so it faces the table centre.
            float seatRotation = SeatFanRotation(seat);
            float seatRad      = seatRotation * Mathf.Deg2Rad;
            float cosR = Mathf.Cos(seatRad);
            float sinR = Mathf.Sin(seatRad);

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, area);

                CardView cv = go.GetComponent<CardView>();
                if (cv != null) cv.enabled = false;

                UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
                if (img != null && _cardBackSprite != null) img.sprite = _cardBackSprite;

                UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = false;

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float angleDeg = count > 1 ? -halfAngle + i * angleStep : 0f;
                    float angleRad = angleDeg * Mathf.Deg2Rad;

                    // Base fan (same as the original single-opponent fan): circle
                    // centre ABOVE the pivot, so cards arc downward.
                    float x =  radius * Mathf.Sin(angleRad);
                    float y =  radius - radius * Mathf.Cos(angleRad);
                    // y positive = downward from the pivot (which sits at top).
                    Vector2 basePos = new Vector2(x, -y - _fanYOffset * 0.3f);

                    // Rotate the whole fan (position + tilt) by the seat's angle
                    // so left/right seats read as side hands.
                    Vector2 pos = new Vector2(
                        basePos.x * cosR - basePos.y * sinR,
                        basePos.x * sinR + basePos.y * cosR);

                    rt.anchoredPosition = pos;
                    rt.sizeDelta        = new Vector2(120f, 180f);
                    // Follow the arc tangent, then add the seat's base rotation.
                    rt.localRotation    = Quaternion.Euler(0f, 0f, -angleDeg + seatRotation);
                }

                backs.Add(go);
            }
        }

        /// <summary>
        /// The base rotation (degrees) applied to a seat's whole card-back fan so
        /// it faces the table centre. Clockwise screen layout:
        /// seat 1 = LEFT (+90°), seat 2 = TOP (0°), seat 3 = RIGHT (-90°).
        /// In 1v1 the single opponent sits across the top (0°), unchanged.
        /// </summary>
        private float SeatFanRotation(int seat)
        {
            if (_playerCount == 2) return 0f;   // 1v1: opponent across the top.

            // 2v2 (team play): left / top (partner) / right.
            if (_config != null && _config.IsTeamPlay)
            {
                switch (seat)
                {
                    case 1:  return  90f;  // left
                    case 2:  return   0f;  // top (partner)
                    case 3:  return -90f;  // right
                    default: return   0f;
                }
            }

            // 1v1v1 (individual, 2 opponents): one on the left, one on the right.
            if (_playerCount == 3)
                return seat == 1 ? 90f : -90f;   // seat 1 left, seat 2 right

            // 4-player free-for-all (individual): left / top / right.
            switch (seat)
            {
                case 1:  return  90f;  // left
                case 2:  return   0f;  // top
                case 3:  return -90f;  // right
                default: return   0f;
            }
        }

        private void RemoveCardViewFromHand(Card card)
        {
            CardView toRemove = null;
            foreach (CardView cv in _humanCardViews)
                if (cv != null && cv.Card == card) { toRemove = cv; break; }
            if (toRemove == null) return;

            _humanCardViews.Remove(toRemove);
            _handDisplayOrder.Remove(card);
            Destroy(toRemove.gameObject);

            RefanHumanHand();
        }

        /// <summary>
        /// Recalculates fan positions for all remaining CardViews and updates
        /// their base positions so they animate smoothly to the new layout.
        /// </summary>
        /// <summary>
        /// Applies trump highlighting to all current CardViews based on _trump.
        /// Call after trump is declared and after each hand rebuild.
        /// </summary>
        private void ApplyTrumpHighlights()
        {
            foreach (CardView cv in _humanCardViews)
            {
                if (cv == null) continue;
                bool isTrump = _trump.HasValue && cv.Card.Suit == _trump.Value;
                cv.SetTrump(isTrump);
            }
        }

        private void RefanHumanHand()        {
            int count = _humanCardViews.Count;
            if (count == 0) return;

            float halfAngle = FanAngleForCount(count) * 0.5f;
            float angleStep = count > 1 ? FanAngleForCount(count) / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                CardView cv = _humanCardViews[i];
                if (cv == null) continue;

                float angleDeg = count > 1 ? -halfAngle + i * angleStep : 0f;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                float x = _fanRadius * Mathf.Sin(angleRad);
                float y = -_fanRadius + _fanRadius * Mathf.Cos(angleRad) + _fanYOffset;

                cv.SetBasePosition(new Vector2(x, y));
                cv.SetBaseRotation(angleDeg);

                // Left cards render under right cards — sibling index = position in list.
                cv.transform.SetSiblingIndex(i);
            }
        }

        /// <summary>
        /// Removes one face-down back from the given seat's fan (the last one,
        /// since backs are interchangeable). No-op if the seat has no backs.
        /// </summary>
        private void RemoveOpponentCardBack(int seat)
        {
            if (!_opponentCardBacks.TryGetValue(seat, out List<GameObject> backs)
                || backs.Count == 0)
                return;

            int last = backs.Count - 1;
            if (backs[last] != null) Destroy(backs[last]);
            backs.RemoveAt(last);
        }

        private void ClearAllCardViews()
        {
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            foreach (var kv in _opponentCardBacks)
                foreach (GameObject go in kv.Value)
                    if (go != null) Destroy(go);
            _opponentCardBacks.Clear();
        }

        // ── Public button handlers ────────────────────────────────────────────

        public void OnPlayAgainClicked() => StartRound();

        public void OnMainMenuClicked() => SceneManager.LoadScene(_mainMenuScene);

        /// <summary>
        /// Sorts the hand display by suit (Green → Bell → Red → Acorn) then by
        /// strength descending within each suit (Ace first, Nine last).

        /// <summary>Toggles auto-sort on/off and updates the highlight.</summary>
        public void OnAutoSortToggleClicked()
        {
            _autoSort = !_autoSort;
            RefreshAutoSortButton();
            if (_autoSort)
            {
                _sound?.PlayHandSort();
                ApplyAutoSort();
                RebuildHumanHand();
                ApplyTrumpHighlights();
            }
        }

        /// <summary>
        /// Shuffles the hand display randomly.
        /// Called by the Shuffle button in the scene.
        /// </summary>
        public void OnShuffleHandClicked()
        {
            _sound?.PlayHandShuffle();
            System.Random rng = new System.Random();
            for (int i = _handDisplayOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Card tmp = _handDisplayOrder[i];
                _handDisplayOrder[i] = _handDisplayOrder[j];
                _handDisplayOrder[j] = tmp;
            }
            RebuildHumanHand();
            ApplyTrumpHighlights();
        }

        /// <summary>Suit display order: Green=0, Bell=1, Red=2, Acorn=3.</summary>
        private static int SuitSortOrder(Suit suit)
        {
            switch (suit)
            {
                case Suit.Green: return 0;
                case Suit.Bell:  return 1;
                case Suit.Red:   return 2;
                case Suit.Acorn: return 3;
                default:         return 4;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Applies sort if auto-sort is on. Call after any hand change.</summary>
        private void ApplyAutoSort()
        {
            if (!_autoSort) return;
            _handDisplayOrder.Sort((a, b) =>
            {
                int suitA = SuitSortOrder(a.Suit);
                int suitB = SuitSortOrder(b.Suit);
                if (suitA != suitB) return suitA.CompareTo(suitB);
                return b.Strength.CompareTo(a.Strength);
            });
        }

        /// <summary>Updates the auto-sort button's highlight to reflect current state.</summary>
        private void RefreshAutoSortButton()
        {
            if (_autoSortHighlight != null)
                _autoSortHighlight.color = _autoSort
                    ? new Color(1f, 0.85f, 0f, 0.45f)  // same gold as trump tint
                    : Color.clear;
        }

        /// <summary>
        /// Returns the actual total fan angle for a given card count.
        /// Scales per-card but never exceeds _fanMaxAngle.
        /// </summary>
        private float FanAngleForCount(int count)
        {
            if (count <= 1) return 0f;
            return Mathf.Min(_fanAnglePerCard * (count - 1), _fanMaxAngle);
        }

        private static string SuitDisplayName(Suit? suit)
        {
            if (!suit.HasValue) return "—";
            switch (suit.Value)
            {
                case Suit.Green: return "Verde";
                case Suit.Red:   return "Rosu";
                case Suit.Acorn: return "Ghinda";
                case Suit.Bell:  return "Duba";
                default:         return suit.ToString();
            }
        }
    }
}
