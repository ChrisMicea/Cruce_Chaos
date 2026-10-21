using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        IPointerEnterHandler, IPointerExitHandler
    {
        // ---- Inspector fields ------------------------------------------------

        [Header("Sprites")]
        [SerializeField] private Image _faceImage;
        [SerializeField] private Image _highlightOverlay;

        [Header("State Colours")]
        [SerializeField] private Color _hoverTint    = new Color(1f, 1f, 0.7f, 0.5f);
        [SerializeField] private Color _selectedTint = new Color(0.4f, 0.9f, 0.4f, 0.5f);
        [SerializeField] private Color _illegalTint  = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Animation")]
        [SerializeField] private float _hoverLiftPx    = 20f;
        [SerializeField] private float _selectedLiftPx = 35f;
        [SerializeField] private float _animSpeed      = 10f;

        // ---- Runtime state ---------------------------------------------------

        /// <summary>The domain card this view represents. Set via Initialise().</summary>
        public Card Card { get; private set; }

        /// <summary>
        /// Raised when the player clicks this card.
        /// The GameManager subscribes to this on each CardView it spawns.
        /// </summary>
        public event Action<Card> OnCardClicked;

        private Button        _button;
        private RectTransform _rect;
        private Vector2       _basePosition;
        private Vector2       _targetPosition;
        private bool          _isLegal    = true;
        private bool          _isSelected = false;
        private bool          _isHovered  = false;

        // ---- Unity lifecycle -------------------------------------------------

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect   = GetComponent<RectTransform>();
            _button.onClick.AddListener(HandleClick);

            if (_highlightOverlay != null)
                _highlightOverlay.color = Color.clear;
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
        public void Initialise(Card card, Sprite sprite, Vector2 basePosition)
        {
            Card            = card;
            _basePosition   = basePosition;
            _targetPosition = basePosition;
            _rect.anchoredPosition = basePosition;

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

        // ---- Pointer handlers -----------------------------------------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isLegal) return;
            _isHovered = true;
            RefreshVisuals();
            RecalcTargetPosition();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshVisuals();
            RecalcTargetPosition();
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
                _highlightOverlay.color = _hoverTint;
            else
                _highlightOverlay.color = Color.clear;
        }

        private void RecalcTargetPosition()
        {
            if (_isSelected)
                _targetPosition = _basePosition + Vector2.up * _selectedLiftPx;
            else if (_isHovered)
                _targetPosition = _basePosition + Vector2.up * _hoverLiftPx;
            else
                _targetPosition = _basePosition;
        }
    }
}
