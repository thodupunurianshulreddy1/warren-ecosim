// ============================================================================
//  Carnivore.cs
//  Abstract intermediate class: animals that hunt herbivores.
//  Adds the notion of STAMINA: a chase that lasts too long is abandoned
//  and the predator must rest (coroutine) before hunting again.
//  Concrete children: Fox, Wolf.
//
//  [H4] Reads the world modifiers of the SimulationManager: at night and as
//  days pass, predators see further and run faster (difficulty curve).
// ============================================================================

using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public abstract class Carnivore : Animal
    {
        // --------------------------------------------------------------------
        //  Public API read by herbivores
        // --------------------------------------------------------------------

        /// <summary>A predator that is eating or resting is not considered dangerous.</summary>
        public virtual bool IsThreatening =>
            isAlive && state != AgentState.Eating && state != AgentState.Resting;

        /// <summary>1 = noticed at full sense range. Lower = stealthier (overridden by Fox).</summary>
        public virtual float DetectionRangeMultiplier => 1f;

        // --------------------------------------------------------------------
        //  Overrides of Animal
        // --------------------------------------------------------------------
        protected override AgentState FoodSeekingState => AgentState.Hunting;

        /// <summary>Chooses the prey with the best (lowest) score among the huntable ones.</summary>
        protected override SimEntity FindFood(float range)
        {
            return SimUtils.FindBest<Herbivore>(World, Position, range, ScorePrey, CanHunt);
        }

        /// <summary>[H4] Predators' senses are boosted by the world modifier (night, later days).</summary>
        protected override SimEntity SenseFood()
        {
            return FindFood(stats.senseRadius * SimulationManager.Instance.PredatorSenseMultiplier);
        }

        /// <summary>Default preference: the closest prey. Wolves override to prefer deer.</summary>
        protected virtual float ScorePrey(Herbivore prey)
        {
            return SimUtils.FlatSqrDistance(Position, prey.Position);
        }

        /// <summary>
        /// Which prey this predator is able to hunt. Foxes override (rabbits only).
        /// CanBeEaten is false for a hidden prey (rabbit in a burrow), so burrows work automatically.
        /// </summary>
        protected virtual bool CanHunt(Herbivore prey)
        {
            return prey.CanBeEaten;
        }

        /// <summary>Carnivores sprint after their prey ([H4] faster as days pass).</summary>
        protected override float GetPursuitSpeed(float distanceToTarget)
        {
            return stats.walkSpeed * stats.runSpeedMultiplier * SimulationManager.Instance.PredatorSpeedMultiplier;
        }

        /// <summary>
        /// Adds the stamina rule on top of the base behaviour:
        /// if the chase lasted too long, give up and rest.
        /// </summary>
        protected override void ExecuteCurrentState()
        {
            if (state == AgentState.Hunting && stateTimer > stats.staminaSeconds)
            {
                StartBusyAction(RestRoutine(stats.restDuration));
                return;
            }
            base.ExecuteCurrentState();
        }
    }
}
