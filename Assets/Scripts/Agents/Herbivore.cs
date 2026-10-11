// ============================================================================
//  Herbivore.cs
//  Abstract intermediate class: animals that eat plants and are eaten
//  by carnivores. Implements IEdible so predators can consume them.
//  Concrete children: Rabbit, Deer.
//
//  [H4] Refactoring:
//   - The ALARM mechanic (fleeing because a neighbour warned you) used to
//     live in Deer only. It now lives here so that rabbits can use it too
//     (the player's "Thump" ability re-uses the deer herd alarm).
//   - IsHidden: a hidden herbivore (rabbit in a burrow) cannot be eaten,
//     so predators lose interest in it automatically.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public abstract class Herbivore : Animal, IEdible
    {
        [Header("Alarm (shared by herds and warrens)")]
        [Tooltip("How long an alarmed herbivore keeps fleeing from a threat it did not see itself.")]
        [SerializeField] protected float alarmDuration = 2.5f;

        private SimEntity alarmThreat;     // threat reported by a neighbour
        private float alarmTimer;

        // --------------------------------------------------------------------
        //  Overrides of Animal
        // --------------------------------------------------------------------
        protected override AgentState FoodSeekingState => AgentState.SeekingFood;

        protected override void Update()
        {
            if (alarmTimer > 0f) alarmTimer -= Time.deltaTime;
            base.Update();
        }

        /// <summary>Closest plant within range that is grown enough to be worth eating.</summary>
        protected override SimEntity FindFood(float range)
        {
            return SimUtils.FindClosest<Plant>(World, Position, range, plant => plant.CanBeEaten);
        }

        /// <summary>
        /// Own senses first (closest dangerous carnivore); if nothing is seen,
        /// trust a recent alarm from a neighbour.
        /// </summary>
        protected override SimEntity SenseThreat()
        {
            SimEntity seenThreat = SimUtils.FindClosest<Carnivore>(World, Position, stats.senseRadius, IsDangerous);
            if (seenThreat != null) return seenThreat;

            bool alarmStillValid = alarmTimer > 0f && alarmThreat != null && alarmThreat.IsAlive;
            return alarmStillValid ? alarmThreat : null;
        }

        /// <summary>
        /// Virtual so that species can ignore some predators (deer ignore foxes).
        /// Each predator can reduce how far away it is noticed (a stalking fox is harder to spot).
        /// </summary>
        protected virtual bool IsDangerous(Carnivore predator)
        {
            if (!predator.IsThreatening) return false;
            float detectionRange = stats.senseRadius * predator.DetectionRangeMultiplier;
            return SimUtils.IsWithinRange(Position, predator.Position, detectionRange);
        }

        /// <summary>
        /// A herbivore drops its meal (or its partner) when a predator appears.
        /// [H4] Not when the player is in control: the player decides when to drop a meal.
        /// </summary>
        protected override bool ShouldInterruptBusyAction()
        {
            if (isPlayerControlled) return false;
            bool interruptibleAction = state == AgentState.Eating || state == AgentState.Mating;
            return interruptibleAction && SenseThreat() != null;
        }

        // --------------------------------------------------------------------
        //  [H4] Alarm (moved up from Deer)
        // --------------------------------------------------------------------

        /// <summary>Called by a neighbour (another deer, or the player's thump).</summary>
        public virtual void ReceiveAlarm(SimEntity threat)
        {
            // Already running, dead, or controlled by the player: ignore (also stops infinite chains).
            if (!isAlive || isPlayerControlled || threat == null || state == AgentState.Fleeing) return;

            alarmThreat = threat;
            alarmTimer = alarmDuration;
            if (isBusy) InterruptBusyAction();                    // drop food immediately
            ChangeState(AgentState.Fleeing, threat);               // may in turn alert its neighbours
        }

        /// <summary>Generic helper: alarms every herbivore of type T in the radius (except itself).</summary>
        protected int AlertNearby<T>(SimEntity threat, float radius, List<T> buffer) where T : Herbivore
        {
            SimUtils.FindAllInRange(World, Position, radius, buffer);
            int alerted = 0;
            foreach (T member in buffer)
            {
                if (member == this) continue;
                member.ReceiveAlarm(threat);
                alerted++;
            }
            return alerted;
        }

        // --------------------------------------------------------------------
        //  IEdible implementation
        // --------------------------------------------------------------------

        /// <summary>[H4] True while protected (e.g. a rabbit inside a burrow).</summary>
        public virtual bool IsHidden => false;

        public bool CanBeEaten => isAlive && !IsHidden;

        public float GetEaten(SimEntity eater)
        {
            // A well-fed prey is more nutritious.
            float nutrition = stats.meatValue + energy * 0.5f;
            Die("eaten by " + eater.Species);
            return nutrition;
        }
    }
}
