# Network Scripts for Cruce & Chaos Multiplayer

This folder contains the core networking scripts for LAN multiplayer functionality using Unity's Netcode for GameObjects.

## Files Overview

### 1. NetworkGameManager.cs
**Purpose**: Central coordinator for multiplayer games.

**Responsibilities**:
- Manages client connections and disconnections
- Assigns seats to players (0-3)
- Synchronizes game state (round number, game in progress)
- Provides single-instance access via `NetworkGameManager.Instance`

**Key Methods**:
- `RequestStartGameServerRpc()` - Client requests game start
- `GetSeatForClient(clientId)` - Get seat for a client
- `IsLocalClientSeat(seat)` - Check if local player owns a seat

**Events**:
- `OnGameStarted` - Game begins
- `OnRoundStarted` - New round begins
- `OnPlayerDisconnected` - Player leaves

---

### 2. NetworkPlayer.cs
**Purpose**: Represents one connected player.

**Responsibilities**:
- Stores player name (synced across network)
- Tracks seat assignment
- Manages ready state for game start

**Network Variables** (auto-synced):
- `PlayerName` - Display name (owner sets, all see)
- `SeatIndex` - Assigned seat (server sets, all see)
- `IsReady` - Ready to start (owner sets, all see)

**Key Methods**:
- `SetName(string)` - Owner changes their name
- `SetReady(bool)` - Owner toggles ready state
- `GetPlayerName()` - Get current display name

**Usage**: Attach to a prefab and assign as Player Prefab in NetworkManager.

---

### 3. NetworkCardSync.cs
**Purpose**: Synchronizes all game actions across network.

**Responsibilities**:
- Receives player actions (card plays, bids, trump choices)
- Validates actions on server
- Broadcasts validated actions to all clients
- Serializes Card objects for network transmission

**Flow**: Client → ServerRpc → Validation → ClientRpc → All clients execute

**Server RPCs** (clients call these):
- `PlayCardServerRpc(cardData)` - Request to play a card
- `MakeBidServerRpc(bidValue, isPass)` - Request to bid
- `DeclareTrumpServerRpc(suitValue)` - Declare trump suit
- `AnnounceMarriageServerRpc(suitValue)` - Announce marriage

**Events** (GameManager subscribes):
- `OnCardPlayed` - Any player plays a card
- `OnBidMade` - Any player bids
- `OnTrumpDeclared` - Trump is declared
- `OnMarriageAnnounced` - Marriage announced
- `OnTrickResolved` - Trick winner determined
- `OnRoundEnded` - Round completes

**Static Methods**:
- `SerializeCard(Card)` - Convert Card to network string
- `TryParseCard(string, out Card)` - Parse network string to Card

---

## Setup Instructions

### Step 1: Install Netcode for GameObjects
1. Open Package Manager (Window → Package Manager)
2. Click + → "Add package by name"
3. Enter: `com.unity.netcode.gameobjects`
4. Click Add

### Step 2: Create NetworkManager GameObject
1. In your Game scene, create empty GameObject named "NetworkManager"
2. Add `NetworkManager` component
3. Add `UnityTransport` component
4. Configure NetworkManager:
   - Transport: Drag UnityTransport component
   - Connection Approval: ☑ Enabled
   - Max Players: 4

### Step 3: Create Network Coordinator GameObject
1. Create empty GameObject named "NetworkCoordinator"
2. Add these scripts:
   - `NetworkGameManager`
   - `NetworkCardSync`
3. In NetworkGameManager component:
   - Assign reference to your existing GameManager
   - Set Max Players: 4

### Step 4: Create NetworkPlayer Prefab
1. Create new GameObject named "NetworkPlayer"
2. Add `NetworkPlayer` component
3. Save as prefab in Assets/_Project/Prefabs/
4. Delete from scene
5. In NetworkManager component:
   - Drag NetworkPlayer prefab to "Player Prefab" slot
   - Add NetworkPlayer prefab to "Network Prefabs List"

### Step 5: Integrate with GameManager
Add to your GameManager.cs:

```csharp
using CruceChaos.Unity.Network;

// Add fields
private bool _isNetworkGame = false;
private NetworkGameManager _networkManager;
private NetworkCardSync _networkCardSync;

// In Start() method
void Start()
{
    // Check if this is a network game
    _networkManager = NetworkGameManager.Instance;
    _networkCardSync = NetworkCardSync.Instance;
    _isNetworkGame = (_networkManager != null);
    
    if (_isNetworkGame)
    {
        SetupNetworkCallbacks();
    }
    
    // ... rest of your Start() code
}

void SetupNetworkCallbacks()
{
    _networkCardSync.OnCardPlayed += HandleNetworkCardPlayed;
    _networkCardSync.OnBidMade += HandleNetworkBid;
    _networkCardSync.OnTrumpDeclared += HandleNetworkTrump;
    // ... subscribe to other events
}

void HandleNetworkCardPlayed(int seatIndex, Card card)
{
    // Process the card play for this seat
}
```

When a player makes an action, send through network:

```csharp
void OnLocalPlayerPlaysCard(Card card)
{
    if (_isNetworkGame)
    {
        string cardData = NetworkCardSync.SerializeCard(card);
        _networkCardSync.PlayCardServerRpc(cardData);
    }
    else
    {
        // Local game - process immediately
        ProcessCardPlay(card);
    }
}
```

---

## Architecture Overview

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│   Client 1  │         │   Server    │         │   Client 2  │
│             │         │   (Host)    │         │             │
└──────┬──────┘         └──────┬──────┘         └──────┬──────┘
       │                       │                       │
       │  PlayCardServerRpc    │                       │
       ├──────────────────────>│                       │
       │                       │ Validate              │
       │                       │ (check legal move)    │
       │                       │                       │
       │  PlayCardClientRpc    │  PlayCardClientRpc    │
       │<──────────────────────┼──────────────────────>│
       │                       │                       │
       │  Both execute         │                       │
       │  OnCardPlayed event   │                       │
       └───────────────────────┴───────────────────────┘
```

**Key Principle**: Server is authoritative
- Clients request actions via ServerRpc
- Server validates against game rules
- Server broadcasts results via ClientRpc
- All clients execute the same result

---

## Testing

### Single Machine (Recommended)
1. Install ParrelSync from Package Manager
2. Create a clone project
3. Run Host in main editor
4. Run Client in clone editor

### Build Method
1. Build your game
2. Run one instance as Host
3. Run another as Client (IP: 127.0.0.1)

### LAN Testing
1. Host clicks "Host"
2. Find host's IP: `ipconfig` (Windows) or `ip addr` (Linux)
3. Clients enter host's IP and click "Join"

---

## Common Issues

### Scripts don't compile
- Make sure Netcode for GameObjects is installed
- Check that Unity.Netcode namespace is accessible
- Verify assembly definition references (see CruceChaos.Unity.asmdef)

### NetworkManager not found
- Ensure NetworkManager GameObject exists in scene
- Check that it's not disabled

### Players can't connect
- Verify firewall allows port 7777
- Check IP address is correct
- Ensure host started before clients try to join

### Cards don't sync
- Verify NetworkCardSync.Instance is not null
- Check that OnCardPlayed event is subscribed
- Ensure GameManager is calling ServerRpcs correctly

---

## Next Steps

1. **Create Lobby UI** - See implementation guide for NetworkLobbyUI.cs
2. **Add Scene Management** - Load Game scene when host starts
3. **Handle Disconnections** - Replace disconnected player with AI
4. **Add Reconnection** - Save/restore game state for reconnecting players

For detailed implementation, see the full guide document.
