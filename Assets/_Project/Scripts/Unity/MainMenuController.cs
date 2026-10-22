using UnityEngine;
using UnityEngine.SceneManagement;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Handles navigation from the Main Menu scene.
    /// Attach this to any GameObject in the Main Menu scene and wire
    /// the buttons/text buttons to the methods below.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Exact name of the 1v1 game scene (must match the scene filename).")]
        [SerializeField] private string _oneVsOneScene = "1vsAI";

        public void OnOneVsOneClicked()
        {
            SceneManager.LoadScene(_oneVsOneScene);
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
