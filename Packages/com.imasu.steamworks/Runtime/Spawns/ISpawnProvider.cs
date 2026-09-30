using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// A physical spawn location for players. Implementations: <see cref="SpawnPoint"/>
    /// (exact point) and <see cref="SpawnArea"/> (random point inside a volume).
    /// The <see cref="SpawnManager"/> on the Session Manager collects these and hands
    /// Mirror a team-filtered, anti-clump position when a player joins.
    ///
    /// teamId semantics: 0 = any team (everyone may spawn here); a positive value =
    /// only that team. A map's spawns ARE the physical limit — a scene with no
    /// SpawnPoint/SpawnArea is flagged as not multiplayer-ready by the Control Center.
    /// </summary>
    public interface ISpawnProvider
    {
        int TeamId { get; }

        /// <summary>World-space spawn position (single points return their own TRS; areas sample inside their volume).</summary>
        Vector3 GetPosition();

        Quaternion GetRotation();

        /// <summary>Visual gizmo label (e.g. "Team 1" or "Any").</summary>
        string Label { get; }
    }
}