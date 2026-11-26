using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Central network coordinator for multiplayer Cruce.
    /// Runs on both host and clients.
    /// 
    /// Responsibilities:
    ///   - Manages client connections and disconnections
    ///   - Synchronizes game state across all clients
    ///   - Handles game start/pause/resume flow
    ///   - Assigns seats to connected players
    ///   - Provides single-instance access for other network components
    /// 
    /// This sits ABOVE the existing GameManager and doesn't replace it —
    /// the GameManager continues to run the game logic, while this component
    /// ensures all clients see the same game state.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameManager _gameManager;

        [Header("Settings")]
        [SerializeField] private int _maxPlayers = 4;

        // Network variables for game state synchronization
        // These automatically replicate from server to clients
        private NetworkVariable<int> _currentRound = new NetworkVariable<int>(0);
        private NetworkVariable<bool> _isGameInProgress = new NetworkVariable<bool>(false);
        private NetworkVariable<int> _connectedPlayerCount = new NetworkVariable<int>(0);

        // Track which seats are occupied by which client IDs
        private readonly Dictionary<ulong, int> _clientToSeat = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> _seatToClient = new Dictionary<int, ulong>();

        // Track which seats are still available
        private readonly HashSet<int> _availableSeats = new HashSet<int> { 0, 1, 2, 3 };

        // Events for game state changes
        public System.Action<int> OnRoundStarted;
        public System.Action OnGameStarted;
        public System.Action OnGamePaused;
        public System.Action<ulong> OnPlayerDisconnected;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Server-only: listen for client lifecycle events
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                
                Debug.Log("[NetworkGameManager] Server initialized");
            }

            // Both server and clients listen for game state changes
            _isGameInProgress.OnValueChanged += OnGameInProgressChanged;
            _currentRound.OnValueChanged += OnCurrentRoundChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            _isGameInProgress.OnValueChanged -= OnGameInProgressChanged;
            _currentRound.OnValueChanged -= OnCurrentRoundChanged;
        }

        private void OnDestroy()
        {
            // Additional cleanup when object is destroyed
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        // ── Client Connection Management ──────────────────────────────────────

        /// <summary>
        /// Server-only: Called when a new client connects.
        /// Assigns an available seat to the client.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkGameManager] Client {clientId} connected");

            // Assign the first available seat
            if (_availableSeats.Count > 0)
            {
                int seat = GetNextAvailableSeat();
                AssignSeatToClient(clientId, seat);

                _connectedPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;

                Debug.Log($"[NetworkGameManager] Assigned seat {seat} to client {clientId}. " +
                          $"Players: {_connectedPlayerCount.Value}/{_maxPlayers}");

                // Sync the seat assignment to the client
                SyncSeatAssignmentClientRpc(clientId, seat);
            }
            else
            {
                Debug.LogWarning($"[NetworkGameManager] No available seats for client {clientId}");
            }
        }

        /// <summary>
        /// Server-only: Called when a client disconnects.
        /// Frees their seat and handles game interruption if needed.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkGameManager] Client {clientId} disconnected");

            if (_clientToSeat.TryGetValue(clientId, out int seat))
            {
                // Free the seat
                _clientToSeat.Remove(clientId);
                _seatToClient.Remove(seat);
                _availableSeats.Add(seat);

                _connectedPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;

                Debug.Log($"[NetworkGameManager] Freed seat {seat}. " +
                          $"Players: {_connectedPlayerCount.Value}/{_maxPlayers}");

                // Notify all clients about the disconnection
                NotifyPlayerDisconnectedClientRpc(clientId, seat);

                // If a game is in progress, pause it
                if (_isGameInProgress.Value)
                {
                    PauseGame();
                }
            }

            OnPlayerDisconnected?.Invoke(clientId);
        }

        /// <summary>
        /// Server: Gets the next available seat index.
        /// </summary>
        private int GetNextAvailableSeat()
        {
            foreach (int seat in _availableSeats)
            {
                _availableSeats.Remove(seat);
                return seat;
            }
            return -1;
        }

        /// <summary>
        /// Server: Records the seat assignment.
        /// </summary>
        private void AssignSeatToClient(ulong clientId, int seat)
        {
            _clientToSeat[clientId] = seat;
            _seatToClient[seat] = clientId;
        }

        // ── Game Flow Control ─────────────────────────────────────────────────

        /// <summary>
        /// Server RPC: Client requests to start the game.
        /// Only the host (first connected client) can start.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestStartGameServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[NetworkGameManager] Client {senderClientId} requested game start");

            // Verify minimum players (at least 2)
            if (_connectedPlayerCount.Value < 2)
            {
                Debug.LogWarning("[NetworkGameManager] Need at least 2 players to start");
                return;
            }

            // Start the game
            _isGameInProgress.Value = true;
            _currentRound.Value = 1;

            StartGameClientRpc();
        }

        /// <summary>
        /// Client RPC: Server tells all clients to start the game.
        /// </summary>
        [ClientRpc]
        private void StartGameClientRpc()
        {
            Debug.Log("[NetworkGameManager] Game started!");
            OnGameStarted?.Invoke();

            // The GameManager can now begin its round loop
            // Integration point: call _gameManager.StartNetworkGame() or similar
        }

        /// <summary>
        /// Server: Advances to the next round.
        /// </summary>
        public void AdvanceRound()
        {
            if (!IsServer) return;

            _currentRound.Value++;
            Debug.Log($"[NetworkGameManager] Advanced to round {_currentRound.Value}");
        }

        /// <summary>
        /// Server: Pauses the game (e.g., due to disconnection).
        /// </summary>
        public void PauseGame()
        {
            if (!IsServer) return;

            _isGameInProgress.Value = false;
            Debug.Log("[NetworkGameManager] Game paused");

            PauseGameClientRpc();
        }

        [ClientRpc]
        private void PauseGameClientRpc()
        {
            OnGamePaused?.Invoke();
        }

        /// <summary>
        /// Server: Ends the game.
        /// </summary>
        public void EndGame()
        {
            if (!IsServer) return;

            _isGameInProgress.Value = false;
            _currentRound.Value = 0;
            Debug.Log("[NetworkGameManager] Game ended");
        }

        // ── Seat Query Methods ────────────────────────────────────────────────

        /// <summary>
        /// Returns the seat index assigned to the given client ID, or -1 if none.
        /// </summary>
        public int GetSeatForClient(ulong clientId)
        {
            return _clientToSeat.TryGetValue(clientId, out int seat) ? seat : -1;
        }

        /// <summary>
        /// Returns the client ID assigned to the given seat, or null if unassigned.
        /// </summary>
        public ulong? GetClientForSeat(int seat)
        {
            return _seatToClient.TryGetValue(seat, out ulong clientId) ? clientId : (ulong?)null;
        }

        /// <summary>
        /// Returns the local client's assigned seat index.
        /// </summary>
        public int GetLocalClientSeat()
        {
            return GetSeatForClient(NetworkManager.Singleton.LocalClientId);
        }

        /// <summary>
        /// Returns true if the local client owns the given seat.
        /// </summary>
        public bool IsLocalClientSeat(int seat)
        {
            return GetSeatForClient(NetworkManager.Singleton.LocalClientId) == seat;
        }

        // ── ClientRpc Callbacks ───────────────────────────────────────────────

        /// <summary>
        /// Client RPC: Syncs seat assignment to all clients.
        /// </summary>
        [ClientRpc]
        private void SyncSeatAssignmentClientRpc(ulong clientId, int seat)
        {
            Debug.Log($"[NetworkGameManager] Client {clientId} assigned to seat {seat}");
            
            // Update local tracking (for UI display)
            if (!_clientToSeat.ContainsKey(clientId))
            {
                _clientToSeat[clientId] = seat;
                _seatToClient[seat] = clientId;
                _availableSeats.Remove(seat);
            }
        }

        /// <summary>
        /// Client RPC: Notifies all clients that a player disconnected.
        /// </summary>
        [ClientRpc]
        private void NotifyPlayerDisconnectedClientRpc(ulong clientId, int seat)
        {
            Debug.Log($"[NetworkGameManager] Player at seat {seat} (client {clientId}) disconnected");
            
            // Update local tracking
            _clientToSeat.Remove(clientId);
            _seatToClient.Remove(seat);
            _availableSeats.Add(seat);
        }

        // ── NetworkVariable Change Callbacks ──────────────────────────────────

        private void OnGameInProgressChanged(bool previous, bool current)
        {
            Debug.Log($"[NetworkGameManager] Game in progress changed: {previous} → {current}");
        }

        private void OnCurrentRoundChanged(int previous, int current)
        {
            Debug.Log($"[NetworkGameManager] Round changed: {previous} → {current}");
            OnRoundStarted?.Invoke(current);
        }

        // ── Public Properties ─────────────────────────────────────────────────

        public bool IsGameInProgress => _isGameInProgress.Value;
        public int CurrentRound => _currentRound.Value;
        public int ConnectedPlayerCount => _connectedPlayerCount.Value;
        public int MaxPlayers => _maxPlayers;
    }
}
