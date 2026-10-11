// ============================================================================
//  CameraController.cs
//  Simple orbit camera to observe the ecosystem.
//    WASD / arrows : move the focus point
//    Q / E         : orbit around the focus point
//    Mouse wheel   : zoom
//  Uses unscaledDeltaTime so the camera still moves when the simulation is paused.
// ============================================================================

using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.UI
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 focusPoint = Vector3.zero;
        [SerializeField] private float distance = 45f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField] [Range(10f, 89f)] private float pitch = 55f;   // vertical angle
        [SerializeField] private float yaw = 0f;                         // horizontal angle
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float orbitSpeed = 90f;
        [SerializeField] private float zoomSpeed = 4f;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // --- Orbit & zoom ---
            yaw += InputHelper.GetRotateAxis() * orbitSpeed * dt;
            distance = Mathf.Clamp(distance - InputHelper.GetZoomDelta() * zoomSpeed, minDistance, maxDistance);

            // --- Pan relative to where the camera looks (flattened on the ground) ---
            Vector2 move = InputHelper.GetMoveAxis();
            Vector3 forward = transform.forward.Flattened().normalized;
            Vector3 right = transform.right.Flattened().normalized;
            focusPoint += (right * move.x + forward * move.y) * (panSpeed * dt);

            if (SimulationManager.Instance != null)
                focusPoint = SimUtils.ClampToArena(focusPoint, SimulationManager.Instance.ArenaHalfSize);

            // --- Place the camera: rotation from angles, position behind the focus point ---
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focusPoint - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }
    }
}
