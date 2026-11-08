using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CruceChaos.Core.Game;

namespace CruceChaos.Unity
{
    /// <summary>
    /// UI panel that lets the human player place a bid or pass.
    ///
    /// SCENE SETUP:
    ///   Create a child of Canvas named "BidPanel" with this component.
    ///   Children:
    ///     - "BidLabel"   TextMeshProUGUI  — shows e.g. "Current bid: 2  |  Your bid (3–6):"
    ///     - "BidSlider"  Slider           — min=MinLegalBid, max=MaxBid, whole numbers
    ///     - "BidValue"   TextMeshProUGUI  — shows the slider's current value
    ///     - "BidButton"  Button           — "Bid X"
    ///     - "PassButton" Button           — "Pass"
    ///   Disable the panel GameObject by default.
    /// </summary>
    public class BidPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _bidLabel;
        [SerializeField] private Slider          _bidSlider;
        [SerializeField] private TextMeshProUGUI _bidValueLabel;
        [SerializeField] private Button          _bidButton;
        [SerializeField] private Button          _passButton;
        [SerializeField] private TextMeshProUGUI _bidButtonLabel;

        [Header("Audio")]
        [Tooltip("Plays a short tick when the player moves the bid slider. " +
                 "Optional — leave unassigned to run silent.")]
        [SerializeField] private SoundManager _sound;

        private HumanPlayerController _controller;
        private Action                _onDone;
        private BiddingContext        _context;

        // True only while Show() is programmatically setting the slider, so the
        // onValueChanged handler can skip the tick for that non-user change.
        private bool _settingSliderProgrammatically;

        private void Awake()
        {
            if (_bidButton  != null) _bidButton.onClick.AddListener(OnBidClicked);
            if (_passButton != null) _passButton.onClick.AddListener(OnPassClicked);
            if (_bidSlider  != null) _bidSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Show the panel and wire it up for this bid decision.</summary>
        public void Show(BiddingContext context, HumanPlayerController controller, Action onDone)
        {
            _context    = context;
            _controller = controller;
            _onDone     = onDone;

            if (_bidSlider != null)
            {
                // Suppress the slider tick for this programmatic reset — it's
                // panel setup, not the player moving the slider.
                _settingSliderProgrammatically = true;
                _bidSlider.minValue    = context.MinLegalBid;
                _bidSlider.maxValue    = context.MaxBid;
                _bidSlider.wholeNumbers = true;
                _bidSlider.value       = context.MinLegalBid;
                _settingSliderProgrammatically = false;
            }

            UpdateLabel();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Increments the slider by 1, wrapping back to MinLegalBid after MaxBid.</summary>
        public void IncrementBid()
        {
            if (_bidSlider == null) return;
            int next = (int)_bidSlider.value + 1;
            if (next > (int)_bidSlider.maxValue)
                next = (int)_bidSlider.minValue;
            _bidSlider.value = next;
        }

        /// <summary>Confirms the current slider value as a bid (same as clicking Bid button).</summary>
        public void ConfirmBid() => OnBidClicked();

        /// <summary>Passes (same as clicking Pass button).</summary>
        public void ConfirmPass() => OnPassClicked();

        private void OnSliderChanged(float value)
        {
            UpdateLabel();
            if (!_settingSliderProgrammatically)
                _sound?.PlayBidSlider();
        }

        private void UpdateLabel()
        {
            int val = _bidSlider != null ? (int)_bidSlider.value : _context.MinLegalBid;

            if (_bidLabel != null)
            {
                string current = _context.CurrentHighBid.HasValue
                    ? $"Current bid: {_context.CurrentHighBid.Value}   |   "
                    : "";
                _bidLabel.text = $"{current}Your bid ({_context.MinLegalBid}–{_context.MaxBid}):";
            }

            if (_bidValueLabel  != null) _bidValueLabel.text  = val.ToString();
            if (_bidButtonLabel != null) _bidButtonLabel.text = $"Bid {val}";
        }

        private void OnBidClicked()
        {
            int val = _bidSlider != null ? (int)_bidSlider.value : _context.MinLegalBid;
            _controller.SubmitBid(val);
            _onDone?.Invoke();
        }

        private void OnPassClicked()
        {
            _controller.SubmitPass();
            _onDone?.Invoke();
        }
    }
}
