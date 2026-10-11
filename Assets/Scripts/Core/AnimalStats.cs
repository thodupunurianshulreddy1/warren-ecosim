// ============================================================================
//  AnimalStats.cs
//  Plain C# data class (NOT a MonoBehaviour) grouping every tuning value
//  of an animal. [System.Serializable] makes it editable in the Inspector.
//  Each species builds its own default stats by overriding
//  Animal.CreateDefaultStats()  ->  see Rabbit, Deer, Fox, Wolf.
// ============================================================================

using UnityEngine;

namespace ThodupunuriAnshul.EcoSim.Core
{
    [System.Serializable]
    public class AnimalStats
    {
        [Header("Movement")]
        [Tooltip("Normal walking speed (units / second).")]
        public float walkSpeed = 2f;
        [Tooltip("Speed multiplier applied when running (fleeing / hunting).")]
        public float runSpeedMultiplier = 2f;
        [Tooltip("Maximum rotation speed (degrees / second).")]
        public float turnSpeed = 360f;
        [Tooltip("Max distance of a random wander destination.")]
        public float wanderRadius = 6f;

        [Header("Senses")]
        [Tooltip("Distance at which the animal can see food, threats and mates.")]
        public float senseRadius = 8f;
        [Tooltip("Distance at which the animal 'touches' its target (eat / mate).")]
        public float contactRange = 0.6f;
        [Tooltip("Seconds between two decisions (lower = more reactive, more CPU).")]
        public float decisionInterval = 0.25f;

        [Header("Energy")]
        public float maxEnergy = 100f;
        [Range(0f, 1f)] public float startEnergyFraction = 0.7f;
        [Tooltip("Energy lost per second while walking.")]
        public float energyDrainPerSecond = 2f;
        [Tooltip("Drain multiplier while running.")]
        public float runEnergyMultiplier = 2f;
        [Tooltip("Below this energy fraction the animal looks for food.")]
        [Range(0f, 1f)] public float hungerThreshold = 0.6f;
        [Tooltip("Seconds spent eating / digesting.")]
        public float eatDuration = 1.5f;
        [Tooltip("Energy given to a predator when this animal is eaten (herbivores only).")]
        public float meatValue = 40f;

        [Header("Stamina (carnivores)")]
        [Tooltip("Max seconds a carnivore can chase before giving up.")]
        public float staminaSeconds = 6f;
        [Tooltip("Seconds of rest needed after giving up a chase.")]
        public float restDuration = 3f;

        [Header("Life cycle")]
        [Tooltip("Age (seconds) at which the animal becomes an adult and can mate.")]
        public float maturityAge = 15f;
        [Tooltip("Age (seconds) at which the animal dies of old age.")]
        public float maxAge = 120f;
        [Tooltip("Scale of a newborn, relative to an adult.")]
        [Range(0.1f, 1f)] public float newbornScale = 0.5f;

        [Header("Reproduction")]
        [Tooltip("Minimum energy fraction needed to look for a mate.")]
        [Range(0f, 1f)] public float reproductionEnergyThreshold = 0.75f;
        [Tooltip("Fraction of max energy spent by EACH parent.")]
        [Range(0f, 1f)] public float reproductionCost = 0.35f;
        [Tooltip("Seconds before the animal can mate again.")]
        public float reproductionCooldown = 20f;
        [Tooltip("Seconds the two parents stay together.")]
        public float matingDuration = 1.5f;
    }
}
