using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Simple flag to tell GameManager whether we're in network mode.
    /// GameManager can check NetworkGameMode.IsNetworkGame to decide
    /// whether to use network synchronization or local AI.
    /// </summary>
    public static class NetworkGameMode
    {
        /// <summary>
        /// Returns true if this is a networked multiplayer game.
        /// </summary>
        public static bool IsNetworkGame
        {
            get
            {
                return NetworkManager.Singleton != null &&
                       (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);
            }
        }
        
        /// <summary>
        /// Returns true if this is the host/server.
        /// </summary>
        public static bool IsHost
        {
            get
            {
                return NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsHost;
            }
        }
        
        /// <summary>
        /// Returns true if this is a client (not host).
        /// </summary>
        public static bool IsClient
        {
            get
            {
                return NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsClient &&
                       !NetworkManager.Singleton.IsHost;
            }
        }
        
        /// <summary>
        /// Gets the local player's client ID.
        /// </summary>
        public static ulong LocalClientId
        {
            get
            {
                if (NetworkManager.Singleton == null)
                    return 0;
                return NetworkManager.Singleton.LocalClientId;
            }
        }
    }
}
