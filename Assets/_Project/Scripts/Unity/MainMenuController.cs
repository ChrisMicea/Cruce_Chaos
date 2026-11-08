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

        [Tooltip("Upper bound (seconds) on how long a click can delay the scene " +
                 "load / quit while its sound plays. Guards against an " +
                 "accidentally long clip freezing the menu.")]
        [SerializeField] [Range(0f, 1f)] private float _maxClickDelay = 0.4f;

        // Guards against a second click firing another coroutine mid-transition
        // (e.g. mashing two buttons) — once a transition starts, further clicks
        // are ignored.
        private bool _transitioning;

        public void OnOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOne;
            BeginTransition(_oneVsOneScene);
        }

        /// <summary>Classic 2v2: 6 cards each, all dealt, no draw pile.</summary>
        public void OnTwoVsTwoClassicClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwo;
            BeginTransition(_twoVsTwoScene);
        }

        /// <summary>
        /// 2v2 "With Surprises": the reveal-and-draft variant — 5 dealt each,
        /// the leftover revealed and drafted one apiece after trump is declared.
        /// </summary>
        public void OnTwoVsTwoSurprisesClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwoDraft;
            BeginTransition(_twoVsTwoScene);
        }

        /// <summary>
        /// 1v1v1: three independent players (free-for-all). 7 cards each, the
        /// 3-card leftover revealed and drafted one apiece after trump.
        /// </summary>
        public void OnOneVsOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOneVsOne;
            BeginTransition(_oneVsOneVsOneScene);
        }

        public void OnQuitClicked()
        {
            if (_transitioning) return;
            _transitioning = true;
            StartCoroutine(PlayClickThen(QuitGame));
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
