using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// UI panel shown to the bid winner to choose the trump suit.
    ///
    /// SCENE SETUP:
    ///   Create a child of Canvas named "TrumpPickerPanel".
    ///   Children:
    ///     - "Label"         TextMeshProUGUI — "You won the bid! Choose trump:"
    ///     - "GreenButton"   Button — suit Verde
    ///     - "RedButton"     Button — suit Rosu
    ///     - "AcornButton"   Button — suit Ghinda
    ///     - "BellButton"    Button — suit Duba
    ///   Disable the panel GameObject by default.
    /// </summary>
    public class TrumpPickerPanel : MonoBehaviour
    {
        [Header("Suit Buttons (order: Green, Red, Acorn, Bell)")]
        [SerializeField] private Button[] _suitButtons; // 4 buttons, one per suit
        [SerializeField] private TextMeshProUGUI _label;

        private Action<Suit> _onSuitChosen;

        private static readonly string[] SuitNames = { "Verde", "Rosu", "Ghinda", "Duba" };

        private void Awake()
        {
            for (int i = 0; i < _suitButtons.Length && i < 4; i++)
            {
                int idx = i; // capture for lambda
                if (_suitButtons[idx] != null)
                    _suitButtons[idx].onClick.AddListener(() => OnSuitClicked((Suit)idx));
            }
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        public void Show(Action<Suit> onSuitChosen)
        {
            _onSuitChosen = onSuitChosen;
            if (_label != null) _label.text = "You won the bid!\nChoose trump:";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnSuitClicked(Suit suit)
        {
            Action<Suit> cb = _onSuitChosen;
            _onSuitChosen   = null;
            Hide();
            cb?.Invoke(suit);
        }
    }
}
