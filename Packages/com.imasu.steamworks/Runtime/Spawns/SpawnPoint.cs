using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// A single player spawn point. Register it with the scene's SpawnManager
    /// (auto via OnEnable). teamId 0 = any team may spawn here. Draws a gizmo in
    /// the Scene view so the Control Center can guide adding them visually.
    /// </summary>
    [AddComponentMenu("Imasu Steamworks/Spawn Point")]
    public class SpawnPoint : MonoBehaviour, ISpawnProvider
    {
        [Tooltip("Which team spawns here (0 = any team).")]
        public int teamId;

        public int TeamId => teamId;

        public string Label => teamId == 0 ? "Any" : "Team " + teamId;

        public Vector3 GetPosition() => transform.position;

        public Quaternion GetRotation() => transform.rotation;

        void OnEnable() => SpawnManager.Register(this);

        void OnDisable() => SpawnManager.Unregister(this);

        void OnDrawGizmos()
        {
            Gizmos.color = teamId == 0 ? new Color(0.4f, 1f, 0.45f, 0.9f) : new Color(1f, 0.7f, 0.2f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
        }
    }
}