using Mirror;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Server-authoritative moving object (R3): proves networked transform sync.
    /// The AI "drives" the cube but the movement logic only runs on the server
    /// (host). Clients receive the position via NetworkTransformReliable, exactly
    /// how a server-side AI enemy would move for everyone.
    ///
    /// Requires a NetworkTransformReliable component on the same object.
    /// </summary>
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class AiCube : NetworkBehaviour
    {
        [Tooltip("Speed the cube patrols with (m/s).")]
        public float speed = 3f;

        [Tooltip("Patrol radius around the start position.")]
        public float radius = 4f;

        Vector3 startPosition;
        Vector3 target;

        void Awake()
        {
            startPosition = transform.position;
            target = NextTarget();
        }

        void Update()
        {
            // Movement is server-authoritative only.
            if (!isServer) return;

            Vector3 to = target - transform.position;
            to.y = 0;
            if (to.magnitude < 0.1f)
            {
                target = NextTarget();
                return;
            }

            transform.position += to.normalized * (speed * Time.deltaTime);
        }

        Vector3 NextTarget()
        {
            Vector2 rand = Random.insideUnitCircle * radius;
            return startPosition + new Vector3(rand.x, 0, rand.y);
        }
    }
}