// ============================================================================
//  FollowCamera.cs   [H4]
//  Third-person orbit camera that follows the possessed rabbit.
//    Right mouse drag or Z / C : orbit       Mouse wheel : zoom
//  Without a target (title screen, end screen) it slowly orbits the whole
//  arena from far away: the simulation becomes the menu background.
//  Uses unscaledDeltaTime so it keeps working while the game is paused.
// ============================================================================

using UnityEngine;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Player
{
    public class FollowCamera : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [Tooltip("Higher = the camera catches up with the target faster.")]
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private float distance = 13f;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] [Range(15f, 85f)] private float pitch = 50f;
        [SerializeField] private float yaw = 0f;

        [Header("Controls")]
        [SerializeField] private float keyOrbitSpeed = 100f;      // degrees / second
        [SerializeField] private float mouseOrbitSpeed = 4f;      // degrees / mouse unit
        [SerializeField] private float zoomSpeed = 2f;

        [Header("Overview (no target)")]
        [SerializeField] private Vector3 overviewFocus = Vector3.zero;
        [SerializeField] private float overviewDistance = 48f;
        [SerializeField] private float overviewOrbitSpeed = 5f;   // degrees / second

        private Vector3 focus;
        private float currentDistance;
        private bool isOverview = true;

        public Transform Target => target;

        private void Awake()
        {
            focus = overviewFocus;
            currentDistance = overviewDistance;
            isOverview = target == null;
        }

        /// <summary>Follow this transform. If it is destroyed later, the camera stays where it was.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            isOverview = newTarget == null;
        }

        /// <summary>Back to the slow orbit around the whole arena.</summary>
        public void ShowOverview()
        {
            target = null;
            isOverview = true;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // --- Orbit & zoom (player input) ---
            float keyOrbit = (InputHelper.GetKey(GameKey.OrbitRight) ? 1f : 0f)
                           - (InputHelper.GetKey(GameKey.OrbitLeft) ? 1f : 0f);
            yaw += keyOrbit * keyOrbitSpeed * dt + InputHelper.GetMouseOrbitDelta() * mouseOrbitSpeed;
            distance = Mathf.Clamp(distance - InputHelper.GetZoomDelta() * zoomSpeed, minDistance, maxDistance);

            // --- Where to look ---
            // 'target != null' uses Unity's null check: a destroyed rabbit counts as null,
            // and the camera simply holds its last focus until a new target is given.
            if (target != null) focus = Vector3.Lerp(focus, target.position, 1f - Mathf.Exp(-followSharpness * dt));
            else if (isOverview)
            {
                focus = Vector3.Lerp(focus, overviewFocus, 1f - Mathf.Exp(-followSharpness * 0.3f * dt));
                yaw += overviewOrbitSpeed * dt;
            }

            float wantedDistance = isOverview ? overviewDistance : distance;
            currentDistance = Mathf.Lerp(currentDistance, wantedDistance, 1f - Mathf.Exp(-3f * dt));

            // --- Place the camera behind the focus point ---
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus - rotation * Vector3.forward * currentDistance;
            transform.rotation = rotation;
        }
    }
}
