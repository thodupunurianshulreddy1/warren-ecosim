// ============================================================================
//  Fox.cs   (Carnivore -> Animal -> SimEntity)
//  Solitary, stealthy hunter of rabbits.
//  Overrides:
//   - CreateDefaultStats       : light predator, short stamina
//   - CanHunt                  : rabbits only (deer are too big)
//   - GetPursuitSpeed          : STALK slowly, then POUNCE when close
//   - DetectionRangeMultiplier : a stalking fox is noticed later by rabbits
// ============================================================================

using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public class Fox : Carnivore
    {
        [Header("Fox specifics")]
        [Tooltip("Distance to the prey under which the fox stops stalking and pounces.")]
        [SerializeField] private float pounceDistance = 3f;
        [Tooltip("Speed multiplier (on walk speed) while stalking.")]
        [SerializeField] private float stalkSpeedFactor = 0.7f;
        [Tooltip("Speed multiplier (on run speed) while pouncing.")]
        [SerializeField] private float pounceSpeedFactor = 1.25f;
        [Tooltip("Detection multiplier while stalking (0.5 = seen at half the usual distance).")]
        [Range(0.1f, 1f)] [SerializeField] private float stealthFactor = 0.5f;

        private bool isStalking;

        public override Species Species => Species.Fox;

        protected override AnimalStats CreateDefaultStats()
        {
            return new AnimalStats
            {
                walkSpeed = 2.0f,
                runSpeedMultiplier = 2.6f,
                senseRadius = 10f,
                maxEnergy = 110f,
                energyDrainPerSecond = 1.6f,
                hungerThreshold = 0.55f,
                eatDuration = 2.5f,
                staminaSeconds = 6f,
                restDuration = 3f,
                maturityAge = 20f,
                maxAge = 140f,
                reproductionCooldown = 30f,
                reproductionCost = 0.3f
            };
        }

        protected override bool CanHunt(Herbivore prey)
        {
            return prey is Rabbit && base.CanHunt(prey);
        }

        /// <summary>Two-phase hunt: sneak in, then sprint.</summary>
        protected override float GetPursuitSpeed(float distanceToTarget)
        {
            isStalking = distanceToTarget > pounceDistance;
            return isStalking
                ? stats.walkSpeed * stalkSpeedFactor
                : base.GetPursuitSpeed(distanceToTarget) * pounceSpeedFactor;
        }

        /// <summary>[H4] Read by the player HUD: a stalking fox is only "felt", not clearly announced.</summary>
        public bool IsStalking => state == AgentState.Hunting && isStalking;

        public override float DetectionRangeMultiplier => IsStalking ? stealthFactor : 1f;

        protected override void OnStateChanged(AgentState previous, AgentState next)
        {
            base.OnStateChanged(previous, next);
            if (next != AgentState.Hunting) isStalking = false;
        }
    }
}
