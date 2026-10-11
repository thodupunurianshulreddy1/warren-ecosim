// ============================================================================
//  GameSettings.cs   [H4]
//  Plain [Serializable] data classes holding the game's balancing values.
//  They are fields of the GameManager, so a designer tunes the difficulty
//  curve, the unlocks and the score in the Inspector without touching code.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Player;

namespace ThodupunuriAnshul.EcoSim.Game
{
    /// <summary>How predators get stronger: at night, and a little more every day.</summary>
    [Serializable]
    public class DifficultySettings
    {
        [Tooltip("Predator sense radius multiplier at night.")]
        public float nightSenseMultiplier = 1.35f;
        [Tooltip("Extra predator sense per day (0.04 = +4% per day).")]
        public float senseGrowthPerDay = 0.04f;
        [Tooltip("Extra predator chase speed per day (0.025 = +2.5% per day).")]
        public float speedGrowthPerDay = 0.025f;
        [Tooltip("New predators arriving on the first night.")]
        public int predatorsFirstNight = 1;
        [Tooltip("One more predator arrives every N nights.")]
        public int extraPredatorEveryNights = 2;

        public float GetSenseMultiplier(int day, bool isNight)
        {
            float daily = 1f + senseGrowthPerDay * (day - 1);
            return isNight ? daily * nightSenseMultiplier : daily;
        }

        public float GetSpeedMultiplier(int day)
        {
            return 1f + speedGrowthPerDay * (day - 1);
        }

        public int GetPredatorsForNight(int day)
        {
            return predatorsFirstNight + (day - 1) / Mathf.Max(1, extraPredatorEveryNights);
        }
    }

    /// <summary>One milestone of the progression loop: warren size -> ability.</summary>
    [Serializable]
    public class AbilityUnlock
    {
        public AbilityType ability;
        public int warrenSizeRequired;

        public AbilityUnlock() { }   // needed by Unity's serializer

        public AbilityUnlock(AbilityType ability, int warrenSizeRequired)
        {
            this.ability = ability;
            this.warrenSizeRequired = warrenSizeRequired;
        }
    }

    /// <summary>Unlock milestones, star thresholds and the score formula.</summary>
    [Serializable]
    public class ProgressionSettings
    {
        public List<AbilityUnlock> unlocks = new List<AbilityUnlock>
        {
            new AbilityUnlock(AbilityType.Thump, 3),
            new AbilityUnlock(AbilityType.DigBurrow, 6),
            new AbilityUnlock(AbilityType.SwiftFeet, 10)
        };

        [Header("Stars (only on victory)")]
        public int twoStarPeakWarren = 10;
        public int threeStarPeakWarren = 20;

        [Header("Score")]
        public int pointsPerDaySurvived = 100;
        public int pointsPerPeakWarrenMember = 20;
        public int pointsPerBirth = 5;
        public int pointsPerGeneration = 50;
        public int victoryBonus = 500;

        /// <summary>Warren size needed for an ability (-1 if it is never unlocked).</summary>
        public int GetRequirement(AbilityType ability)
        {
            foreach (AbilityUnlock unlock in unlocks)
            {
                if (unlock.ability == ability) return unlock.warrenSizeRequired;
            }
            return -1;
        }

        public int ComputeScore(RunRecord run)
        {
            return run.daysSurvived * pointsPerDaySurvived
                 + run.peakWarren * pointsPerPeakWarrenMember
                 + run.babiesBorn * pointsPerBirth
                 + (run.generations - 1) * pointsPerGeneration
                 + (run.victory ? victoryBonus : 0);
        }

        public int ComputeStars(RunRecord run)
        {
            if (!run.victory) return 0;
            if (run.peakWarren >= threeStarPeakWarren) return 3;
            return run.peakWarren >= twoStarPeakWarren ? 2 : 1;
        }
    }
}
