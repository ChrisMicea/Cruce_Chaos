using Unity.Netcode;
using global::Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Simple UI for testing LAN multiplayer.
    /// Shows Host and Join buttons at the start of the game.
    /// 
    /// Usage:
    /// 1. Create a Canvas with this script
    /// 2. Add two buttons (Host and Join)
    /// 3. Add a text field for entering IP address
    /// 4. Add a status text to show connection info
    /// 5. Wire everything up in the inspector
    /// </summary>
    public class NetworkTestUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private TMP_InputField _ipAddressInput;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _connectionPanel;
        
        [Header("Settings")]
        [SerializeField] private ushort _port = 7777;
        
        private void Start()
        {
            // Wire up button clicks
            if (_hostButton != null)
                _hostButton.onClick.AddListener(OnHostClicked);
            
            if (_joinButton != null)
                _joinButton.onClick.AddListener(OnJoinClicked);
            
            // Set default IP to localhost
            if (_ipAddressInput != null)
                _ipAddressInput.text = "127.0.0.1";
            
            UpdateStatus("Choose Host or Join");
        }
        
        private void OnHostClicked()
        {
            UpdateStatus("Starting as Host...");
            
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                UpdateStatus("ERROR: NetworkManager not found in scene!");
                return;
            }
            
            // Configure connection approval (limit players, etc.)
            networkManager.ConnectionApprovalCallback = ApprovalCheck;
            
            // Start as host
            bool success = networkManager.StartHost();
            
            if (success)
            {
                UpdateStatus($"✓ Hosting on port {_port}\nWaiting for players...");
                HideConnectionPanel();
                
                // Subscribe to connection events
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                UpdateStatus("✗ Failed to start host");
            }
        }
        
        private void OnJoinClicked()
        {
            if (_ipAddressInput == null)
            {
                UpdateStatus("ERROR: IP Address input not assigned!");
                return;
            }
            
            string ipAddress = _ipAddressInput.text.Trim();
            if (string.IsNullOrEmpty(ipAddress))
            {
                UpdateStatus("Please enter host IP address");
                return;
            }
            
            UpdateStatus($"Connecting to {ipAddress}:{_port}...");
            
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                UpdateStatus("ERROR: NetworkManager not found in scene!");
                return;
            }
            
            // Get the Unity Transport component and set connection data
            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, _port);
            }
            else
            {
                UpdateStatus("ERROR: UnityTransport not found!");
                return;
            }
            
            // Start as client
            bool success = networkManager.StartClient();
            
            if (success)
            {
                UpdateStatus($"Connecting to {ipAddress}...");
                HideConnectionPanel();
                
                // Subscribe to connection events
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                UpdateStatus("✗ Failed to connect");
            }
        }
        
        /// <summary>
        /// Connection approval callback - only allow up to 4 players
        /// </summary>
        private void ApprovalCheck(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Count current players
            int currentPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
            
            // Allow if under max players (4 for Cruce)
            response.Approved = currentPlayers < 4;
            response.CreatePlayerObject = true;
            
            if (!response.Approved)
            {
                response.Reason = "Server is full (max 4 players)";
                Debug.Log($"Connection rejected: {response.Reason}");
            }
        }
        
        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
                UpdateStatus($"✓ Hosting - {playerCount} player(s) connected");
                Debug.Log($"Client {clientId} connected. Total players: {playerCount}");
            }
            else if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                UpdateStatus("✓ Connected to host!");
                Debug.Log("Successfully connected to host");
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
                UpdateStatus($"✓ Hosting - {playerCount} player(s) connected");
                Debug.Log($"Client {clientId} disconnected. Total players: {playerCount}");
            }
            else if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                UpdateStatus("✗ Disconnected from host");
                ShowConnectionPanel();
            }
        }
        
        private void UpdateStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
            
            Debug.Log($"[NetworkTestUI] {message}");
        }
        
        private void HideConnectionPanel()
        {
            if (_connectionPanel != null)
                _connectionPanel.SetActive(false);
        }
        
        private void ShowConnectionPanel()
        {
            if (_connectionPanel != null)
                _connectionPanel.SetActive(true);
        }
        
        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }
}
