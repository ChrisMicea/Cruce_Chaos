using Unity.Netcode;
using global::Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace CruceChaos.Unity
{
    public class NetworkTestUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        
        void Start()
        {
            hostButton.onClick.AddListener(StartHost);
            clientButton.onClick.AddListener(StartClient);
        }
        
        void StartHost()
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Started as Host");
            gameObject.SetActive(false); // Hide buttons
        }
        
        void StartClient()
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager.Singleton.StartClient();
            Debug.Log("Started as Client");
            gameObject.SetActive(false); // Hide buttons
        }
    }
}
