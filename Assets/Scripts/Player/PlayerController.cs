// ============================================================================
//  PlayerController.cs   [H4]
//  Translates the player's input into COMMANDS for the possessed rabbit.
//  It never moves the rabbit itself: the rabbit applies the commands with
//  its own stats and rules (Animal.SetMoveInput, TryEatNearby...).
//
//  Responsibilities:
//   - Possess / release a rabbit (the GameManager decides which one)
//   - Camera-relative movement + sprint limited by STAMINA
//   - Eat (E), mate (F), abilities (Space, B) through PlayerAbilities
//   - Threat awareness for the HUD: who is chasing me? am I being stalked?
//   - A floating marker above the possessed rabbit
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Movement is relative to this camera (Camera.main if empty).")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerAbilities abilities;
        [Tooltip("Floating arrow above the possessed rabbit (optional).")]
        [SerializeField] private Transform marker;

        [Header("Movement")]
        [Tooltip("The player's rabbit is a little faster than wild rabbits (it is the hero).")]
        [SerializeField] private float baseSpeedMultiplier = 1.15f;

        [Header("Stamina (sprint)")]
        [Tooltip("Seconds of continuous sprint.")]
        [SerializeField] private float maxStamina = 4f;
        [Tooltip("Stamina recovered per second when not sprinting.")]
        [SerializeField] private float staminaRegenPerSecond = 0.8f;
        [Tooltip("After running out of breath, sprint is blocked until stamina is back to this fraction.")]
        [Range(0f, 1f)] [SerializeField] private float exhaustedRecoveryFraction = 0.35f;

        [Header("Interaction reach")]
        [SerializeField] private float eatReach = 1.5f;
        [SerializeField] private float mateReach = 2.2f;

        [Header("Marker")]
        [SerializeField] private float markerHeight = 1.5f;
        [SerializeField] private float markerBobAmplitude = 0.12f;
        [SerializeField] private float markerSpinSpeed = 90f;

        private Rabbit possessed;
        private float stamina;
        private bool isExhausted;
        private bool inputEnabled = true;
        private Carnivore activeChaser;
        private bool isBeingStalked;

        // --------------------------------------------------------------------
        //  Public read-only API (HUD, GameManager)
        // --------------------------------------------------------------------
        public Rabbit Possessed => possessed;
        public bool HasRabbit => possessed != null && possessed.IsAlive;
        public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
        public bool IsExhausted => isExhausted;
        public bool IsSprinting { get; private set; }
        public Carnivore ActiveChaser => activeChaser;
        public bool IsBeingStalked => isBeingStalked;
        public PlayerAbilities Abilities => abilities;

        /// <summary>Short messages for the player (HUD toasts), e.g. "Out of breath!".</summary>
        public event Action<string> Feedback;

        // ====================================================================
        //  Possession
        // ====================================================================
        private void Awake()
        {
            stamina = maxStamina;
            if (marker != null) marker.gameObject.SetActive(false);
        }

        public void Possess(Rabbit rabbit, float lifespanMultiplier)
        {
            Release();
            if (rabbit == null) return;

            possessed = rabbit;
            possessed.SetPlayerControlled(true, lifespanMultiplier);
            stamina = maxStamina;
            isExhausted = false;
        }

        public void Release()
        {
            if (possessed != null && possessed.IsAlive) possessed.SetPlayerControlled(false);
            possessed = null;
            activeChaser = null;
            isBeingStalked = false;
            IsSprinting = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled && HasRabbit) possessed.SetMoveInput(Vector3.zero, false, 1f);
        }

        // ====================================================================
        //  Loop
        // ====================================================================
        private void Update()
        {
            if (!HasRabbit)
            {
                IsSprinting = false;
                return;
            }

            UpdateThreatAwareness();
            if (!inputEnabled) return;

            HandleMovement(Time.deltaTime);
            HandleActions();
        }

        private void LateUpdate()
        {
            UpdateMarker();
        }

        private void HandleMovement(float dt)
        {
            Vector3 direction = ToWorldDirection(InputHelper.GetMoveAxis());
            bool wantsToMove = direction.sqrMagnitude > 0.01f;

            // Moving while eating drops the meal: a deliberate risk / reward choice.
            if (wantsToMove && possessed.IsBusy && possessed.State == AgentState.Eating
                && possessed.CancelBusyAction())
            {
                RaiseFeedback("You dropped your meal to run!");
            }

            bool wantsToSprint = wantsToMove && InputHelper.GetKey(GameKey.Sprint);
            UpdateStamina(wantsToSprint, dt);

            float speedMultiplier = baseSpeedMultiplier * (1f + (abilities != null ? abilities.SpeedBonus : 0f));
            possessed.SetMoveInput(direction, IsSprinting, speedMultiplier);
        }

        private void UpdateStamina(bool wantsToSprint, float dt)
        {
            IsSprinting = wantsToSprint && !isExhausted && stamina > 0f;

            if (IsSprinting)
            {
                stamina -= dt;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    isExhausted = true;
                    IsSprinting = false;
                    RaiseFeedback("Out of breath!");
                }
            }
            else
            {
                stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSecond * dt);
                if (isExhausted && stamina >= maxStamina * exhaustedRecoveryFraction) isExhausted = false;
            }
        }

        private void HandleActions()
        {
            if (InputHelper.GetKeyDown(GameKey.Eat)) TryEat();
            if (InputHelper.GetKeyDown(GameKey.Mate)) TryMate();
            if (InputHelper.GetKeyDown(GameKey.Thump)) UseAbility(AbilityType.Thump);
            if (InputHelper.GetKeyDown(GameKey.Dig)) UseAbility(AbilityType.DigBurrow);
        }

        private void TryEat()
        {
            if (possessed.IsBusy) return;
            if (!possessed.TryEatNearby(eatReach))
                RaiseFeedback("No grown grass within reach.");
        }

        private void TryMate()
        {
            if (possessed.IsBusy) return;
            if (possessed.TryMateNearby(mateReach)) return;

            string blocker = possessed.GetMatingBlocker();
            RaiseFeedback(blocker != null
                ? $"Can't mate: {blocker}."
                : "No rabbit ready to mate within reach (it must be an adult with enough energy).");
        }

        private void UseAbility(AbilityType ability)
        {
            if (abilities == null || possessed.IsBusy) return;
            abilities.TryUse(ability, possessed, out string message);
            if (!string.IsNullOrEmpty(message)) RaiseFeedback(message);
        }

        // ====================================================================
        //  Helpers
        // ====================================================================

        /// <summary>WASD relative to where the camera looks, flattened on the ground.</summary>
        private Vector3 ToWorldDirection(Vector2 axis)
        {
            Transform cam = cameraTransform;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return new Vector3(axis.x, 0f, axis.y);

            Vector3 forward = cam.forward.Flattened().normalized;
            Vector3 right = cam.right.Flattened().normalized;
            Vector3 direction = right * axis.x + forward * axis.y;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        /// <summary>
        /// Finds the closest predator hunting the player's rabbit.
        /// A STALKING fox is not reported as a chaser (its stealth must stay meaningful),
        /// only as a vague "you feel watched" warning.
        /// </summary>
        private void UpdateThreatAwareness()
        {
            activeChaser = null;
            isBeingStalked = false;
            float closestSqr = float.MaxValue;

            IReadOnlyList<SimEntity> world = SimulationManager.Instance.Entities;
            for (int i = 0; i < world.Count; i++)
            {
                if (!(world[i] is Carnivore predator) || !predator.IsAlive) continue;
                if (predator.State != AgentState.Hunting || predator.CurrentTarget != possessed) continue;

                if (predator is Fox fox && fox.IsStalking)
                {
                    isBeingStalked = true;
                    continue;
                }

                float sqr = SimUtils.FlatSqrDistance(predator.Position, possessed.Position);
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    activeChaser = predator;
                }
            }
        }

        private void UpdateMarker()
        {
            if (marker == null) return;

            bool show = HasRabbit;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;

            float bob = Mathf.Sin(Time.unscaledTime * 3f) * markerBobAmplitude;
            float height = markerHeight * possessed.transform.localScale.y + bob;
            marker.position = possessed.Position + Vector3.up * height;
            marker.Rotate(0f, markerSpinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }

        private void RaiseFeedback(string message)
        {
            Feedback?.Invoke(message);
        }
    }
}
