// ============================================================================
//  Deer.cs   (Herbivore -> Animal -> SimEntity)
//  Large herd animal.
//  Overrides:
//   - CreateDefaultStats      : slower breeding, long life, very nutritious
//   - IsDangerous             : deer are too big for foxes, they only fear wolves
//   - ChooseWanderDestination : herd cohesion (wander toward nearby deer)
//   - OnStateChanged          : when a deer starts fleeing it alerts the herd
//
//  [H4] The alarm itself (ReceiveAlarm, alarm timer, trusting the alarm in
//  SenseThreat) moved up to Herbivore so that rabbits can share it.
//  Deer only decide WHEN to raise the alarm and WHO hears it (the herd).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public class Deer : Herbivore
    {
        [Header("Deer specifics")]
        [Tooltip("Radius in which deer consider each other part of the same herd.")]
        [SerializeField] private float herdRadius = 9f;

        private readonly List<Deer> herdBuffer = new List<Deer>();   // reused to avoid allocations

        public override Species Species => Species.Deer;

        protected override AnimalStats CreateDefaultStats()
        {
            return new AnimalStats
            {
                walkSpeed = 2.0f,
                runSpeedMultiplier = 2.4f,
                senseRadius = 9f,
                maxEnergy = 150f,
                energyDrainPerSecond = 2.4f,
                eatDuration = 2.0f,
                meatValue = 90f,
                maturityAge = 20f,
                maxAge = 150f,
                reproductionCooldown = 25f,
                reproductionCost = 0.35f,
                wanderRadius = 5f,
                contactRange = 0.9f
            };
        }

        /// <summary>Foxes are ignored: only wolves are a danger for an adult deer.</summary>
        protected override bool IsDangerous(Carnivore predator)
        {
            return predator is Wolf && base.IsDangerous(predator);
        }

        /// <summary>When this deer starts fleeing, every nearby deer is alerted.</summary>
        protected override void OnStateChanged(AgentState previous, AgentState next)
        {
            base.OnStateChanged(previous, next);
            if (next == AgentState.Fleeing && currentTarget != null)
                AlertNearby(currentTarget, herdRadius, herdBuffer);   // generic helper from Herbivore
        }

        /// <summary>Herd cohesion: wander around the centre of the nearby deer.</summary>
        protected override Vector3 ChooseWanderDestination()
        {
            SimUtils.FindAllInRange(World, Position, herdRadius, herdBuffer);
            if (herdBuffer.Count <= 1) return base.ChooseWanderDestination();   // alone: normal wander

            Vector3 herdCenter = Vector3.zero;
            foreach (Deer member in herdBuffer) herdCenter += member.Position;
            herdCenter /= herdBuffer.Count;

            return SimUtils.RandomPointNear(herdCenter, stats.wanderRadius);
        }
    }
}
