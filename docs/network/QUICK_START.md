# Quick Start Guide - 15 Minutes to Multiplayer

Follow these steps to get LAN multiplayer working in your Cruce game.

## ☐ Step 1: Install Netcode (2 minutes)

1. Open Unity
2. Go to **Window → Package Manager**
3. Click **+** button (top-left)
4. Select **Add package by name...**
5. Enter: `com.unity.netcode.gameobjects`
6. Click **Add**
7. Wait for installation to complete

✅ You should see "Netcode for GameObjects" in the package list.

---

## ☐ Step 2: Setup Scene (3 minutes)

### Create NetworkManager
1. In your **Game** scene (where GameManager is)
2. Right-click in Hierarchy → **Create Empty**
3. Name it: `NetworkManager`
4. With NetworkManager selected, click **Add Component**
5. Search for and add: **NetworkManager**
6. Click **Add Component** again
7. Search for and add: **UnityTransport**

### Configure NetworkManager Component
In the NetworkManager component:
1. **Transport**: Drag the UnityTransport component into this field
2. **Connection Approval**: ☑ Check this box
3. Click on **Network Prefabs** to expand it
4. Set **Size** to 1 (for now)

---

## ☐ Step 3: Create Network Coordinator (2 minutes)

1. Right-click in Hierarchy → **Create Empty**
2. Name it: `NetworkCoordinator`
3. Click **Add Component** → Search: `NetworkGameManager`
4. Click **Add Component** → Search: `NetworkCardSync`
5. In **NetworkGameManager** component:
   - **Game Manager**: Drag your existing GameManager from the scene
   - **Max Players**: Set to `4`

---

## ☐ Step 4: Create NetworkPlayer Prefab (3 minutes)

1. Right-click in Hierarchy → **Create Empty**
2. Name it: `NetworkPlayer`
3. Click **Add Component** → Search: `NetworkPlayer`
4. Drag **NetworkPlayer** from Hierarchy into your Prefabs folder
   - (Create a Prefabs folder if you don't have one: `Assets/_Project/Prefabs/`)
5. **Delete** the NetworkPlayer from the Hierarchy
6. Select **NetworkManager** in Hierarchy
7. In NetworkManager component:
   - **Player Prefab**: Drag the NetworkPlayer prefab here
   - **Network Prefabs → Element 0**: Also drag NetworkPlayer prefab here

---

## ☐ Step 5: Add Basic Testing UI (3 minutes)

We'll add simple buttons to test hosting and joining.

1. In your **Game** scene, right-click Hierarchy → **UI → Canvas** (if you don't have one)
2. Right-click Canvas → **UI → Panel**
3. Name it: `NetworkTestPanel`
4. Right-click NetworkTestPanel → **UI → Button**
5. Name it: `HostButton`
6. Change button text to: **Host**
7. Duplicate the button (Ctrl+D)
8. Name it: `ClientButton`
9. Change text to: **Join**
10. Position them side-by-side at the top of the screen

### Create Test Script
1. Right-click in Scripts/Unity folder → **Create → C# Script**
2. Name it: `NetworkTestUI`
3. Paste this code:

```csharp
using Unity.Netcode;
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
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager.Singleton.StartClient();
            Debug.Log("Started as Client");
            gameObject.SetActive(false); // Hide buttons
        }
    }
}
```

4. Save the script
5. Select **NetworkTestPanel** in Hierarchy
6. Click **Add Component** → Search: `NetworkTestUI`
7. Drag **HostButton** into the **Host Button** field
8. Drag **ClientButton** into the **Client Button** field

---

## ☐ Step 6: Test It! (2 minutes)

### Option A: ParrelSync (Recommended)
1. Install ParrelSync from Package Manager
2. **ParrelSync → Create Clone**
3. In **Main Project**: Press Play → Click **Host**
4. In **Clone Project**: Press Play → Click **Join**

### Option B: Build Method
1. **File → Build Settings → Build**
2. Run the built game → Click **Host**
3. In Unity Editor: Press Play → Click **Join**

### What You Should See
- Console logs: "Started as Host" and "Started as Client"
- NetworkManager shows "Connected Clients: 2" in inspector
- No errors in the console

✅ **Success!** You now have basic networking working.

---

## 🎯 Next Steps

Now integrate with your GameManager:

### In GameManager.cs, add to Start():

```csharp
using CruceChaos.Unity.Network;

void Start()
{
    // ... your existing code ...
    
    // Check if network mode
    var networkManager = NetworkGameManager.Instance;
    var networkCardSync = NetworkCardSync.Instance;
    bool isNetworkGame = (networkManager != null);
    
    if (isNetworkGame)
    {
        Debug.Log("NETWORK MODE");
        SetupNetworkCallbacks();
    }
    else
    {
        Debug.Log("LOCAL MODE");
    }
}

void SetupNetworkCallbacks()
{
    var sync = NetworkCardSync.Instance;
    
    sync.OnCardPlayed += (seat, card) => {
        Debug.Log($"Network: Seat {seat} played {card}");
        // TODO: Apply card play to game state
    };
    
    sync.OnBidMade += (seat, bid) => {
        Debug.Log($"Network: Seat {seat} bid {bid}");
        // TODO: Apply bid to game state
    };
    
    sync.OnTrumpDeclared += (trump) => {
        Debug.Log($"Network: Trump declared: {trump}");
        // TODO: Set trump in game state
    };
}
```

### When a player makes an action:

```csharp
void OnPlayerWantsToPlayCard(Card card)
{
    if (NetworkCardSync.Instance != null)
    {
        // Network mode: send to server
        NetworkCardSync.Instance.PlayCardServerRpc(
            card.ToNetworkString());
    }
    else
    {
        // Local mode: process directly
        ProcessCardPlay(card);
    }
}
```

---

## 🐛 Common Issues

**"NetworkBehaviour not found"**
→ Restart Unity after installing Netcode package

**"Instance is null"**
→ Make sure NetworkCoordinator is in the scene and active

**Can't connect on LAN**
→ In StartClient(), change IP from "127.0.0.1" to host's LAN IP
→ Find with: `ipconfig` (Windows) or `ip addr` (Linux)

**Buttons don't work**
→ Check EventSystem exists in scene (auto-created with Canvas)

---

## 📋 Checklist Summary

- [ ] Netcode package installed
- [ ] NetworkManager GameObject created and configured
- [ ] NetworkCoordinator GameObject created with scripts
- [ ] NetworkPlayer prefab created and assigned
- [ ] Test UI buttons created
- [ ] NetworkTestUI script attached and wired
- [ ] Tested Host/Join connection
- [ ] GameManager integration started

**Time**: ~15 minutes  
**Result**: Working LAN multiplayer foundation

Ready to showcase to your lab assistant! 🎉
