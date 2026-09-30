using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>A discovered LAN server (read-only view for the browser UI).</summary>
    [Serializable]
    public class LanServerInfo
    {
        public string serverName;
        public int currentPlayers;
        public int maxPlayers;
        public string mapName;
        public string uri;
        public string host;
    }

    /// <summary>
    /// LAN-discovery surface the CORE can use without depending on the optional
    /// LAN addon (com.imasu.steamworks.lan). The addon's LanDiscovery implements
    /// this and auto-initializes; when the addon isn't installed every query
    /// returns null / empty and the browser simply shows no LAN section.
    /// </summary>
    public interface ILanDiscovery
    {
        void StartDiscovery();
        void StopDiscovery();
        void Clear();
        void AdvertiseServer();
        List<LanServerInfo> Snapshot();
    }

    /// <summary>Locates the LAN addon's implementation at runtime (null when not installed).</summary>
    public static class LanDiscoveryQuery
    {
        public static ILanDiscovery Find()
        {
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         UnityEngine.FindObjectsSortMode.None))
                if (mb is ILanDiscovery lan) return lan;
            return null;
        }
    }
}