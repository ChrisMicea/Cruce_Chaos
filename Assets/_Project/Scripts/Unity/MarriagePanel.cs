using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// UI panel shown when the human player is leading and holds a marriage
    /// (a Three + Four of the same suit). Lets them announce one or skip.
    ///
    /// SCENE SETUP:
    ///   Create a child of Canvas named "MarriagePanel".
    ///   Children:
    ///     - "Label"          TextMeshProUGUI — "You have a marriage! Announce?"
    ///     - "GreenButton"    Button (hidden when suit not available)
    ///     - "RedButton"      Button
    ///     - "AcornButton"    Button
    ///     - "BellButton"     Button
    ///     - "SkipButton"     Button — "Don't announce"
    ///   Disable the panel GameObject by default.
    /// </summary>
    public class MarriagePanel : MonoBehaviour
    {
        [Header("Suit Buttons (order: Green, Red, Acorn, Bell)")]
        [SerializeField] private Button[] _suitButtons; // 4 buttons

        [SerializeField] private Button          _skipButton;
        [SerializeField] private TextMeshProUGUI _label;

        private Action<Suit?> _onChosen;

        private void Awake()
        {
            for (int i = 0; i < _suitButtons.Length && i < 4; i++)
            {
                int idx = i;
                if (_suitButtons[idx] != null)
                    _suitButtons[idx].onClick.AddListener(() => OnSuitClicked((Suit)idx));
            }

            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<Suit> available, Action<Suit?> onChosen, Action onSkip)
        {
            _onChosen = onChosen;

            // Show only buttons for available marriage suits.
            for (int i = 0; i < _suitButtons.Length && i < 4; i++)
            {
                if (_suitButtons[i] == null) continue;
                bool hasIt = false;
                foreach (Suit s in available)
                    if ((int)s == i) { hasIt = true; break; }
                _suitButtons[i].gameObject.SetActive(hasIt);
            }

            if (_label != null) _label.text = "Marriage available!\nAnnounce?";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnSuitClicked(Suit suit)
        {
            Action<Suit?> cb = _onChosen;
            _onChosen        = null;
            Hide();
            cb?.Invoke(suit);
        }

        private void OnSkipClicked()
        {
            Action<Suit?> cb = _onChosen;
            _onChosen        = null;
            Hide();
            cb?.Invoke(null);
        }
    }
}
