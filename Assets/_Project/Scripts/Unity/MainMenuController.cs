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

        public void OnOneVsOneClicked()
        {
            GameSelection.SelectedMode = GameMode.OneVsOne;
            SceneManager.LoadScene(_oneVsOneScene);
        }

        /// <summary>Classic 2v2: 6 cards each, all dealt, no draw pile.</summary>
        public void OnTwoVsTwoClassicClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwo;
            SceneManager.LoadScene(_twoVsTwoScene);
        }

        /// <summary>
        /// 2v2 "With Surprises": the reveal-and-draft variant — 5 dealt each,
        /// the leftover revealed and drafted one apiece after trump is declared.
        /// </summary>
        public void OnTwoVsTwoSurprisesClicked()
        {
            GameSelection.SelectedMode = GameMode.TwoVsTwoDraft;
            SceneManager.LoadScene(_twoVsTwoScene);
        }

        public void OnQuitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
