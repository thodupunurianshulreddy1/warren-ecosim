// ============================================================================
//  Plant.cs
//  Static (non-moving) agent at the bottom of the food chain.
//  - Grows over time through a COROUTINE (GrowRoutine)
//  - Can be eaten by herbivores (implements IEdible)
//  - When fully grown, may randomly spread a seed nearby
// ============================================================================

using System.Collections;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public class Plant : SimEntity, IEdible
    {
        // --------------------------------------------------------------------
        //  Tuning
        // --------------------------------------------------------------------
        [Header("Growth")]
        [Tooltip("Seconds between two growth steps.")]
        [SerializeField] private float growthTickInterval = 1.0f;
        [Tooltip("Growth added at every tick (growth goes from 0 to 1).")]
        [SerializeField] private float growthPerTick = 0.08f;
        [Tooltip("Plants smaller than this are ignored by herbivores.")]
        [SerializeField] private float minEdibleGrowth = 0.35f;
        [Tooltip("Visual scale when growth = 0 (relative to the prefab scale).")]
        [SerializeField] private float minVisualScale = 0.2f;

        [Header("Food")]
        [Tooltip("Energy given to a herbivore eating a fully grown plant.")]
        [SerializeField] private float nutritionValue = 35f;
        [Tooltip("Chance that the plant is completely uprooted when eaten (otherwise it regrows).")]
        [Range(0f, 1f)] [SerializeField] private float uprootChance = 0.3f;

        [Header("Reproduction")]
        [Tooltip("Chance per growth tick, once fully grown, to spread a seed.")]
        [Range(0f, 1f)] [SerializeField] private float seedChancePerTick = 0.04f;
        [Tooltip("Max distance at which a seed lands.")]
        [SerializeField] private float seedSpreadRadius = 4f;

        // --------------------------------------------------------------------
        //  State
        // --------------------------------------------------------------------
        private float growth;   // 0 = just sprouted, 1 = fully grown

        public override Species Species => Species.Grass;
        public float Growth => growth;

        // IEdible implementation ------------------------------------------------
        public bool CanBeEaten => isAlive && growth >= minEdibleGrowth;

        public float GetEaten(SimEntity eater)
        {
            float energyGiven = nutritionValue * growth;   // bigger plant = more food

            // Random outcome: either uprooted (dies) or cut down (regrows from 0).
            if (SimUtils.RollChance(uprootChance))
            {
                Die("eaten by " + eater.Species);
            }
            else
            {
                growth = 0f;
                UpdateVisualScale();
            }
            return energyGiven;
        }

        // --------------------------------------------------------------------
        //  Lifecycle
        // --------------------------------------------------------------------
        protected override void Awake()
        {
            base.Awake();                                   // keep parent behaviour (store baseScale)
            growth = Random.Range(0.1f, 0.6f);              // every plant starts at a random size
            transform.rotation = SimUtils.RandomYRotation();
            UpdateVisualScale();
        }

        private void Start()
        {
            StartCoroutine(GrowRoutine());
        }

        /// <summary>
        /// COROUTINE: runs for the whole life of the plant.
        /// Instead of checking a timer every frame in Update, we simply
        /// "sleep" between two growth ticks with WaitForSeconds.
        /// </summary>
        private IEnumerator GrowRoutine()
        {
            // Each plant gets a slightly different rhythm so they don't all grow in sync.
            var wait = new WaitForSeconds(SimUtils.Vary(growthTickInterval, 0.2f));

            while (isAlive)
            {
                yield return wait;

                if (growth < 1f)
                {
                    growth = Mathf.Min(1f, growth + growthPerTick);
                    UpdateVisualScale();
                }
                else if (SimUtils.RollChance(seedChancePerTick))
                {
                    SpreadSeed();
                }
            }
        }

        private void SpreadSeed()
        {
            SimulationManager manager = SimulationManager.Instance;
            if (manager == null) return;

            Vector3 seedPosition = SimUtils.ClampToArena(
                SimUtils.RandomPointNear(Position, seedSpreadRadius), manager.ArenaHalfSize);
            manager.Spawn(Species.Grass, seedPosition);   // the manager enforces the population cap
        }

        private void UpdateVisualScale()
        {
            transform.localScale = baseScale * Mathf.Lerp(minVisualScale, 1f, growth);
        }

        public override string GetStatusText()
        {
            return $"{base.GetStatusText()} - growth {growth.ToPercent()}";
        }
    }
}
