using Unity.Netcode;
using UnityEngine;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Bridge between GameManager and NetworkCardSync.
    /// Handles sending local player actions to network and receiving remote actions.
    /// 
    /// Attach this to the same GameObject as GameManager.
    /// It will intercept player actions and route them through the network.
    /// </summary>
    public class NetworkGameManagerBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager _gameManager;
        
        private NetworkCardSync _networkCardSync;
        private NetworkGameManager _networkGameManager;
        private bool _isNetworkGame;
        
        // Pending actions waiting for network confirmation
        private bool _waitingForNetworkAction;
        private System.Action _pendingAction;
        
        private void Start()
        {
            // Check if this is a network game
            _networkCardSync = NetworkCardSync.Instance;
            _networkGameManager = NetworkGameManager.Instance;
            _isNetworkGame = NetworkGameMode.IsNetworkGame;
            
            if (_isNetworkGame)
            {
                Debug.Log("[NetworkBridge] Network game detected - setting up callbacks");
                SetupNetworkCallbacks();
            }
            else
            {
                Debug.LogWarning("[NetworkBridge] Local game - network disabled");
                Debug.LogWarning($"[NetworkBridge] NetworkManager.Singleton = {NetworkManager.Singleton}");
                Debug.LogWarning($"[NetworkBridge] IsServer = {(NetworkManager.Singleton != null ? NetworkManager.Singleton.IsServer.ToString() : "null")}");
                Debug.LogWarning($"[NetworkBridge] IsClient = {(NetworkManager.Singleton != null ? NetworkManager.Singleton.IsClient.ToString() : "null")}");
            }
        }
        
        private void SetupNetworkCallbacks()
        {
            if (_networkCardSync == null)
            {
                Debug.LogError("[NetworkBridge] NetworkCardSync not found!");
                return;
            }
            
            // Subscribe to network events
            _networkCardSync.OnCardPlayed += HandleNetworkCardPlay;
            _networkCardSync.OnBidMade += HandleNetworkBid;
            _networkCardSync.OnTrumpDeclared += HandleNetworkTrump;
            _networkCardSync.OnMarriageAnnounced += HandleNetworkMarriage;
            
            Debug.Log("[NetworkBridge] Network callbacks registered");
        }
        
        private void OnDestroy()
        {
            if (_networkCardSync != null)
            {
                _networkCardSync.OnCardPlayed -= HandleNetworkCardPlay;
                _networkCardSync.OnBidMade -= HandleNetworkBid;
                _networkCardSync.OnTrumpDeclared -= HandleNetworkTrump;
                _networkCardSync.OnMarriageAnnounced -= HandleNetworkMarriage;
            }
        }
        
        // ── Public API for GameManager to Call ────────────────────────────────
        
        /// <summary>
        /// Call this instead of directly playing a card.
        /// If network game, sends to server for validation.
        /// If local game, plays immediately.
        /// </summary>
        public void RequestPlayCard(Card card, System.Action onConfirmed)
        {
            if (!_isNetworkGame)
            {
                // Local game - play immediately
                onConfirmed?.Invoke();
                return;
            }
            
            // Network game - send to server
            Debug.Log($"[NetworkBridge] Requesting card play: {card}");
            _pendingAction = onConfirmed;
            _waitingForNetworkAction = true;
            
            string cardData = NetworkCardSync.SerializeCard(card);
            _networkCardSync.PlayCardServerRpc(cardData);
        }
        
        /// <summary>
        /// Call this when local player makes a bid.
        /// </summary>
        public void RequestBid(Bid bid, System.Action onConfirmed)
        {
            if (!_isNetworkGame)
            {
                onConfirmed?.Invoke();
                return;
            }
            
            Debug.Log($"[NetworkBridge] Requesting bid: {bid}");
            _pendingAction = onConfirmed;
            _waitingForNetworkAction = true;
            
            _networkCardSync.MakeBidServerRpc(bid.Value, bid.IsPass);
        }
        
        /// <summary>
        /// Call this when local player declares trump.
        /// </summary>
        public void RequestDeclareTrump(Suit trump, System.Action onConfirmed)
        {
            if (!_isNetworkGame)
            {
                onConfirmed?.Invoke();
                return;
            }
            
            Debug.Log($"[NetworkBridge] Requesting trump: {trump}");
            _pendingAction = onConfirmed;
            _waitingForNetworkAction = true;
            
            _networkCardSync.DeclareTrumpServerRpc((int)trump);
        }
        
        /// <summary>
        /// Call this when local player announces marriage.
        /// </summary>
        public void RequestAnnounceMarriage(Suit suit, System.Action onConfirmed)
        {
            if (!_isNetworkGame)
            {
                onConfirmed?.Invoke();
                return;
            }
            
            Debug.Log($"[NetworkBridge] Requesting marriage: {suit}");
            _pendingAction = onConfirmed;
            _waitingForNetworkAction = true;
            
            _networkCardSync.AnnounceMarriageServerRpc((int)suit);
        }
        
        // ── Network Event Handlers ────────────────────────────────────────────
        
        private void HandleNetworkCardPlay(int seatIndex, Card card)
        {
            Debug.Log($"[NetworkBridge] Card played by seat {seatIndex}: {card}");
            
            // Get local client's seat
            int localSeat = _networkGameManager.GetLocalClientSeat();
            
            if (seatIndex == localSeat)
            {
                // This is our own action coming back from server - confirm it
                if (_waitingForNetworkAction && _pendingAction != null)
                {
                    _waitingForNetworkAction = false;
                    var action = _pendingAction;
                    _pendingAction = null;
                    action.Invoke();
                }
            }
            else
            {
                // Another player's action - GameManager should process this
                // TODO: GameManager needs a public method to handle remote player actions
                Debug.Log($"[NetworkBridge] Remote player {seatIndex} played {card}");
            }
        }
        
        private void HandleNetworkBid(int seatIndex, Bid bid)
        {
            Debug.Log($"[NetworkBridge] Bid by seat {seatIndex}: {bid}");
            
            int localSeat = _networkGameManager.GetLocalClientSeat();
            
            if (seatIndex == localSeat && _waitingForNetworkAction)
            {
                _waitingForNetworkAction = false;
                _pendingAction?.Invoke();
                _pendingAction = null;
            }
        }
        
        private void HandleNetworkTrump(Suit trump)
        {
            Debug.Log($"[NetworkBridge] Trump declared: {trump}");
            
            if (_waitingForNetworkAction)
            {
                _waitingForNetworkAction = false;
                _pendingAction?.Invoke();
                _pendingAction = null;
            }
        }
        
        private void HandleNetworkMarriage(int seatIndex, Suit suit)
        {
            Debug.Log($"[NetworkBridge] Marriage announced by seat {seatIndex}: {suit}");
            
            int localSeat = _networkGameManager.GetLocalClientSeat();
            
            if (seatIndex == localSeat && _waitingForNetworkAction)
            {
                _waitingForNetworkAction = false;
                _pendingAction?.Invoke();
                _pendingAction = null;
            }
        }
        
        // ── Utility ────────────────────────────────────────────────────────────
        
        public bool IsNetworkGame => _isNetworkGame;
        
        public bool IsWaitingForNetwork => _waitingForNetworkAction;
    }
}
