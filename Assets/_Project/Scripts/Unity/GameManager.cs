using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;
using CruceChaos.Core.Players.AI.GreedyHeuristic;
using CruceChaos.Core.Rules;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Central scene controller for a 1-human-vs-1-AI round of Cruce.
    ///
    /// It owns the game loop, drives both players' turns, and coordinates all
    /// the UI views. The pure-C# engine components (Deck, Hand, RoundEngine)
    /// are re-created fresh for each round.
    ///
    /// ─── SCENE SETUP ────────────────────────────────────────────────────────
    ///
    ///  Canvas (Screen Space – Overlay, reference 1920×1080)
    ///  ├── HandArea          ← RectTransform where human cards are spawned
    ///  │     (anchor: bottom-centre, pivot: bottom-centre, ~1000×220 px)
    ///  ├── AIHandArea        ← (optional) top strip showing AI card backs
    ///  ├── TrickArea         ← TrickAreaView component (see that script)
    ///  ├── HUD               ← ScoreHUD component (see that script)
    ///  └── ResultBanner      ← (child of ScoreHUD or standalone)
    ///
    ///  GameManager GameObject (empty, not inside Canvas)
    ///  ├── GameManager component   ← this script
    ///  └── HumanPlayerController   ← HumanPlayerController component
    ///
    /// ─── CARD PREFAB ────────────────────────────────────────────────────────
    ///  The card prefab needs:
    ///    Root:  RectTransform, Image (face), Button, CardView component
    ///    Child "Highlight": Image (colour overlay, raycast off)
    ///  Size: ~120×180 px recommended.
    ///
    /// ─── INSPECTOR FIELDS ───────────────────────────────────────────────────
    ///  Drag references from the scene hierarchy into the fields below.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Inspector wiring ──────────────────────────────────────────────────

        [Header("Data")]
        [Tooltip("The CardSpriteRegistry ScriptableObject (create via Assets menu).")]
        [SerializeField] private CardSpriteRegistry _spriteRegistry;

        [Header("Prefabs")]
        [Tooltip("Card UI prefab with CardView, Image, and Button components.")]
        [SerializeField] private GameObject _cardPrefab;

        [Header("Scene References")]
        [SerializeField] private RectTransform _humanHandArea;
        [SerializeField] private RectTransform _aiHandArea;     // optional, shows card backs
        [SerializeField] private TrickAreaView  _trickAreaView;
        [SerializeField] private ScoreHUD       _scoreHUD;
        [SerializeField] private HumanPlayerController _humanController;

        [Header("Card Back Sprite")]
        [Tooltip("Sprite shown for AI face-down cards.")]
        [SerializeField] private Sprite _cardBackSprite;

        [Header("Timing")]
        [Tooltip("Delay (seconds) before the AI plays its card (makes it feel less instant).")]
        [SerializeField] [Range(0.3f, 2f)] private float _aiThinkDelay = 0.8f;
        [Tooltip("Pause after a trick resolves before clearing the table.")]
        [SerializeField] [Range(0.3f, 2f)] private float _trickResolvePause = 1.0f;

        [Header("Game Rules")]
        [SerializeField] private bool _mustRaiseWhenFollowing = false;

        [Header("Match Scoring (optional)")]
        [Tooltip("Assign the MatchManager to enable cross-round scoring. Leave empty for single-round play.")]
        [SerializeField] private MatchManager _matchManager;

        [Header("Scene Navigation")]
        [Tooltip("Name of the main-menu scene to load when the player clicks 'Back to Menu'.")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        // ── Runtime state ─────────────────────────────────────────────────────

        // Domain players (persistent match identity).
        private Player _humanPlayer;
        private Player _aiPlayer;

        // Per-round participants (hand + captured points).
        private RoundParticipant _humanParticipant;
        private RoundParticipant _aiParticipant;

        // AI controller (pure C#, no MonoBehaviour).
        private GreedyHeuristicController _aiController;

        // Rule pipeline.
        private LegalMoveEvaluator _legalMoveEvaluator;

        // Deck for the current round.
        private Deck _deck;

        // Currently displayed CardViews for the human's hand.
        private readonly List<CardView> _humanCardViews = new List<CardView>();

        // Currently displayed CardViews for the AI's hand (card backs).
        private readonly List<GameObject> _aiCardBackViews = new List<GameObject>();

        // Round state.
        private Trick _currentTrick;
        private Suit? _trump;
        private int   _leaderIndex; // 0 = human leads, 1 = AI leads
        private int   _trickCount;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            _humanPlayer  = new Player("You");
            _aiPlayer     = new Player("AI");
            _aiController = new GreedyHeuristicController();

            StartRound();
        }

        // ── Round flow ────────────────────────────────────────────────────────

        /// <summary>Initialises a fresh round and deals cards.</summary>
        private void StartRound()
        {
            _trump       = null;
            _leaderIndex = 0;
            _trickCount  = 0;

            // Build fresh participants.
            _humanParticipant = new RoundParticipant(_humanPlayer);
            _aiParticipant    = new RoundParticipant(_aiPlayer);

            // Build rule set.
            var ruleSet = RuleSetFactory.TwoPlayerDrawPile(new RuleSetFactory.Options
            {
                MustRaiseWhenFollowing = _mustRaiseWhenFollowing
            });
            _legalMoveEvaluator = new LegalMoveEvaluator(ruleSet,
                msg => Debug.LogWarning("[LegalMoves] " + msg));

            // Shuffle and deal.
            _deck = new Deck();
            _deck.Shuffle();
            DealCards(8, _humanParticipant);
            DealCards(8, _aiParticipant);

            // Clear views.
            ClearAllCardViews();
            _trickAreaView?.ClearTrick();
            _scoreHUD?.HideResult();
            _scoreHUD?.SetTrump(null);
            _scoreHUD?.SetDeckCount(_deck.Count);
            _scoreHUD?.SetHumanPoints(0);
            _scoreHUD?.SetAIPoints(0);

            // Spawn hand views.
            RebuildHumanHand();
            RebuildAIHand();

            // Start the trick loop.
            StartCoroutine(TrickLoop());
        }

        /// <summary>
        /// Main coroutine: plays tricks until all cards are gone,
        /// then shows the result.
        /// </summary>
        private IEnumerator TrickLoop()
        {
            while (!_humanParticipant.Hand.IsEmpty || !_aiParticipant.Hand.IsEmpty)
            {
                _trickCount++;
                yield return StartCoroutine(PlayOneTrick());
            }

            // Round over — show result.
            int humanPts = _humanParticipant.CapturedPoints;
            int aiPts    = _aiParticipant.CapturedPoints;

            _scoreHUD?.ShowResult(humanPts, aiPts);
            _matchManager?.ReportRoundResult(humanPts, aiPts);
        }

        /// <summary>Drives one full trick: two plays, resolution, draw phase.</summary>
        private IEnumerator PlayOneTrick()
        {
            _currentTrick = new Trick();
            _currentTrick.SetTrump(_trump);
            _currentTrick.SetDrawPileCount(_deck.Count);
            _trickAreaView?.ClearTrick();

            // Two plays: leader first, then the other.
            for (int i = 0; i < 2; i++)
            {
                int seat = (_leaderIndex + i) % 2;
                bool isHuman = (seat == 0);

                if (isHuman)
                    yield return StartCoroutine(HumanPlays());
                else
                    yield return StartCoroutine(AIPlays());
            }

            // Pause briefly so both cards are visible before resolving.
            yield return new WaitForSeconds(_trickResolvePause);

            ResolveTrick();

            // Small pause after resolution.
            yield return new WaitForSeconds(0.3f);

            DrawFromPile();
        }

        // ── Turn execution ────────────────────────────────────────────────────

        /// <summary>
        /// Human turn: highlights legal cards, waits for a click,
        /// then plays the chosen card.
        /// </summary>
        private IEnumerator HumanPlays()
        {
            // Compute legal moves.
            var context = new LegalMoveContext(
                _humanParticipant.Hand, _currentTrick, _trump, _deck.Count);
            IReadOnlyList<Card> legal = _legalMoveEvaluator.LegalMoves(context);

            // Mark each card view as legal or not.
            foreach (CardView cv in _humanCardViews)
            {
                bool isLegal = false;
                foreach (Card c in legal)
                    if (c == cv.Card) { isLegal = true; break; }
                cv.SetLegal(isLegal);
            }

            // Wait for the player to click.
            Card chosen = default;
            bool cardChosen = false;

            _humanController.BeginTurn(legal, card =>
            {
                chosen     = card;
                cardChosen = true;
            });

            yield return new WaitUntil(() => cardChosen);

            // Cancel any lingering hover/selected states.
            _humanController.CancelTurn();

            // Play the card.
            PlayCard(_humanParticipant, chosen, isHuman: true);
        }

        /// <summary>
        /// AI turn: waits a moment to simulate "thinking", then plays.
        /// </summary>
        private IEnumerator AIPlays()
        {
            yield return new WaitForSeconds(_aiThinkDelay);

            var context = new LegalMoveContext(
                _aiParticipant.Hand, _currentTrick, _trump, _deck.Count);
            IReadOnlyList<Card> legal = _legalMoveEvaluator.LegalMoves(context);

            Card chosen = _aiController.ChooseCard(_aiParticipant, legal, _currentTrick);
            PlayCard(_aiParticipant, chosen, isHuman: false);
        }

        /// <summary>
        /// Shared card-play logic: removes the card from the participant's hand,
        /// adds it to the trick, establishes trump if needed, and updates views.
        /// </summary>
        private void PlayCard(RoundParticipant participant, Card card, bool isHuman)
        {
            participant.Hand.Remove(card);

            // The very first card of the round sets trump.
            bool trumpJustSet = false;
            if (!_trump.HasValue)
            {
                _trump = card.Suit;
                trumpJustSet = true;
                _currentTrick.SetTrump(_trump);
                _scoreHUD?.SetTrump(_trump);
                _trickAreaView?.SetTrump(_trump);
            }

            _currentTrick.Add(participant, card);

            // Show the card in the trick area.
            Sprite sprite = _spriteRegistry != null ? _spriteRegistry.GetSprite(card) : null;
            if (isHuman)
            {
                _trickAreaView?.ShowHumanCard(sprite);
                RemoveCardViewFromHand(card);
            }
            else
            {
                _trickAreaView?.ShowAICard(sprite);
                RemoveAICardBack();
            }

            // Update deck count (unchanged until draw phase, but keep it fresh).
            _scoreHUD?.SetDeckCount(_deck.Count);

            if (trumpJustSet)
                Debug.Log($"[Cruce] Trump established: {_trump}");
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
            _scoreHUD?.SetHumanPoints(_humanParticipant.CapturedPoints);
            _scoreHUD?.SetAIPoints(_aiParticipant.CapturedPoints);

            Debug.Log($"[Cruce] Trick {_trickCount}: {winner.Player.Name} wins {pts} pts. " +
                      $"(Human {_humanParticipant.CapturedPoints} / AI {_aiParticipant.CapturedPoints})");
        }

        private void DrawFromPile()
        {
            if (_deck.Count == 0) return;

            // Winner draws first.
            RoundParticipant first  = _leaderIndex == 0 ? _humanParticipant : _aiParticipant;
            RoundParticipant second = _leaderIndex == 0 ? _aiParticipant    : _humanParticipant;

            first.Hand.Add(_deck.Draw());
            second.Hand.Add(_deck.Draw());

            _scoreHUD?.SetDeckCount(_deck.Count);

            // Rebuild hand views to show new cards.
            RebuildHumanHand();
            RebuildAIHand();
        }

        // ── Hand view management ──────────────────────────────────────────────

        /// <summary>
        /// Deals <paramref name="count"/> cards from the deck into a
        /// participant's hand.
        /// </summary>
        private void DealCards(int count, RoundParticipant participant)
        {
            System.Collections.Generic.List<Card> dealt = _deck.Draw(count);
            participant.Hand.AddRange(dealt);
        }

        /// <summary>
        /// Destroys all existing CardViews and spawns fresh ones for every card
        /// in the human's hand, laid out in a centred row.
        /// </summary>
        private void RebuildHumanHand()
        {
            // Clear existing views.
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            if (_humanHandArea == null || _cardPrefab == null) return;

            IReadOnlyList<Card> cards = _humanParticipant.Hand.Cards;
            int count = cards.Count;
            if (count == 0) return;

            float cardWidth = 150f;
            float spacing   = 14f;
            float totalWidth = count * cardWidth + (count - 1) * spacing;
            float startX    = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _humanHandArea);
                CardView cv   = go.GetComponent<CardView>();
                if (cv == null) continue;

                float x = startX + i * (cardWidth + spacing);
                Vector2 basePos = new Vector2(x, 0f);

                Sprite sprite = _spriteRegistry != null
                    ? _spriteRegistry.GetSprite(cards[i])
                    : null;

                cv.Initialise(cards[i], sprite, basePos);

                // Wire click → HumanPlayerController.
                cv.OnCardClicked += _humanController.HandleCardClicked;

                _humanCardViews.Add(cv);
            }
        }

        /// <summary>
        /// Destroys all existing AI card-back objects and spawns fresh ones.
        /// </summary>
        private void RebuildAIHand()
        {
            foreach (GameObject go in _aiCardBackViews)
                if (go != null) Destroy(go);
            _aiCardBackViews.Clear();

            if (_aiHandArea == null || _cardPrefab == null) return;

            int count = _aiParticipant.Hand.Count;
            if (count == 0) return;

            float cardWidth = 100f;
            float spacing   = 6f;
            float totalWidth = count * cardWidth + (count - 1) * spacing;
            float startX    = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(_cardPrefab, _aiHandArea);

                // Show card back only — disable CardView interactivity.
                CardView cv = go.GetComponent<CardView>();
                if (cv != null) cv.enabled = false;

                UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
                if (img != null && _cardBackSprite != null)
                    img.sprite = _cardBackSprite;

                UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = false;

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float x = startX + i * (cardWidth + spacing);
                    rt.anchoredPosition = new Vector2(x, 0f);
                    rt.sizeDelta = new Vector2(cardWidth, cardWidth * 1.5f);
                }

                _aiCardBackViews.Add(go);
            }
        }

        /// <summary>Removes the CardView for the given card from the hand display.</summary>
        private void RemoveCardViewFromHand(Card card)
        {
            CardView toRemove = null;
            foreach (CardView cv in _humanCardViews)
            {
                if (cv != null && cv.Card == card)
                {
                    toRemove = cv;
                    break;
                }
            }
            if (toRemove == null) return;

            _humanCardViews.Remove(toRemove);
            Destroy(toRemove.gameObject);
        }

        /// <summary>Removes one AI card-back (the last one — order doesn't matter for backs).</summary>
        private void RemoveAICardBack()
        {
            if (_aiCardBackViews.Count == 0) return;
            int last = _aiCardBackViews.Count - 1;
            if (_aiCardBackViews[last] != null)
                Destroy(_aiCardBackViews[last]);
            _aiCardBackViews.RemoveAt(last);
        }

        /// <summary>Destroys all card view objects.</summary>
        private void ClearAllCardViews()
        {
            foreach (CardView cv in _humanCardViews)
                if (cv != null) Destroy(cv.gameObject);
            _humanCardViews.Clear();

            foreach (GameObject go in _aiCardBackViews)
                if (go != null) Destroy(go);
            _aiCardBackViews.Clear();
        }

        // ── Public buttons (wired to UI buttons in the Inspector) ─────────────

        /// <summary>Called by a "Play Again" button.</summary>
        public void OnPlayAgainClicked()
        {
            StartRound();
        }

        /// <summary>Called by a "Back to Menu" button.</summary>
        public void OnMainMenuClicked()
        {
            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}
