using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Synchronizes game actions (card plays, bids, trump declarations, marriages)
    /// across the network for multiplayer Cruce.
    /// 
    /// Responsibilities:
    ///   - Receives player actions via ServerRpc (from clients)
    ///   - Validates actions on the server against Core game rules
    ///   - Broadcasts validated actions to all clients via ClientRpc
    ///   - Provides events that GameManager subscribes to
    ///   - Serializes/deserializes Card objects for network transmission
    /// 
    /// Flow:
    ///   Client → ServerRpc → Server validates → ClientRpc → All clients execute
    /// 
    /// This ensures the server is authoritative: clients request actions,
    /// but only the server decides if they're legal and broadcasts results.
    /// </summary>
    public class NetworkCardSync : NetworkBehaviour
    {
        public static NetworkCardSync Instance { get; private set; }

        // ── Events (GameManager subscribes to these) ──────────────────────────

        /// <summary>Fired when any player plays a card. (seatIndex, card)</summary>
        public System.Action<int, Card> OnCardPlayed;

        /// <summary>Fired when any player makes a bid. (seatIndex, bid)</summary>
        public System.Action<int, Bid> OnBidMade;

        /// <summary>Fired when the bid winner declares trump. (trump suit)</summary>
        public System.Action<Suit> OnTrumpDeclared;

        /// <summary>Fired when a player announces a marriage. (seatIndex, suit)</summary>
        public System.Action<int, Suit> OnMarriageAnnounced;

        /// <summary>Fired when the dealer deals cards (triggers hand rebuild on clients).</summary>
        public System.Action OnCardsDealt;

        /// <summary>Fired when a trick is resolved. (winningSeat, points)</summary>
        public System.Action<int, int> OnTrickResolved;

        /// <summary>Fired when a round ends and scores are calculated.</summary>
        public System.Action OnRoundEnded;

        // ── Validation Context (set by GameManager) ───────────────────────────

        // The server needs access to the current game state to validate moves.
        // GameManager sets these before each validation point.
        private RoundParticipant[] _participants;
        private Suit? _currentTrump;
        private int _currentLeaderSeat;
        private Trick _currentTrick;

        // ── Singleton Setup ───────────────────────────────────────────────────

        private void Awake()
        {
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
                Debug.Log("[NetworkCardSync] Server initialized");
            }
        }

        public override void OnNetworkDespawn()
        {
            // Cleanup when network despawns
        }

        private void OnDestroy()
        {
            // Clear singleton reference
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ── Context Setters (called by GameManager on server) ────────────────

        /// <summary>
        /// Server: Updates the validation context before accepting player actions.
        /// GameManager calls this at the start of each trick.
        /// </summary>
        public void SetValidationContext(
            RoundParticipant[] participants,
            Suit? trump,
            int leaderSeat,
            Trick currentTrick)
        {
            if (!IsServer) return;

            _participants = participants;
            _currentTrump = trump;
            _currentLeaderSeat = leaderSeat;
            _currentTrick = currentTrick;
        }

        // ── Card Play Synchronization ─────────────────────────────────────────

        /// <summary>
        /// Client: Requests to play a card.
        /// The server validates and broadcasts if legal.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PlayCardServerRpc(string cardData, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            int seatIndex = NetworkGameManager.Instance.GetSeatForClient(senderClientId);

            Debug.Log($"[NetworkCardSync] Client {senderClientId} (seat {seatIndex}) " +
                      $"requests to play: {cardData}");

            // Deserialize card
            if (!TryParseCard(cardData, out Card card))
            {
                Debug.LogWarning($"[NetworkCardSync] Invalid card data: {cardData}");
                return;
            }

            // Validate the move on the server
            if (!IsValidCardPlay(seatIndex, card))
            {
                Debug.LogWarning($"[NetworkCardSync] Illegal card play by seat {seatIndex}: {card}");
                // Optionally notify the client their move was rejected
                return;
            }

            // Valid move — broadcast to all clients
            PlayCardClientRpc(seatIndex, cardData);
        }

        /// <summary>
        /// All clients: Execute the card play.
        /// GameManager handles the actual game logic.
        /// </summary>
        [ClientRpc]
        private void PlayCardClientRpc(int seatIndex, string cardData)
        {
            if (!TryParseCard(cardData, out Card card))
            {
                Debug.LogError($"[NetworkCardSync] Failed to parse card in ClientRpc: {cardData}");
                return;
            }

            Debug.Log($"[NetworkCardSync] Seat {seatIndex} played {card}");
            OnCardPlayed?.Invoke(seatIndex, card);
        }

        /// <summary>
        /// Server-side validation: Is this card play legal?
        /// Uses Core game rules (must follow suit, trump rules, etc.)
        /// </summary>
        private bool IsValidCardPlay(int seatIndex, Card card)
        {
            // Basic checks
            if (_participants == null || seatIndex < 0 || seatIndex >= _participants.Length)
                return false;

            var participant = _participants[seatIndex];
            if (participant == null) return false;

            // Check if player has this card
            if (!participant.Hand.Contains(card))
            {
                Debug.LogWarning($"[NetworkCardSync] Seat {seatIndex} doesn't have {card}");
                return false;
            }

            // TODO: Add Core rule validation here
            // - Must follow suit if possible
            // - Must trump if can't follow suit
            // - Must play higher if following suit
            // This requires integrating with your LegalMoveEvaluator

            return true;
        }

        // ── Bidding Synchronization ───────────────────────────────────────────

        /// <summary>
        /// Client: Requests to make a bid.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void MakeBidServerRpc(int bidValue, bool isPass, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            int seatIndex = NetworkGameManager.Instance.GetSeatForClient(senderClientId);

            Debug.Log($"[NetworkCardSync] Client {senderClientId} (seat {seatIndex}) " +
                      $"bids: {(isPass ? "PASS" : bidValue.ToString())}");

            // Create Bid object
            Bid bid = isPass ? Bid.Pass : Bid.Of(bidValue);

            // Validation could be added here (is it this player's turn to bid?)

            // Broadcast to all clients
            MakeBidClientRpc(seatIndex, bidValue, isPass);
        }

        [ClientRpc]
        private void MakeBidClientRpc(int seatIndex, int bidValue, bool isPass)
        {
            Bid bid = isPass ? Bid.Pass : Bid.Of(bidValue);
            Debug.Log($"[NetworkCardSync] Seat {seatIndex} bid: {bid}");
            OnBidMade?.Invoke(seatIndex, bid);
        }

        // ── Trump Declaration Synchronization ─────────────────────────────────

        /// <summary>
        /// Client: Bid winner declares trump suit.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DeclareTrumpServerRpc(int suitValue, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            int seatIndex = NetworkGameManager.Instance.GetSeatForClient(senderClientId);

            Suit trump = (Suit)suitValue;
            Debug.Log($"[NetworkCardSync] Client {senderClientId} (seat {seatIndex}) " +
                      $"declares trump: {trump}");

            // Validation: is this player the bid winner?
            // (GameManager should track this)

            DeclareTrumpClientRpc(suitValue);
        }

        [ClientRpc]
        private void DeclareTrumpClientRpc(int suitValue)
        {
            Suit trump = (Suit)suitValue;
            Debug.Log($"[NetworkCardSync] Trump declared: {trump}");
            OnTrumpDeclared?.Invoke(trump);
        }

        // ── Marriage Announcement Synchronization ─────────────────────────────

        /// <summary>
        /// Client: Announces a marriage (Queen and King of same suit).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void AnnounceMarriageServerRpc(int suitValue, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            int seatIndex = NetworkGameManager.Instance.GetSeatForClient(senderClientId);

            Suit suit = (Suit)suitValue;
            Debug.Log($"[NetworkCardSync] Client {senderClientId} (seat {seatIndex}) " +
                      $"announces marriage in {suit}");

            // Validation: does player have both Three and Four of this suit?
            // (In Cruce, Three = Queen, Four = King)

            AnnounceMarriageClientRpc(seatIndex, suitValue);
        }

        [ClientRpc]
        private void AnnounceMarriageClientRpc(int seatIndex, int suitValue)
        {
            Suit suit = (Suit)suitValue;
            Debug.Log($"[NetworkCardSync] Seat {seatIndex} announced marriage in {suit}");
            OnMarriageAnnounced?.Invoke(seatIndex, suit);
        }

        // ── Deal Synchronization ──────────────────────────────────────────────

        /// <summary>
        /// Server: Broadcasts that cards have been dealt.
        /// Each client receives their own hand separately (secure).
        /// </summary>
        public void BroadcastCardsDealt()
        {
            if (!IsServer) return;

            Debug.Log("[NetworkCardSync] Broadcasting cards dealt");
            CardsDealtClientRpc();
        }

        [ClientRpc]
        private void CardsDealtClientRpc()
        {
            Debug.Log("[NetworkCardSync] Cards dealt");
            OnCardsDealt?.Invoke();
        }

        /// <summary>
        /// Server: Sends a specific player their hand (private, only they see it).
        /// For now, this is a placeholder - we'll implement proper hand sync later.
        /// </summary>
        public void SendHandToClient(ulong clientId, Card[] hand)
        {
            if (!IsServer) return;

            // TODO: Implement secure hand distribution
            // For now, just log it
            Debug.Log($"[NetworkCardSync] Would send {hand.Length} cards to client {clientId}");
        }

        // ── Trick Resolution Synchronization ──────────────────────────────────

        /// <summary>
        /// Server: Broadcasts trick winner and points captured.
        /// </summary>
        public void BroadcastTrickResolved(int winningSeat, int points)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkCardSync] Trick won by seat {winningSeat}, {points} points");
            TrickResolvedClientRpc(winningSeat, points);
        }

        [ClientRpc]
        private void TrickResolvedClientRpc(int winningSeat, int points)
        {
            Debug.Log($"[NetworkCardSync] Trick resolved: seat {winningSeat} wins {points} points");
            OnTrickResolved?.Invoke(winningSeat, points);
        }

        // ── Round End Synchronization ─────────────────────────────────────────

        /// <summary>
        /// Server: Broadcasts that the round has ended.
        /// </summary>
        public void BroadcastRoundEnded()
        {
            if (!IsServer) return;

            Debug.Log("[NetworkCardSync] Broadcasting round ended");
            RoundEndedClientRpc();
        }

        [ClientRpc]
        private void RoundEndedClientRpc()
        {
            Debug.Log("[NetworkCardSync] Round ended");
            OnRoundEnded?.Invoke();
        }

        // ── Card Serialization (for network transmission) ─────────────────────

        /// <summary>
        /// Serializes a Card to a string for network transmission.
        /// Format: "Suit:Rank" (e.g., "0:5" for Green Ace)
        /// </summary>
        public static string SerializeCard(Card card)
        {
            return $"{(int)card.Suit}:{(int)card.Rank}";
        }

        /// <summary>
        /// Deserializes a Card from a network string.
        /// Returns true if successful.
        /// </summary>
        public static bool TryParseCard(string cardData, out Card card)
        {
            card = default;

            if (string.IsNullOrEmpty(cardData))
                return false;

            string[] parts = cardData.Split(':');
            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out int suitValue))
                return false;

            if (!int.TryParse(parts[1], out int rankValue))
                return false;

            // Validate enum ranges
            if (suitValue < 0 || suitValue > 3) // 4 suits
                return false;

            if (rankValue < 0 || rankValue > 5) // 6 ranks
                return false;

            card = new Card((Suit)suitValue, (Rank)rankValue);
            return true;
        }

        // ── Helper: Get current trick state ───────────────────────────────────

        /// <summary>
        /// Returns a summary of the current trick for debugging.
        /// </summary>
        public string GetTrickState()
        {
            if (_currentTrick == null)
                return "No active trick";

            return $"Trick: {_currentTrick.Plays.Count} cards played";
        }
    }
}
