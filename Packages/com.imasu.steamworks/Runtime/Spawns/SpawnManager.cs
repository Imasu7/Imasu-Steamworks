using System.Collections.Generic;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Collects every SpawnPoint/SpawnArea in the scene and hands Mirror a
    /// team-filtered, anti-clump spawn position when a player joins.
    ///
    /// Lives on the Session Manager (survives via DontDestroyOnLoad, so it
    /// re-scans on each scene load). The spawn providers register themselves on
    /// OnEnable; SpawnManager.Clear() is called on scene change to drop stale ones.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager instance { get; private set; }

        [Tooltip("Optional team config: anti-clump separation + team names/colors.")]
        public ImasuTeamConfig teamConfig;

        readonly List<ISpawnProvider> providers = new List<ISpawnProvider>();
        readonly List<Vector3> recentSpawns = new List<Vector3>();
        int nextIndex;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Register(ISpawnProvider provider)
        {
            if (instance == null || provider == null) return;
            if (!instance.providers.Contains(provider))
                instance.providers.Add(provider);
        }

        public static void Unregister(ISpawnProvider provider)
        {
            if (instance == null) return;
            instance.providers.Remove(provider);
        }

        public static int ProviderCount => instance != null ? instance.providers.Count : 0;

        /// <summary>
        /// Picks a spawn for a team (0 = any team), round-robin across the team's
        /// spawns, re-rolling up to 8 times to avoid clumping players. Falls back
        /// to the first provider (then transform zero) if none match.
        /// </summary>
        public static bool TryGetSpawn(int teamId, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (instance == null) return false;

            List<ISpawnProvider> candidates = new List<ISpawnProvider>();
            foreach (ISpawnProvider p in instance.providers)
                if (p.TeamId == 0 || p.TeamId == teamId)
                    candidates.Add(p);
            if (candidates.Count == 0) return false;

            float minSep = instance.teamConfig != null ? instance.teamConfig.minSeparation : 0f;
            int start = instance.nextIndex % candidates.Count;
            for (int attempt = 0; attempt < candidates.Count; attempt++)
            {
                ISpawnProvider pick = candidates[(start + attempt) % candidates.Count];
                Vector3 pos = pick.GetPosition();
                if (minSep <= 0f || !instance.Clumps(pos, minSep))
                {
                    instance.nextIndex = (start + attempt + 1) % candidates.Count;
                    instance.recentSpawns.Add(pos);
                    if (instance.recentSpawns.Count > 32) instance.recentSpawns.RemoveAt(0);
                    position = pos;
                    rotation = pick.GetRotation();
                    return true;
                }
            }

            // Everything clumps; fall back to round-robin anyway.
            ISpawnProvider fallback = candidates[start % candidates.Count];
            position = fallback.GetPosition();
            rotation = fallback.GetRotation();
            instance.nextIndex = (start + 1) % candidates.Count;
            return true;
        }

        bool Clumps(Vector3 pos, float minSep)
        {
            foreach (Vector3 recent in recentSpawns)
                if (Vector3.Distance(pos, recent) < minSep)
                    return true;
            return false;
        }
    }
}