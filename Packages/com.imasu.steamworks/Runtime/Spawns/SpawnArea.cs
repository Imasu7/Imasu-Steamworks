using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// A spawn AREA: players spawn at a random point inside the box volume (so a
    /// team fills a region instead of one exact spot). Uses a BoxCollider if one
    /// is present, otherwise a default 4x4x2 volume you can resize in the Scene
    /// view. Anti-clump is applied by SpawnManager against other live players.
    /// </summary>
    [AddComponentMenu("Imasu Steamworks/Spawn Area")]
    [RequireComponent(typeof(BoxCollider))]
    public class SpawnArea : MonoBehaviour, ISpawnProvider
    {
        [Tooltip("Which team spawns here (0 = any team).")]
        public int teamId;

        public int TeamId => teamId;

        public string Label => (teamId == 0 ? "Any" : "Team " + teamId) + " area";

        public Vector3 GetPosition()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            Vector3 half = box.size * 0.5f;
            Vector3 local = new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z));
            return box.transform.TransformPoint(box.center + local);
        }

        public Quaternion GetRotation() => transform.rotation;

        void OnEnable() => SpawnManager.Register(this);

        void OnDisable() => SpawnManager.Unregister(this);

        void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            Gizmos.color = teamId == 0 ? new Color(0.4f, 1f, 0.45f, 0.25f) : new Color(1f, 0.7f, 0.2f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}