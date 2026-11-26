using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Controls when GameManager starts in multiplayer mode.
    /// Waits for Host/Join before allowing the game to begin.
    /// 
    /// Usage:
    /// 1. Attach to the same GameObject as GameManager
    /// 2. Disable GameManager component initially
    /// 3. This script will enable it once network is ready
    /// </summary>
    public class NetworkGameStarter : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private bool _waitForNetwork = true;
        
        private bool _networkReady = false;
        
        private void Start()
        {
            if (!_waitForNetwork)
            {
                // Skip network wait - start immediately (local game)
                if (_gameManager != null)
                    _gameManager.enabled = true;
                return;
            }
            
            // Disable GameManager until network is ready
            if (_gameManager != null)
            {
                _gameManager.enabled = false;
                Debug.Log("[NetworkGameStarter] GameManager disabled - waiting for network connection");
            }
            
            // Subscribe to network events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            }
        }
        
        private void OnServerStarted()
        {
            Debug.Log("[NetworkGameStarter] Server started - game can begin");
            StartGame();
        }
        
        private void OnClientConnected(ulong clientId)
        {
            // If we're a client and we just connected
            if (!NetworkManager.Singleton.IsServer && 
                clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[NetworkGameStarter] Client connected - game can begin");
                StartGame();
            }
        }
        
        private void StartGame()
        {
            if (_networkReady) return; // Already started
            
            _networkReady = true;
            
            if (_gameManager != null)
            {
                _gameManager.enabled = true;
                Debug.Log("[NetworkGameStarter] GameManager enabled - starting game!");
            }
        }
        
        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            }
        }
    }
}
