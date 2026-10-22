using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Attach directly to any UI element (Text, Image, Panel) to make it
    /// load a scene when clicked. Does not require a Button component.
    /// </summary>
    public class MenuButtonClick : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Exact name of the scene to load when this object is clicked.")]
        [SerializeField] private string _sceneName;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                Debug.LogWarning("[MenuButtonClick] Scene name is empty!");
                return;
            }
            Debug.Log($"[MenuButtonClick] Loading scene: {_sceneName}");
            SceneManager.LoadScene(_sceneName);
        }
    }
}
