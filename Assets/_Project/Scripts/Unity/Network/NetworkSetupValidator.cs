using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Validates that networking is set up correctly.
    /// Attach to any GameObject and check the Console for results.
    /// </summary>
    public class NetworkSetupValidator : MonoBehaviour
    {
        private void Start()
        {
            ValidateSetup();
        }

        [ContextMenu("Validate Network Setup")]
        private void ValidateSetup()
        {
            Debug.Log("=== NETWORK SETUP VALIDATION ===");
            
            bool allGood = true;
            
            // Check 1: NetworkManager
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("❌ NetworkManager not found in scene!");
                allGood = false;
            }
            else
            {
                Debug.Log("✓ NetworkManager found");
                
                // Check transport
                var transport = NetworkManager.Singleton.GetComponent<global::Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("❌ UnityTransport not found on NetworkManager!");
                    allGood = false;
                }
                else
                {
                    Debug.Log("✓ UnityTransport found");
                }
            }
            
            // Check 2: NetworkGameManager
            if (NetworkGameManager.Instance == null)
            {
                Debug.LogError("❌ NetworkGameManager not found in scene!");
                allGood = false;
            }
            else
            {
                Debug.Log("✓ NetworkGameManager found");
            }
            
            // Check 3: NetworkCardSync
            if (NetworkCardSync.Instance == null)
            {
                Debug.LogError("❌ NetworkCardSync not found in scene!");
                allGood = false;
            }
            else
            {
                Debug.Log("✓ NetworkCardSync found");
            }
            
            // Check 4: NetworkTestUI
            var testUI = FindObjectOfType<NetworkTestUI>();
            if (testUI == null)
            {
                Debug.LogWarning("⚠ NetworkTestUI not found - you won't be able to Host/Join from UI");
            }
            else
            {
                Debug.Log("✓ NetworkTestUI found");
            }
            
            if (allGood)
            {
                Debug.Log("=== ✓ ALL CHECKS PASSED - Ready to test! ===");
            }
            else
            {
                Debug.LogError("=== ❌ SETUP INCOMPLETE - Fix errors above ===");
            }
        }
    }
}
