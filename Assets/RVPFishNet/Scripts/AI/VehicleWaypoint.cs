using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/AI/Vehicle Waypoint", 1)]
    public class VehicleWaypoint : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public VehicleWaypoint nextPoint;

        [Tooltip("")]
        public float radius = 10;

        [Tooltip("Percentage of a vehicle's max speed to drive at")]
        [Range(0, 1)]
        public float speed = 1;

        private void OnDrawGizmos()
        {
            // Visualize waypoint
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);

            // Draw line to next point
            if (nextPoint)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, nextPoint.transform.position);
            }
        }
    }
}