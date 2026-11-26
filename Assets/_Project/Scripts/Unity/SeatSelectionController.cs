using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CruceChaos.Core.Game;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Reusable visual seat-picker for pass-and-play. For a team/FFA mode
    /// (1v1v1, 2v2 classic, 2v2 draft) it lets the player toggle each seat
    /// between Human and AI, then writes the choice to <see cref="GameSelection"/>
    /// and loads that mode's scene. The game engine (GameManager) already honours
    /// an arbitrary <see cref="GameSelection.HumanSeats"/> array, so this panel
    /// only has to build that array.
    ///
    /// One component drives every mode: the seat COUNT (3 or 4), the seat LABELS,
    /// and the TARGET SCENE are all derived from the <see cref="GameMode"/> passed
    /// to <see cref="Configure"/>. A 3-seat mode hides the 4th seat toggle.
    ///
    /// Seat model (from Core GameMode.DefaultConfig):
    ///   2v2 / 2v2 draft — 4 seats, teams {0,2} vs {1,3}. Labels, by table
    ///     position: seat 0 = "You" (bottom), 1 = "Left", 2 = "Partner" (across),
    ///     3 = "Right".
    ///   1v1v1 — 3 seats, free-for-all (no teams). Labels: 0 = "You", 1 = "Left",
    ///     2 = "Right". There is NO "Partner" in FFA.
    ///
    /// All combinations are allowed, including all-AI (a spectator game) and
    /// all-human (full hotseat). There is no validation gate — any mix launches.
    ///
    /// SCENE SETUP:
    ///   Create a child of Canvas named "SeatSelectionPanel" with this component.
    ///   Children (build FOUR seat toggle slots even for 3-seat modes; the 4th is
    ///   hidden at runtime for 1v1v1):
    ///     - Four seat toggles, assigned to _seatButtons[0..3] in seat order.
    ///       Each is a Button; give it a child TextMeshProUGUI for the seat label
    ///       (assigned to _seatLabels[i]) and a child TextMeshProUGUI for the
    ///       Human/AI state (assigned to _seatStateLabels[i]). Clicking the
    ///       button flips that seat's state.
    ///     - "StartButton"  Button — confirms and launches the game.
    ///     - "BackButton"   Button — returns to the mode-select panel without
    ///       launching (and clears any staged GameSelection state).
    ///   Leave the panel active in the hierarchy; Start() deactivates it, so it
    ///   only appears when the mode-select controller calls Show()/Configure().
    /// </summary>
    public class SeatSelectionController : MonoBehaviour
    {
        [Header("Seat Toggles (seat order: 0,1,2,3)")]
        [Tooltip("One button per seat, in seat order. For a 3-seat mode the 4th " +
                 "is hidden automatically.")]
        [SerializeField] private Button[]          _seatButtons    = new Button[4];
        [Tooltip("The seat-label text on each seat button (You / Left / ...), in " +
                 "seat order.")]
        [SerializeField] private TextMeshProUGUI[] _seatLabels     = new TextMeshProUGUI[4];
        [Tooltip("The Human/AI state text on each seat button, in seat order.")]
        [SerializeField] private TextMeshProUGUI[] _seatStateLabels = new TextMeshProUGUI[4];

        [Header("Actions")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _backButton;

        [Header("Navigation")]
        [Tooltip("The menu controller that owns the Pass & Play flow — Back " +
                 "returns to its mode-select panel, Start delegates the scene " +
                 "load so the click-sound transition is shared.")]
        [SerializeField] private MainMenuController _menuController;

        [Header("State Text")]
        [SerializeField] private string _humanText = "Human";
        [SerializeField] private string _aiText    = "AI";

        // The mode this picker is currently configured for, and how many of the
        // four seat slots it uses.
        private GameMode _mode;
        private int      _seatCount;

        // Per-seat human/AI choice, index = seat. Only the first _seatCount
        // entries are meaningful for the current mode.
        private readonly bool[] _human = new bool[4];

        private void Awake()
        {
            for (int i = 0; i < _seatButtons.Length; i++)
            {
                int seat = i; // capture for the closure
                if (_seatButtons[i] != null)
                    _seatButtons[i].onClick.AddListener(() => ToggleSeat(seat));
            }

            if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_backButton  != null) _backButton.onClick.AddListener(OnBackClicked);
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures the picker for <paramref name="mode"/> (seat count, labels),
        /// resets to the default layout (seat 0 Human, the rest AI), and shows it.
        /// Called by the mode-select controller.
        /// </summary>
        public void Configure(GameMode mode)
        {
            _mode      = mode;
            _seatCount = SeatCountFor(mode);

            string[] labels = SeatLabelsFor(mode);

            for (int seat = 0; seat < _seatButtons.Length; seat++)
            {
                bool used = seat < _seatCount;

                // Hide unused seat slots (e.g. the 4th seat for 1v1v1).
                if (_seatButtons[seat] != null)
                    _seatButtons[seat].gameObject.SetActive(used);

                if (used)
                {
                    _human[seat] = (seat == 0); // default: only seat 0 is human
                    if (_seatLabels[seat] != null)
                        _seatLabels[seat].text = labels[seat];
                    RefreshSeat(seat);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void ToggleSeat(int seat)
        {
            if (seat < 0 || seat >= _seatCount) return;
            _human[seat] = !_human[seat];
            RefreshSeat(seat);
        }

        /// <summary>Updates one seat button's state text to Human/AI.</summary>
        private void RefreshSeat(int seat)
        {
            if (_seatStateLabels[seat] != null)
                _seatStateLabels[seat].text = _human[seat] ? _humanText : _aiText;
        }

        private void OnStartClicked()
        {
            // Build the HumanSeats array at EXACTLY the mode's player count so
            // GameManager uses it verbatim (a length mismatch makes it fall back).
            var humanSeats = new bool[_seatCount];
            for (int seat = 0; seat < _seatCount; seat++)
                humanSeats[seat] = _human[seat];

            GameSelection.SelectedMode = _mode;
            GameSelection.PassAndPlay  = false;   // seat-picked modes use HumanSeats
            GameSelection.HumanSeats   = humanSeats;

            Hide();

            // Delegate the actual scene load so the click-sound transition and
            // scene-name mapping live in one place (MainMenuController).
            if (_menuController != null)
                _menuController.LaunchSelectedMode(_mode);
        }

        private void OnBackClicked()
        {
            // Returning without launching must not leave staged state behind.
            GameSelection.HumanSeats = null;

            Hide();
            _menuController?.ShowModeSelect();
        }

        // ── Mode → seat count / labels (mirrors Core GameMode.DefaultConfig) ──

        private static int SeatCountFor(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.OneVsOneVsOne:               return 3;
                case GameMode.TwoVsTwo:
                case GameMode.TwoVsTwoDraft:               return 4;
                default:                                   return 2; // 1v1 (not seat-picked)
            }
        }

        private static string[] SeatLabelsFor(GameMode mode)
        {
            // 1v1v1 is free-for-all: no "Partner".
            if (mode == GameMode.OneVsOneVsOne)
                return new[] { "You", "Left", "Right", "" };

            // 2v2 / 2v2 draft: teams 0&2 vs 1&3, so seat 2 sits across as Partner.
            return new[] { "You", "Left", "Partner", "Right" };
        }
    }
}
