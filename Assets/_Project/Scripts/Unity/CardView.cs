using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Visual representation of a single card in the player's hand.
    ///
    /// Responsibilities:
    ///   - Displays the correct card sprite via a UI Image.
    ///   - Shows visual feedback for hover, selected, and illegal states.
    ///   - Raises OnCardClicked when the player clicks a legal card.
    ///   - Animates a smooth lift on hover and a raised position when selected.
    ///
    /// PREFAB SETUP:
    ///   Root: RectTransform + Image (card face) + Button + CardView component
    ///     Child "Highlight": Image used as a colour overlay for states
    ///
    /// The prefab should be ~120x180 px at 1x scale.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // ---- Inspector fields ------------------------------------------------

        [Header("Sprites")]
        [SerializeField] private Image _faceImage;
        [SerializeField] private Image _highlightOverlay;

        [Header("State Colours")]
        [SerializeField] private Color _hoverTint    = new Color(0.9f, 0.2f, 0.2f, 0.55f); // red
        [SerializeField] private Color _selectedTint = new Color(0.4f, 0.9f, 0.4f, 0.5f);
        [SerializeField] private Color _illegalTint  = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color _trumpTint    = new Color(1f, 0.85f, 0f, 0.45f); // gold
        [SerializeField] private Color _cursorTint   = new Color(0.9f, 0.2f, 0.2f, 0.55f); // red

        [Header("Animation")]
        [SerializeField] private float _hoverLiftPx    = 20f;
        [SerializeField] private float _selectedLiftPx = 35f;
        [SerializeField] private float _animSpeed      = 10f;

        [Header("Order Label")]
        [SerializeField] private float _orderFontSize = 36f;
        [SerializeField] private Color _orderColor    = Color.white;

        // ---- Runtime state ---------------------------------------------------

        /// <summary>The domain card this view represents. Set via Initialise().</summary>
        public Card Card { get; private set; }

        /// <summary>
        /// Raised when the player clicks this card.
        /// The GameManager subscribes to this on each CardView it spawns.
        /// </summary>
        public event Action<Card> OnCardClicked;

        /// <summary>
        /// Raised when the player clicks this card while it is NOT legal to
        /// play. An illegal card's Button is non-interactable, so the normal
        /// OnCardClicked path never fires for it; this view implements
        /// IPointerClickHandler so the raycast still delivers the click here and
        /// we can give "that move isn't allowed" feedback (e.g. a denied sound).
        /// </summary>
        public event Action OnIllegalClickAttempt;

        private Button          _button;
        private RectTransform   _rect;
        private Vector2         _basePosition;
        private Vector2         _targetPosition;
        private float           _baseRotationDeg;
        private bool            _isLegal     = true;
        private bool            _isSelected  = false;
        private bool            _isHovered   = false;
        private bool            _isTrump     = false;
        private bool            _isCursor    = false; // keyboard navigation cursor is on this card
        private TextMeshProUGUI _orderLabel;

        // ---- Unity lifecycle -------------------------------------------------

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect   = GetComponent<RectTransform>();
            _button.onClick.AddListener(HandleClick);

            if (_highlightOverlay != null)
                _highlightOverlay.color = Color.clear;

            CreateOrderLabel();
        }

        /// <summary>
        /// Builds a TextMeshPro label at runtime, anchored to the top of the card,
        /// that shows this card's position number in the fan (1, 2, 3, ...).
        /// Created in code so no prefab change is required.
        /// </summary>
        private void CreateOrderLabel()
        {
            var go = new GameObject("OrderLabel", typeof(RectTransform));
            var labelRect = go.GetComponent<RectTransform>();
            labelRect.SetParent(transform, false);

            // Anchor to the top edge of the card and sit the label ABOVE it
            // (outside the card square). Pivot at the label's bottom-centre so
            // positive Y pushes it clear of the top edge.
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot     = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(60f, 50f);
            labelRect.anchoredPosition = new Vector2(0f, 8f);

            _orderLabel = go.AddComponent<TextMeshProUGUI>();
            _orderLabel.alignment      = TextAlignmentOptions.Center;
            _orderLabel.fontSize       = _orderFontSize;
            _orderLabel.color          = _orderColor;
            _orderLabel.fontStyle      = FontStyles.Bold;
            _orderLabel.raycastTarget  = false;
            _orderLabel.text           = string.Empty;

            // A subtle outline so the number is readable over any card art.
            _orderLabel.outlineWidth = 0.25f;
            _orderLabel.outlineColor = new Color32(0, 0, 0, 255);

            // Render above the card face and the highlight overlay.
            go.transform.SetAsLastSibling();
        }

        private void Update()
        {
            _rect.anchoredPosition = Vector2.Lerp(
                _rect.anchoredPosition,
                _targetPosition,
                Time.deltaTime * _animSpeed);
        }

        // ---- Public API ------------------------------------------------------

        /// <summary>
        /// Must be called immediately after Instantiate by the GameManager.
        /// </summary>
        public void Initialise(Card card, Sprite sprite, Vector2 basePosition, float baseRotationDeg = 0f)
        {
            Card            = card;
            _basePosition      = basePosition;
            _targetPosition    = basePosition;
            _baseRotationDeg   = baseRotationDeg;
            _rect.anchoredPosition = basePosition;
            _rect.localRotation    = Quaternion.Euler(0f, 0f, -baseRotationDeg);

            if (_faceImage != null)
                _faceImage.sprite = sprite;

            RefreshVisuals();
        }

        /// <summary>
        /// Updates the resting position when the hand layout reflows.
        /// </summary>
        public void SetBasePosition(Vector2 pos)
        {
            _basePosition = pos;
            if (!_isSelected && !_isHovered)
                _targetPosition = pos;
        }

        /// <summary>
        /// Updates the resting rotation when the hand layout reflows.
        /// </summary>
        public void SetBaseRotation(float angleDeg)
        {
            _baseRotationDeg = angleDeg;
            if (!_isSelected && !_isHovered)
                _rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
        }

        /// <summary>
        /// Marks this card as a trump card — shows a golden highlight underneath
        /// the normal state colours so it's always visually distinct.
        /// </summary>
        public void SetTrump(bool isTrump)
        {
            _isTrump = isTrump;
            RefreshVisuals();
        }

        /// <summary>
        /// Marks whether this card is currently legal to play.
        /// Illegal cards are greyed out and unclickable.
        /// </summary>
        public void SetLegal(bool legal)
        {
            _isLegal = legal;
            _button.interactable = legal;
            RefreshVisuals();
        }

        /// <summary>
        /// Sets the selected state (card lifts higher and turns green).
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RefreshVisuals();
            RecalcTargetPosition();
        }

        /// <summary>
        /// Sets the 1-based order number shown on top of the card.
        /// Pass a value &lt;= 0 to hide the label.
        /// </summary>
        public void SetOrderNumber(int number)
        {
            if (_orderLabel == null) return;
            _orderLabel.text = number > 0 ? number.ToString() : string.Empty;
        }

        /// <summary>
        /// Marks this card as the current keyboard-navigation cursor.
        /// Shows a blue tint and a small lift (like hover) so the player can see
        /// which card the arrow keys are pointing at. Does not fire any action.
        /// </summary>
        public void SetKeyboardCursor(bool isCursor)
        {
            _isCursor = isCursor;
            RefreshVisuals();
            RecalcTargetPosition();
        }

        // ---- Pointer handlers -----------------------------------------------

        /// <summary>
        /// The card the mouse is currently hovering, or null if none.
        /// Used so keyboard navigation can yield priority to the mouse.
        /// </summary>
        public static CardView HoveredCard { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isLegal) return;
            _isHovered = true;
            HoveredCard = this;
            RefreshVisuals();
            RecalcTargetPosition();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            if (HoveredCard == this) HoveredCard = null;
            RefreshVisuals();
            RecalcTargetPosition();
        }

        private void OnDisable()
        {
            // Guard against a destroyed/disabled card leaving a stale hover ref.
            if (HoveredCard == this) HoveredCard = null;
        }

        /// <summary>
        /// A legal click reaches HandleClick through the Button's onClick, so
        /// here we only react to clicks on an ILLEGAL card (whose Button is
        /// disabled and therefore never raises onClick). This lets the game give
        /// feedback that the move isn't allowed without double-firing the normal
        /// click path.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isLegal)
                OnIllegalClickAttempt?.Invoke();
        }

        // ---- Private helpers -------------------------------------------------

        private void HandleClick()
        {
            if (!_isLegal) return;
            OnCardClicked?.Invoke(Card);
        }

        private void RefreshVisuals()
        {
            if (_highlightOverlay == null) return;

            if (!_isLegal)
                _highlightOverlay.color = _illegalTint;
            else if (_isSelected)
                _highlightOverlay.color = _selectedTint;
            else if (_isHovered)
                _highlightOverlay.color = _hoverTint;   // mouse takes priority over keyboard
            else if (_isCursor)
                _highlightOverlay.color = _cursorTint;
            else if (_isTrump)
                _highlightOverlay.color = _trumpTint;
            else
                _highlightOverlay.color = Color.clear;
        }

        private void RecalcTargetPosition()
        {
            // Lift direction is along the card's local up axis (perpendicular to
            // the fan arc), so the card slides toward the player's hand centre.
            float rad = _baseRotationDeg * Mathf.Deg2Rad;
            Vector2 liftDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

            if (_isSelected)
                _targetPosition = _basePosition + liftDir * _selectedLiftPx;
            else if (_isHovered || _isCursor)
                _targetPosition = _basePosition + liftDir * _hoverLiftPx;
            else
                _targetPosition = _basePosition;
        }
    }
}
