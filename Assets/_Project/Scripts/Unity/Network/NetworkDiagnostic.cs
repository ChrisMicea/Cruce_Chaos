using Unity.Netcode;
using UnityEngine;

namespace CruceChaos.Unity.Network
{
    /// <summary>
    /// Simple diagnostic script to verify Netcode is working.
    /// If this script shows up in Add Component, Netcode is properly installed.
    /// </summary>
    public class NetworkDiagnostic : NetworkBehaviour
    {
        private void Start()
        {
            Debug.Log("NetworkDiagnostic: Netcode is working!");
        }
    }
}
