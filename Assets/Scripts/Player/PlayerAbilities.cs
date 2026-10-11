// ============================================================================
//  PlayerAbilities.cs   [H4]
//  The player's unlockable abilities: which ones are unlocked, their
//  cooldowns, and what they do. Kept separate from PlayerController
//  (movement / eating / mating) so each class has one job.
//
//   Thump      : stamps the ground; every warren rabbit in range receives an
//                alarm about the nearest predator and runs for a burrow.
//                -> re-uses Herbivore.ReceiveAlarm (the deer herd alarm).
//   Dig Burrow : spends energy to create a new burrow (a new safe zone).
//   Swift Feet : passive speed bonus.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Habitat;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Player
{
    public class PlayerAbilities : MonoBehaviour
    {
        [Header("Thump (alarm the warren)")]
        [Tooltip("Warren rabbits closer than this hear the thump.")]
        [SerializeField] private float thumpRadius = 12f;
        [Tooltip("Max distance of the predator the thump warns about.")]
        [SerializeField] private float thumpThreatRadius = 14f;
        [SerializeField] private float thumpCooldown = 8f;

        [Header("Dig burrow")]
        [SerializeField] private Burrow burrowPrefab;
        [SerializeField] private float digCooldown = 20f;
        [Tooltip("Fraction of max energy spent to dig.")]
        [Range(0f, 0.5f)] [SerializeField] private float digEnergyCost = 0.2f;
        [Tooltip("A new burrow must be at least this far from any other burrow.")]
        [SerializeField] private float minBurrowSpacing = 6f;
        [SerializeField] private int maxDugBurrows = 4;

        [Header("Swift feet (passive)")]
        [Range(0f, 0.5f)] [SerializeField] private float swiftFeetSpeedBonus = 0.15f;

        private readonly HashSet<AbilityType> unlocked = new HashSet<AbilityType>();
        private readonly Dictionary<AbilityType, float> readyTime = new Dictionary<AbilityType, float>();
        private readonly List<Rabbit> rabbitBuffer = new List<Rabbit>();
        private int dugBurrows;

        public static readonly AbilityType[] AllAbilities =
            (AbilityType[])Enum.GetValues(typeof(AbilityType));

        /// <summary>Raised when an ability becomes available (the HUD shows a toast).</summary>
        public event Action<AbilityType> AbilityUnlocked;

        /// <summary>Speed bonus added by passive abilities (0 if none).</summary>
        public float SpeedBonus => IsUnlocked(AbilityType.SwiftFeet) ? swiftFeetSpeedBonus : 0f;

        private void Awake()
        {
            burrowPrefab = PrefabLookup.Resolve(burrowPrefab, "Burrow");   // editor safety net
        }

        // ====================================================================
        //  Unlocking & cooldowns
        // ====================================================================
        public bool IsUnlocked(AbilityType ability) => unlocked.Contains(ability);

        public void Unlock(AbilityType ability)
        {
            if (unlocked.Add(ability)) AbilityUnlocked?.Invoke(ability);   // Add is false if already unlocked
        }

        /// <summary>Back to the start of a run: nothing unlocked, no cooldown.</summary>
        public void ResetAll()
        {
            unlocked.Clear();
            readyTime.Clear();
            dugBurrows = 0;
        }

        public bool IsPassive(AbilityType ability) => ability == AbilityType.SwiftFeet;

        public float GetCooldownDuration(AbilityType ability)
        {
            switch (ability)
            {
                case AbilityType.Thump:     return thumpCooldown;
                case AbilityType.DigBurrow: return digCooldown;
                default:                    return 0f;
            }
        }

        public float GetCooldownRemaining(AbilityType ability)
        {
            readyTime.TryGetValue(ability, out float time);
            return Mathf.Max(0f, time - Time.time);
        }

        public static string GetDisplayName(AbilityType ability)
        {
            switch (ability)
            {
                case AbilityType.Thump:     return "Thump";
                case AbilityType.DigBurrow: return "Dig Burrow";
                case AbilityType.SwiftFeet: return "Swift Feet";
                default:                    return ability.ToString();
            }
        }

        /// <summary>The key that triggers an active ability (null for passives).</summary>
        public static GameKey? GetKey(AbilityType ability)
        {
            switch (ability)
            {
                case AbilityType.Thump:     return GameKey.Thump;
                case AbilityType.DigBurrow: return GameKey.Dig;
                default:                    return null;
            }
        }

        // ====================================================================
        //  Using an ability
        // ====================================================================

        /// <summary>
        /// Tries to use an active ability with 'user' as the source.
        /// 'feedback' is always a short message for the player (success or reason of failure).
        /// </summary>
        public bool TryUse(AbilityType ability, Rabbit user, out string feedback)
        {
            if (user == null || !user.IsAlive)
            {
                feedback = null;
                return false;
            }
            if (!IsUnlocked(ability))
            {
                feedback = $"{GetDisplayName(ability)} is still locked: grow your warren.";
                return false;
            }
            float remaining = GetCooldownRemaining(ability);
            if (remaining > 0f)
            {
                feedback = $"{GetDisplayName(ability)} ready in {Mathf.CeilToInt(remaining)}s.";
                return false;
            }

            bool success;
            switch (ability)
            {
                case AbilityType.Thump:     success = Thump(user, out feedback); break;
                case AbilityType.DigBurrow: success = DigBurrow(user, out feedback); break;
                default:
                    feedback = $"{GetDisplayName(ability)} is passive: it is always active.";
                    success = false;
                    break;
            }

            if (success) readyTime[ability] = Time.time + GetCooldownDuration(ability);
            return success;
        }

        private bool Thump(Rabbit user, out string feedback)
        {
            IReadOnlyList<SimEntity> world = SimulationManager.Instance.Entities;
            Carnivore threat = SimUtils.FindClosest<Carnivore>(world, user.Position, thumpThreatRadius);
            if (threat == null)
            {
                feedback = "No predator close enough to warn about.";
                return false;
            }

            SimUtils.FindAllInRange(world, user.Position, thumpRadius, rabbitBuffer);
            int warned = 0;
            foreach (Rabbit rabbit in rabbitBuffer)
            {
                if (rabbit == user || !rabbit.IsWarrenMember) continue;
                rabbit.ReceiveAlarm(threat);
                warned++;
            }

            feedback = $"THUMP! {warned} warren rabbit(s) warned about the {threat.Species}.";
            return true;
        }

        private bool DigBurrow(Rabbit user, out string feedback)
        {
            if (burrowPrefab == null)
            {
                feedback = "No burrow prefab assigned (run EcoSim > Build Everything).";
                return false;
            }
            if (dugBurrows >= maxDugBurrows)
            {
                feedback = $"You already dug {maxDugBurrows} burrows.";
                return false;
            }
            if (Burrow.FindClosest(user.Position, minBurrowSpacing) != null)
            {
                feedback = "Too close to another burrow.";
                return false;
            }
            if (user.Energy01 < digEnergyCost + 0.1f)
            {
                feedback = "Too tired to dig: eat first.";
                return false;
            }

            user.SpendEnergy(digEnergyCost);
            Vector3 position = user.Position.Flattened();
            Burrow burrow = Instantiate(burrowPrefab, position, Quaternion.identity);
            burrow.name = $"Burrow_Dug_{++dugBurrows}";
            feedback = "You dug a new burrow: a new safe place for your warren.";
            return true;
        }
    }
}
