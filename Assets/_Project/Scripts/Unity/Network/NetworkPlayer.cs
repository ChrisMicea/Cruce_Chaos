using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Network player identity and seat assignment.
    /// Each connected client has one NetworkPlayer spawned for them.
    /// 
    /// Responsibilities:
    ///   - Stores player name (synchronized across network)
    ///   - Tracks seat assignment (which position at the table)
    ///   - Provides player ready state for game start
    ///   - Owned by one client but visible to all
    /// 
    /// Unity's NetworkManager automatically spawns one player object per
    /// connected client. This should be assigned as the Player Prefab in
    /// the NetworkManager component.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        // Network variables automatically sync from owner to all clients
        // FixedString types are required for NetworkVariable<string>
        public NetworkVariable<global::Unity.Collections.FixedString64Bytes> PlayerName = 
            new NetworkVariable<global::Unity.Collections.FixedString64Bytes>(
                new global::Unity.Collections.FixedString64Bytes(),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        public NetworkVariable<int> SeatIndex = 
            new NetworkVariable<int>(
                -1,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsReady = 
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        // Local cache for quick access
        private string _cachedPlayerName;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            // Owner (the player who owns this NetworkPlayer) sets their name
            if (IsOwner)
            {
                // Default name based on client ID
                string defaultName = $"Player {OwnerClientId + 1}";
                SetPlayerNameServerRpc(defaultName);
                
                Debug.Log($"[NetworkPlayer] Local player spawned: {defaultName}");
            }

            // All clients (including owner) listen for name changes
            PlayerName.OnValueChanged += OnPlayerNameChanged;
            SeatIndex.OnValueChanged += OnSeatIndexChanged;
            IsReady.OnValueChanged += OnReadyStateChanged;

            // Cache the current name
            _cachedPlayerName = PlayerName.Value.ToString();
        }

        public override void OnNetworkDespawn()
        {
            PlayerName.OnValueChanged -= OnPlayerNameChanged;
            SeatIndex.OnValueChanged -= OnSeatIndexChanged;
            IsReady.OnValueChanged -= OnReadyStateChanged;
        }

        // ── Name Management ───────────────────────────────────────────────────

        /// <summary>
        /// Owner calls this to set their display name.
        /// The server receives and validates it, then it auto-syncs to all clients.
        /// </summary>
        [ServerRpc]
        private void SetPlayerNameServerRpc(string name)
        {
            // Validate name (length, profanity filter, etc.)
            string validatedName = ValidatePlayerName(name);
            
            // Set the network variable (auto-syncs to all clients)
            PlayerName.Value = validatedName;
            
            Debug.Log($"[NetworkPlayer] Server set name for client {OwnerClientId}: {validatedName}");
        }

        /// <summary>
        /// Public method for owner to change their name.
        /// </summary>
        public void SetName(string name)
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[NetworkPlayer] Only the owner can set their name");
                return;
            }

            SetPlayerNameServerRpc(name);
        }

        /// <summary>
        /// Server-side validation of player names.
        /// </summary>
        private string ValidatePlayerName(string name)
        {
            // Trim whitespace
            name = name?.Trim() ?? "";

            // Enforce length limits
            if (name.Length == 0)
                return $"Player {OwnerClientId + 1}";
            
            if (name.Length > 16)
                name = name.Substring(0, 16);

            // Add more validation as needed (profanity filter, etc.)
            
            return name;
        }

        /// <summary>
        /// Called on all clients when the player name changes.
        /// </summary>
        private void OnPlayerNameChanged(global::Unity.Collections.FixedString64Bytes previous, global::Unity.Collections.FixedString64Bytes current)
        {
            _cachedPlayerName = current.ToString();
            Debug.Log($"[NetworkPlayer] Player name changed: {previous} → {current}");
        }

        // ── Seat Assignment ───────────────────────────────────────────────────

        /// <summary>
        /// Server calls this to assign a seat to this player.
        /// NetworkGameManager handles the seat assignment logic.
        /// </summary>
        public void AssignSeat(int seatIndex)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[NetworkPlayer] Only the server can assign seats");
                return;
            }

            SeatIndex.Value = seatIndex;
            Debug.Log($"[NetworkPlayer] Assigned seat {seatIndex} to {GetPlayerName()}");
        }

        /// <summary>
        /// Called on all clients when seat assignment changes.
        /// </summary>
        private void OnSeatIndexChanged(int previous, int current)
        {
            Debug.Log($"[NetworkPlayer] Seat assignment changed for {GetPlayerName()}: {previous} → {current}");
        }

        // ── Ready State ───────────────────────────────────────────────────────

        /// <summary>
        /// Owner toggles their ready state.
        /// </summary>
        public void SetReady(bool ready)
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[NetworkPlayer] Only the owner can set ready state");
                return;
            }

            SetReadyServerRpc(ready);
        }

        [ServerRpc]
        private void SetReadyServerRpc(bool ready)
        {
            IsReady.Value = ready;
            Debug.Log($"[NetworkPlayer] {GetPlayerName()} is {(ready ? "ready" : "not ready")}");
        }

        /// <summary>
        /// Called on all clients when ready state changes.
        /// </summary>
        private void OnReadyStateChanged(bool previous, bool current)
        {
            Debug.Log($"[NetworkPlayer] Ready state changed for {GetPlayerName()}: {previous} → {current}");
        }

        // ── Public Accessors ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the player's current display name.
        /// Uses cached value for performance.
        /// </summary>
        public string GetPlayerName()
        {
            return _cachedPlayerName;
        }

        /// <summary>
        /// Returns the player's assigned seat index, or -1 if not assigned.
        /// </summary>
        public int GetSeat()
        {
            return SeatIndex.Value;
        }

        /// <summary>
        /// Returns true if this player is ready to start.
        /// </summary>
        public bool GetReadyState()
        {
            return IsReady.Value;
        }

        /// <summary>
        /// Returns true if this is the local player (owned by this client).
        /// </summary>
        public new bool IsLocalPlayer()
        {
            return IsOwner;
        }

        // ── Utility ───────────────────────────────────────────────────────────

        public override string ToString()
        {
            return $"NetworkPlayer(Name={GetPlayerName()}, Seat={GetSeat()}, " +
                   $"ClientId={OwnerClientId}, Ready={GetReadyState()})";
        }
    }
}
