using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CruceChaos.Core.Game;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Handles navigation from the Main Menu scene.
    /// Attach this to any GameObject in the Main Menu scene and wire
    /// the buttons/text buttons to the methods below.
    ///
    /// Each handler records the chosen <see cref="GameMode"/> in
    /// <see cref="GameSelection"/> before loading the target scene, so the
    /// scene's <c>GameManager</c> knows which variant to run. The two 2v2
    /// buttons load the SAME 2v2 scene and differ only in the mode they set:
    /// classic (<see cref="GameMode.TwoVsTwo"/>) vs the "with surprises" draft
    /// variant (<see cref="GameMode.TwoVsTwoDraft"/>).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Exact name of the 1v1 game scene (must match the scene filename).")]
        [SerializeField] private string _oneVsOneScene = "1vsAI";

        [Tooltip("Exact name of the 2v2 game scene (must match the scene filename). " +
                 "Both 2v2 buttons load this same scene.")]
        [SerializeField] private string _twoVsTwoScene = "2v2";

        [Tooltip("Exact name of the 1v1v1 game scene (must match the scene filename).")]
        [SerializeField] private string _oneVsOneVsOneScene = "1v1v1";

        [Tooltip("Plays the menu-button click sound. Optional — leave unassigned " +
                 "to run silent.")]
        [SerializeField] private SoundManager _sound;

        [Header("Pass & Play")]
        [Tooltip("In-scene panel listing the pass-and-play modes (1v1 / 1v1v1 / " +
                 "2v2 classic / 2v2 draft). Shown when the Pass & Play button is " +
                 "clicked; hidden otherwise. Optional — leave unassigned to skip.")]
        [SerializeField] private GameObject _modeSelectPanel;

        [Tooltip("The shared seat-picker panel. Opened for the team/FFA modes so " +
                 "the player can choose which seats are human vs AI. Not used for " +
                 "1v1 (both seats are human). Optional.")]
        [SerializeField] private SeatSelectionController _seatSelection;

        [Tooltip("Upper bound (seconds) on how long a click can delay the scene " +
                 "load / quit while its sound plays. Guards against an " +
                 "accidentally long clip freezing the menu.")]
        [SerializeField] [Range(0f, 1f)] private float _maxClickDelay = 0.4f;

        // Guards against a second click firing another coroutine mid-transition
        // (e.g. mashing two buttons) — once a transition starts, further clicks
        // are ignored.
        private bool _transitioning;

        // The Pass & Play mode-select panel is left active in the editor (so it's
        // easy to lay out), so hide it at startup — otherwise it shows on top of
        // the menu and the Pass & Play button appears to do nothing until Back is
        // pressed. Mirrors how the panels (BidPanel/PassDevicePanel) self-hide in
        // Start(). The seat-picker hides itself in its own Start().
        private void Start()
        {
            _modeSelectPanel?.SetActive(false);
        }

        public void OnOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOne;
            GameSelection.PassAndPlay  = false;
            BeginTransition(_oneVsOneScene);
        }

        /// <summary>
        /// 1v1 pass-and-play (hotseat): same 1v1 scene, but both seats are human
        /// and share the device. Sets <see cref="GameSelection.PassAndPlay"/> so
        /// the scene's GameManager marks all seats human and runs the
        /// pass-the-device gate between turns.
        /// </summary>
        public void OnOneVsOneHotseatClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOne;
            GameSelection.PassAndPlay  = true;
            BeginTransition(_oneVsOneScene);
        }

        /// <summary>Classic 2v2: 6 cards each, all dealt, no draw pile.</summary>
        public void OnTwoVsTwoClassicClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwo;
            GameSelection.PassAndPlay  = false;
            BeginTransition(_twoVsTwoScene);
        }

        /// <summary>
        /// 2v2 "With Surprises": the reveal-and-draft variant — 5 dealt each,
        /// the leftover revealed and drafted one apiece after trump is declared.
        /// </summary>
        public void OnTwoVsTwoSurprisesClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwoDraft;
            GameSelection.PassAndPlay  = false;
            BeginTransition(_twoVsTwoScene);
        }

        /// <summary>
        /// 1v1v1: three independent players (free-for-all). 7 cards each, the
        /// 3-card leftover revealed and drafted one apiece after trump.
        /// </summary>
        public void OnOneVsOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOneVsOne;
            GameSelection.PassAndPlay  = false;
            BeginTransition(_oneVsOneVsOneScene);
        }

        public void OnQuitClicked()
        {
            if (_transitioning) return;
            _transitioning = true;
            StartCoroutine(PlayClickThen(QuitGame));
        }

        // ── Pass & Play: mode select → (optional) seat picker ─────────────────

        /// <summary>
        /// Opens the pass-and-play mode-select panel. Wire the existing
        /// "Pass & Play" main-menu button to this.
        /// </summary>
        public void OnPassAndPlayClicked() => ShowModeSelect();

        /// <summary>Shows the mode-select panel and hides the seat-picker.</summary>
        public void ShowModeSelect()
        {
            _seatSelection?.Hide();
            _modeSelectPanel?.SetActive(true);
        }

        /// <summary>Closes the mode-select panel (e.g. a Back button on it).</summary>
        public void HideModeSelect() => _modeSelectPanel?.SetActive(false);

        /// <summary>
        /// Pass-and-play 1v1: both seats are human, no seat-picker needed. Same
        /// as <see cref="OnOneVsOneHotseatClicked"/> — set the hotseat flag and
        /// load the 1v1 scene.
        /// </summary>
        public void OnModeOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOne;
            GameSelection.PassAndPlay  = true;
            GameSelection.HumanSeats   = null;
            HideModeSelect();
            BeginTransition(_oneVsOneScene);
        }

        /// <summary>1v1v1 (FFA): open the seat-picker so the player chooses humans.</summary>
        public void OnModeOneVsOneVsOneClicked() => OpenSeatPicker(GameMode.OneVsOneVsOne);

        /// <summary>2v2 classic: open the seat-picker.</summary>
        public void OnModeTwoVsTwoClassicClicked() => OpenSeatPicker(GameMode.TwoVsTwo);

        /// <summary>2v2 "with surprises" draft variant: open the seat-picker.</summary>
        public void OnModeTwoVsTwoDraftClicked() => OpenSeatPicker(GameMode.TwoVsTwoDraft);

        /// <summary>
        /// Hands off to the shared seat-picker configured for <paramref name="mode"/>.
        /// If no picker is wired, falls back to launching the mode with the
        /// default seat-0-human layout so the menu still works.
        /// </summary>
        private void OpenSeatPicker(GameMode mode)
        {
            if (_seatSelection == null)
            {
                // No picker in the scene yet: launch with the engine's default
                // (seat 0 human) rather than dead-ending the button.
                GameSelection.SelectedMode = mode;
                GameSelection.PassAndPlay  = false;
                GameSelection.HumanSeats   = null;
                LaunchSelectedMode(mode);
                return;
            }

            HideModeSelect();
            _seatSelection.Configure(mode);
        }

        /// <summary>
        /// Loads the scene for <paramref name="mode"/>, reusing the shared
        /// click-sound transition. Called by the seat-picker once the player has
        /// confirmed (GameSelection is already populated by that point). The
        /// classic-vs-draft 2v2 distinction rides in GameSelection.SelectedMode;
        /// both load the same "2v2" scene.
        /// </summary>
        public void LaunchSelectedMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.OneVsOne:
                    BeginTransition(_oneVsOneScene);
                    break;
                case GameMode.OneVsOneVsOne:
                    BeginTransition(_oneVsOneVsOneScene);
                    break;
                case GameMode.TwoVsTwo:
                case GameMode.TwoVsTwoDraft:
                    BeginTransition(_twoVsTwoScene);
                    break;
                default:
                    BeginTransition(_oneVsOneScene);
                    break;
            }
        }

        // ── Transition helpers ────────────────────────────────────────────────

        /// <summary>
        /// Plays the click sound, waits long enough for it to be heard, then
        /// loads <paramref name="sceneName"/>. Ignored if a transition is
        /// already running.
        /// </summary>
        private void BeginTransition(string sceneName)
        {
            if (_transitioning) return;
            _transitioning = true;
            StartCoroutine(PlayClickThen(() => SceneManager.LoadScene(sceneName)));
        }

        /// <summary>
        /// Plays the menu-button sound, waits for its duration (capped by
        /// <see cref="_maxClickDelay"/>), then runs <paramref name="after"/>.
        /// With no SoundManager / clip, the wait is zero so the action is
        /// effectively immediate.
        /// </summary>
        private IEnumerator PlayClickThen(System.Action after)
        {
            float wait = _sound != null ? _sound.PlayMenuButton() : 0f;
            wait = Mathf.Min(wait, _maxClickDelay);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);
            after?.Invoke();
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
