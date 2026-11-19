using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Blocking "pass the device" confirmation panel for hotseat (pass-and-play)
    /// games. Shown by <c>GameManager</c> between two human seats' turns so the
    /// next player can take the device without seeing the previous player's hand.
    /// The panel covers the whole screen and intercepts clicks until "Yes".
    ///
    /// SCENE SETUP:
    ///   Create a child of Canvas named "PassDevicePanel" with this component.
    ///   The panel ROOT must be a full-screen opaque UI <c>Image</c> that covers
    ///   the entire canvas with Raycast Target = ON — this blocks clicks on the
    ///   hand underneath so a waiting player can't peek.
    ///   Children:
    ///     - "Heading"  TextMeshProUGUI — e.g. "Have you passed the device?"
    ///     - "Subtitle" TextMeshProUGUI — e.g. "so your opponent can't see your hand"
    ///     - "WhoIsNext" TextMeshProUGUI — OPTIONAL "Pass to: {name}" label
    ///     - "YesButton" Button          — "Yes"
    ///   Leave the panel GameObject active in the hierarchy; its Start()
    ///   deactivates it, so it only appears when GameManager calls Show().
    /// </summary>
    public class PassDevicePanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _heading;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private Button          _yesButton;

        [Tooltip("Optional label naming who should take the device next " +
                 "(e.g. \"Pass to: You\"). Leave unassigned to omit it.")]
        [SerializeField] private TextMeshProUGUI _whoIsNextLabel;

        private Action _onConfirmed;

        // Guards against a double Yes click firing the callback twice (e.g. a
        // fast double-tap before the panel hides).
        private bool _consumed;

        private void Awake()
        {
            if (_yesButton != null) _yesButton.onClick.AddListener(OnYesClicked);

            // Default copy — inspector values override these if set in the scene.
            if (_heading  != null && string.IsNullOrEmpty(_heading.text))
                _heading.text  = "Have you passed the device?";
            if (_subtitle != null && string.IsNullOrEmpty(_subtitle.text))
                _subtitle.text = "so your opponent can't see your hand";
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the blocking panel for the next player's turn. Sets the optional
        /// "who is next" label, stores the callback, and activates the GameObject.
        /// </summary>
        /// <param name="nextPlayerName">Seat name of the player taking the device.</param>
        /// <param name="onConfirmed">Invoked once when the player clicks Yes.</param>
        public void Show(string nextPlayerName, Action onConfirmed)
        {
            _onConfirmed = onConfirmed;
            _consumed    = false;

            if (_whoIsNextLabel != null)
                _whoIsNextLabel.text = $"Pass to: {nextPlayerName}";

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnYesClicked()
        {
            if (_consumed) return;
            _consumed = true;

            Hide();

            Action callback = _onConfirmed;
            _onConfirmed = null;
            callback?.Invoke();
        }
    }
}
