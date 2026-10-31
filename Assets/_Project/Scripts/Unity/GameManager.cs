using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private RectTransform      _aiHandArea;
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

        [Header("Hand Fan Layout")]
        [Tooltip("Total angle of the fan in degrees. 40–60 works well for 8 cards.")]
        [SerializeField] [Range(10f, 120f)] private float _fanTotalAngle = 50f;
        [Tooltip("Radius of the imaginary circle the cards sit on. Larger = flatter arc.")]
        [SerializeField] [Range(800f, 4000f)] private float _fanRadius   = 1800f;
        [Tooltip("Vertical offset so cards peek up from the bottom of the screen.")]
        [SerializeField] private float _fanYOffset = 80f;

        [Header("Game Rules")]
        [SerializeField] private bool _mustRaiseWhenFollowing = false;

        [Header("Match Scoring")]
        [SerializeField] private MatchManager _matchManager;

        [Header("Logging")]
        [SerializeField] private GameLogger _gameLogger;

        [Header("Scene Navigation")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        // ── Runtime state ─────────────────────────────────────────────────────

        private Player _humanPlayer;
        private Player _aiPlayer;
        private GreedyHeuristicController _aiController;

        // Per-round
        private MatchConfig         _config;
        private RoundParticipant    _humanParticipant;
        private RoundParticipant    _aiParticipant;
        private LegalMoveEvaluator  _legalMoveEvaluator;
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
        private readonly List<GameObject> _aiCardBackViews = new List<GameObject>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            _humanPlayer  = new Player("You");
            _aiPlayer     = new Player("AI");
            _aiController = new GreedyHeuristicController();
            _gameLogger?.LogMatchStart();
            _roundNumber  = 0;
            StartRound();
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

            _config = GameModes.DefaultConfig(GameMode.OneVsOne, ruleOptions);

            _humanParticipant = new RoundParticipant(_humanPlayer);
            _aiParticipant    = new RoundParticipant(_aiPlayer);

            var ruleSet = RuleSetFactory.DrawPileRuleSet(ruleOptions);
            _legalMoveEvaluator = new LegalMoveEvaluator(ruleSet,
                msg => Debug.LogWarning("[LegalMoves] " + msg));

            _deck = new Deck();
            _deck.Shuffle();
            DealCards(8, _humanParticipant);
            DealCards(8, _aiParticipant);

            _gameLogger?.LogRoundStart(_roundNumber);
            _gameLogger?.LogDeal(
                _humanParticipant.Hand.ToString(),
                "8 cards (face down)",
                _deck.Count);

            ClearAllCardViews();
            _trickAreaView?.ClearTrick();
            _scoreHUD?.HideResult();
            _scoreHUD?.SetTrump(null);
            _scoreHUD?.SetDeckCount(_deck.Count);
            _scoreHUD?.SetHumanPoints(0);
            _scoreHUD?.SetAIPoints(0);

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

            // 3. Play all tricks
            yield return StartCoroutine(TrickLoop());

            // 4. Score
            int humanPts = _humanParticipant.CapturedPoints + _humanParticipant.MarriagePoints;
            int aiPts    = _aiParticipant.CapturedPoints    + _aiParticipant.MarriagePoints;

            if (_matchManager != null)
            {
                // Compute deltas the same way MatchManager does so we can show them.
                int humanEarned = humanPts / 33;
                int aiEarned    = aiPts    / 33;
                int humanDelta;
                int aiDelta;

                if (_winningBid.HasValue)
                {
                    int bid = _winningBid.Value;
                    if (_bidWinnerSeat == 0) // human won the bid
                    {
                        humanDelta = humanEarned >= bid ? humanEarned : -bid;
                        aiDelta    = aiEarned;
                    }
                    else // AI won the bid
                    {
                        aiDelta    = aiEarned >= bid ? aiEarned : -bid;
                        humanDelta = humanEarned;
                    }
                }
                else
                {
                    humanDelta = humanEarned;
                    aiDelta    = aiEarned;
                }

                // Report to MatchManager first so match scores are updated.
                _matchManager.ReportRoundResult(
                    humanPts, aiPts,
                    winningBid:  _winningBid,
                    humanWonBid: _bidWinnerSeat == 0);

                // Show the rich result banner.
                _scoreHUD?.ShowResult(
                    humanCardPts:    _humanParticipant.CapturedPoints,
                    aiCardPts:       _aiParticipant.CapturedPoints,
                    humanTotal:      humanPts,
                    aiTotal:         aiPts,
                    winningBid:      _winningBid,
                    humanWonBid:     _bidWinnerSeat == 0,
                    humanVPDelta:    humanDelta,
                    aiVPDelta:       aiDelta,
                    humanMatchScore: _matchManager.HumanMatchScore,
                    aiMatchScore:    _matchManager.AIMatchScore);

                // Wait so the player can read the result.
                yield return new WaitForSeconds(3f);

                if (_matchManager.IsMatchOver)
                    yield break;

                StartRound();
            }
            else
            {
                _scoreHUD?.ShowResult(humanPts, aiPts);
            }
        }

        // ── Bidding phase ─────────────────────────────────────────────────────

        private IEnumerator BiddingPhase()
        {
            int highBid      = 0;
            int highSeat     = -1;
            bool[] active    = new bool[] { true, true };
            int activeCount  = 2;
            int lastPasser   = 0;

            int seat = 0; // human bids first

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
                        yield return new WaitUntil(() => bidDone);
                        _bidPanel?.Hide();
                    }
                    else // AI
                    {
                        yield return new WaitForSeconds(_aiThinkDelay * 0.5f);
                        bid = _aiController.PlaceBid(_aiParticipant, ctx);
                    }

                    bool valid = !bid.IsPass
                                 && bid.Value >= ctx.MinLegalBid
                                 && bid.Value <= _config.MaxBid;

                    if (valid)
                    {
                        highBid  = bid.Value;
                        highSeat = seat;
                        _scoreHUD?.ShowBidInfo($"{(seat == 0 ? "You" : "AI")} bid {highBid}");
                        _gameLogger?.LogBid(seat == 0 ? "You" : "AI", highBid);
                    }
                    else
                    {
                        active[seat] = false;
                        activeCount--;
                        lastPasser   = seat;
                        _scoreHUD?.ShowBidInfo($"{(seat == 0 ? "You" : "AI")} passed");
                        _gameLogger?.LogBid(seat == 0 ? "You" : "AI", null);
                    }
                }

                if (activeCount == 0) break;
                seat = (seat + 1) % 2;
            }

            if (highSeat < 0)
            {
                highSeat = lastPasser;
                highBid  = 1;
                _scoreHUD?.ShowBidInfo($"{(highSeat == 0 ? "You" : "AI")} forced to bid 1");
                _gameLogger?.LogForcedBid(highSeat == 0 ? "You" : "AI");
            }

            _winningBid    = highBid;
            _bidWinnerSeat = highSeat;
            _leaderIndex   = highSeat;

            string bidWinnerName = highSeat == 0 ? "You" : "AI";
            _scoreHUD?.ShowPersistentInfo($"{bidWinnerName} won the bid at {highBid}");
            _gameLogger?.LogBidWinner(bidWinnerName, highBid);
            Debug.Log($"[Cruce] Bid won by {(highSeat == 0 ? "Human" : "AI")} at {highBid}");
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
            else // AI declares
            {
                yield return new WaitForSeconds(_aiThinkDelay * 0.5f);
                _trump = _aiController.DeclareTrump(_aiParticipant);
            }

            string suitName = SuitDisplayName(_trump);
            string declarer = _bidWinnerSeat == 0 ? "You declare" : "AI declares";
            _scoreHUD?.ShowPersistentInfo($"{declarer} trump: {suitName}");
            _scoreHUD?.SetTrump(_trump);
            _trickAreaView?.SetTrump(_trump);
            _gameLogger?.LogTrumpDeclared(_bidWinnerSeat == 0 ? "You" : "AI", suitName);
            Debug.Log($"[Cruce] Trump declared: {_trump}");
            yield return new WaitForSeconds(1.5f);
            _scoreHUD?.ClearPersistentInfo();
        }

        // ── Trick loop ────────────────────────────────────────────────────────

        private IEnumerator TrickLoop()
        {
            while (!_humanParticipant.Hand.IsEmpty || !_aiParticipant.Hand.IsEmpty)
            {
                _trickCount++;
                yield return StartCoroutine(PlayOneTrick());
            }
        }

        private IEnumerator PlayOneTrick()
        {
            _currentTrick = new Trick();
            _currentTrick.SetTrump(_trump);
            _currentTrick.SetDrawPileCount(_deck.Count);
            _trickAreaView?.ClearTrick();

            string leaderName = _leaderIndex == 0 ? "You" : "AI";
            _gameLogger?.LogTrickStart(_trickCount, leaderName);

            for (int i = 0; i < 2; i++)
            {
                int seat    = (_leaderIndex + i) % 2;
                bool isHuman = (seat == 0);
                bool isLeading = (i == 0);
                bool isOpeningLead = _firstPlayOfRound && isLeading;

                if (isHuman)
                    yield return StartCoroutine(HumanPlays(isLeading, isOpeningLead));
                else
                    yield return StartCoroutine(AIPlays(isLeading, isOpeningLead));
            }

            yield return new WaitForSeconds(_trickResolvePause);
            ResolveTrick();
            yield return new WaitForSeconds(0.3f);
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
                    _marriagePanel?.Show(marriages,
                        suit => { _humanController.SubmitMarriage(suit.Value); },
                        ()   => { _humanController.SubmitNoMarriage(); });
                    yield return new WaitUntil(() => done);
                    _marriagePanel?.Hide();

                    if (announcedSuit.HasValue)
                    {
                        int bonus = _trump.HasValue && announcedSuit.Value == _trump.Value ? 40 : 20;
                        _humanParticipant.AddMarriagePoints(bonus);
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
            yield return new WaitUntil(() => cardDone);
            _humanController.CancelTurn();

            _gameLogger?.LogCardPlayed("You", chosen.ToString());
            PlayCard(_humanParticipant, chosen, isHuman: true);
            _firstPlayOfRound = false;
        }

        // ── AI turn ───────────────────────────────────────────────────────────

        private IEnumerator AIPlays(bool isLeading, bool isOpeningLead)
        {
            yield return new WaitForSeconds(_aiThinkDelay);

            // Marriage announcement
            Suit? announcedSuit = null;
            if (isLeading)
            {
                var marriages = new List<Suit>(_aiParticipant.Hand.MarriageSuits());
                if (isOpeningLead)
                    marriages.RemoveAll(s => !(_trump.HasValue && s == _trump.Value));

                if (marriages.Count > 0)
                {
                    announcedSuit = _aiController.AnnounceMarriage(_aiParticipant, marriages, _trump);
                    if (announcedSuit.HasValue)
                    {
                        int bonus = _trump.HasValue && announcedSuit.Value == _trump.Value ? 40 : 20;
                        _aiParticipant.AddMarriagePoints(bonus);
                        _scoreHUD?.ShowMarriageBonus(bonus);
                        _gameLogger?.LogMarriage("AI", announcedSuit.Value.ToString(), bonus);
                        Debug.Log($"[Cruce] AI announces marriage in {announcedSuit.Value} (+{bonus})");
                    }
                }
            }

            var context = new LegalMoveContext(
                _aiParticipant.Hand, _currentTrick, _trump, _deck.Count,
                isOpeningLead: isOpeningLead, announcedSuit: announcedSuit);
            IReadOnlyList<Card> legal = _legalMoveEvaluator.LegalMoves(context);

            Card chosen = _aiController.ChooseCard(_aiParticipant, legal, _currentTrick);
            _gameLogger?.LogCardPlayed("AI", chosen.ToString());
            PlayCard(_aiParticipant, chosen, isHuman: false);
            _firstPlayOfRound = false;
        }

        // ── Shared card play ──────────────────────────────────────────────────

        private void PlayCard(RoundParticipant participant, Card card, bool isHuman)
        {
            participant.Hand.Remove(card);
            _currentTrick.Add(participant, card);

            Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(card) : null;
            _trickAreaView?.AddCardToPile(sprite);

            if (isHuman)
                RemoveCardViewFromHand(card);
            else
                RemoveAICardBack();

            _scoreHUD?.SetDeckCount(_deck.Count);
        }

        // ── Trick resolution & draw ───────────────────────────────────────────

        private void ResolveTrick()
        {
            RoundParticipant winner = _currentTrick.DetermineWinner(_trump);
            int pts = _currentTrick.PointValue;
            winner.AddCapturedPoints(pts);

            bool humanWon = (winner == _humanParticipant);
            _leaderIndex  = humanWon ? 0 : 1;

            _trickAreaView?.FlashWinner(humanWon);
            _scoreHUD?.SetHumanPoints(_humanParticipant.CapturedPoints + _humanParticipant.MarriagePoints);
            _scoreHUD?.SetAIPoints(_aiParticipant.CapturedPoints + _aiParticipant.MarriagePoints);

            Debug.Log($"[Cruce] Trick {_trickCount}: {winner.Player.Name} wins {pts} pts.");
        }

        private void DrawFromPile()
        {
            if (_deck.Count == 0) return;

            RoundParticipant first  = _leaderIndex == 0 ? _humanParticipant : _aiParticipant;
            RoundParticipant second = _leaderIndex == 0 ? _aiParticipant    : _humanParticipant;

            first.Hand.Add(_deck.Draw());
            second.Hand.Add(_deck.Draw());

            _scoreHUD?.SetDeckCount(_deck.Count);
            RebuildHumanHand();
            RebuildAIHand();
        }

        // ── Hand view management ──────────────────────────────────────────────

        private void DealCards(int count, RoundParticipant participant)
        {
            participant.Hand.AddRange(_deck.Draw(count));
        }

        private void RebuildHumanHand()
        {
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            if (_humanHandArea == null || _cardPrefab == null) return;

            IReadOnlyList<Card> cards = _humanParticipant.Hand.Cards;
            int count = cards.Count;
            if (count == 0) return;

            // ── Fan / arc layout ─────────────────────────────────────────────
            // The cards are placed on an imaginary circle of radius _fanRadius.
            // The circle centre sits below the hand area so only the top arc
            // is visible. Each card is rotated to be tangent to the circle.

            float totalAngle = _fanTotalAngle;
            float angleStep  = count > 1 ? totalAngle / (count - 1) : 0f;
            float startAngle = -totalAngle * 0.5f; // degrees, 0 = straight up

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _humanHandArea);
                CardView cv   = go.GetComponent<CardView>();
                if (cv == null) continue;

                float angleDeg = startAngle + i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Position on the arc: circle centre is at (0, -_fanRadius)
                // relative to the hand area pivot.
                float x = _fanRadius * Mathf.Sin(angleRad);
                float y = _fanRadius * (1f - Mathf.Cos(angleRad)) - _fanRadius + _fanYOffset;

                Vector2 basePos = new Vector2(x, y);

                Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(cards[i]) : null;
                cv.Initialise(cards[i], sprite, basePos, angleDeg);
                cv.OnCardClicked += _humanController.HandleCardClicked;
                _humanCardViews.Add(cv);
            }
        }

        private void RebuildAIHand()
        {
            foreach (GameObject go in _aiCardBackViews)
                if (go != null) Destroy(go);
            _aiCardBackViews.Clear();

            if (_aiHandArea == null || _cardPrefab == null) return;

            int count = _aiParticipant.Hand.Count;
            if (count == 0) return;

            // Smaller fan for the AI hand at the top (flipped).
            float totalAngle = _fanTotalAngle * 0.7f;
            float angleStep  = count > 1 ? totalAngle / (count - 1) : 0f;
            float startAngle = -totalAngle * 0.5f;
            float radius     = _fanRadius * 0.6f;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _aiHandArea);

                CardView cv = go.GetComponent<CardView>();
                if (cv != null) cv.enabled = false;

                UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
                if (img != null && _cardBackSprite != null) img.sprite = _cardBackSprite;

                UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = false;

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float angleDeg = startAngle + i * angleStep;
                    float angleRad = angleDeg * Mathf.Deg2Rad;

                    float x = radius * Mathf.Sin(angleRad);
                    float y = -(radius * (1f - Mathf.Cos(angleRad)) - radius);

                    rt.anchoredPosition = new Vector2(x, y);
                    rt.sizeDelta        = new Vector2(130f, 195f);
                    // Flip rotation for top of screen (cards point downward).
                    rt.localRotation    = Quaternion.Euler(0f, 0f, -angleDeg + 180f);
                }

                _aiCardBackViews.Add(go);
            }
        }

        private void RemoveCardViewFromHand(Card card)
        {
            CardView toRemove = null;
            foreach (CardView cv in _humanCardViews)
                if (cv != null && cv.Card == card) { toRemove = cv; break; }
            if (toRemove == null) return;
            _humanCardViews.Remove(toRemove);
            Destroy(toRemove.gameObject);
        }

        private void RemoveAICardBack()
        {
            if (_aiCardBackViews.Count == 0) return;
            int last = _aiCardBackViews.Count - 1;
            if (_aiCardBackViews[last] != null) Destroy(_aiCardBackViews[last]);
            _aiCardBackViews.RemoveAt(last);
        }

        private void ClearAllCardViews()
        {
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            foreach (GameObject go in _aiCardBackViews)
                if (go != null) Destroy(go);
            _aiCardBackViews.Clear();
        }

        // ── Public button handlers ────────────────────────────────────────────

        public void OnPlayAgainClicked() => StartRound();

        public void OnMainMenuClicked() => SceneManager.LoadScene(_mainMenuScene);

        // ── Helpers ───────────────────────────────────────────────────────────

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
