// ============================================================================
//  Wolf.cs   (Carnivore -> Animal -> SimEntity)
//  Pack hunter.
//  Overrides:
//   - CreateDefaultStats : strong predator, long stamina
//   - ScorePrey          : prefers deer (bigger meal) over rabbits
//   - OnStateChanged     : when a wolf starts hunting it calls its pack
//   - DecideNextAction   : a wolf that joined a pack hunt sticks to it
//   - GetLitterSize      : 1 or 2 cubs
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public class Wolf : Carnivore
    {
        [Header("Wolf specifics")]
        [Tooltip("Radius in which other wolves hear the hunting call.")]
        [SerializeField] private float packCallRadius = 12f;
        [Tooltip("Deer score is multiplied by this (lower = stronger preference for deer).")]
        [Range(0.1f, 1f)] [SerializeField] private float deerPreference = 0.4f;

        private bool isPackHunting;          // true when hunting because another wolf called
        private readonly List<Wolf> packBuffer = new List<Wolf>();

        public override Species Species => Species.Wolf;

        protected override AnimalStats CreateDefaultStats()
        {
            return new AnimalStats
            {
                walkSpeed = 2.2f,
                runSpeedMultiplier = 2.7f,
                senseRadius = 11f,
                maxEnergy = 160f,
                energyDrainPerSecond = 2.0f,
                hungerThreshold = 0.5f,
                eatDuration = 3f,
                staminaSeconds = 10f,
                restDuration = 4f,
                maturityAge = 25f,
                maxAge = 160f,
                reproductionCooldown = 40f,
                reproductionCost = 0.3f,
                contactRange = 0.9f
            };
        }

        /// <summary>Distance-based score, strongly reduced for deer.</summary>
        protected override float ScorePrey(Herbivore prey)
        {
            float score = base.ScorePrey(prey);
            return prey is Deer ? score * deerPreference : score;
        }

        protected override int GetLitterSize()
        {
            return Random.Range(1, 3);   // 1 or 2 cubs
        }

        /// <summary>A pack member keeps chasing the shared target instead of re-deciding.</summary>
        protected override void DecideNextAction()
        {
            bool sharedTargetValid = isPackHunting && state == AgentState.Hunting
                                     && currentTarget != null && currentTarget.IsAlive;
            if (sharedTargetValid) return;

            isPackHunting = false;
            base.DecideNextAction();
        }

        protected override void OnStateChanged(AgentState previous, AgentState next)
        {
            base.OnStateChanged(previous, next);

            // Only the wolf that STARTED the hunt calls the pack (prevents infinite chains).
            if (next == AgentState.Hunting && !isPackHunting && currentTarget != null)
                CallPack(currentTarget);

            if (next != AgentState.Hunting) isPackHunting = false;
        }

        private void CallPack(SimEntity prey)
        {
            SimUtils.FindAllInRange(World, Position, packCallRadius, packBuffer);
            foreach (Wolf packMate in packBuffer)
            {
                if (packMate != this) packMate.JoinHunt(prey);
            }
        }

        /// <summary>Called by a pack mate. Busy or already-hunting wolves ignore the call.</summary>
        public void JoinHunt(SimEntity prey)
        {
            if (!isAlive || isBusy || state == AgentState.Hunting) return;

            isPackHunting = true;                    // set BEFORE ChangeState (see OnStateChanged)
            ChangeState(AgentState.Hunting, prey);
        }
    }
}
