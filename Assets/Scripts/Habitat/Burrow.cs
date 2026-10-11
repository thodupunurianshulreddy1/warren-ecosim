// ============================================================================
//  Burrow.cs   [H4]
//  A safe zone in the space. Any rabbit standing inside its radius is
//  HIDDEN: predators cannot see or eat it (Herbivore.CanBeEaten is false).
//
//  Design note (from the Part 1 analysis of Minecraft): agents react to a
//  PROPERTY OF THE SPACE instead of hard-coded triggers. The burrow does not
//  talk to predators at all; it only answers "is this point inside me?".
//
//  Every active burrow registers itself in a static list, so agents can query
//  them without a manager (same idea as SimulationManager's entity registry).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Habitat
{
    public class Burrow : MonoBehaviour
    {
        [Tooltip("Radius of the safe zone (slightly bigger than the visible hole).")]
        [SerializeField] private float radius = 1.3f;
        [Tooltip("The burrow where the player's warren starts (drawn differently in the editor).")]
        [SerializeField] private bool isHomeBurrow;

        private static readonly List<Burrow> activeBurrows = new List<Burrow>();

        public static IReadOnlyList<Burrow> All => activeBurrows;
        public Vector3 Position => transform.position;
        public float Radius => radius;
        public bool IsHomeBurrow => isHomeBurrow;

        private void OnEnable()
        {
            if (!activeBurrows.Contains(this)) activeBurrows.Add(this);
        }

        private void OnDisable()
        {
            activeBurrows.Remove(this);
        }

        /// <summary>Marks this burrow as the warren's home (called when it is spawned).</summary>
        public void SetAsHome(bool home)
        {
            isHomeBurrow = home;
        }

        public bool Contains(Vector3 point)
        {
            return SimUtils.IsWithinRange(point, Position, radius);
        }

        // --------------------------------------------------------------------
        //  Static queries (used by rabbits, abilities and the GameManager)
        // --------------------------------------------------------------------

        /// <summary>The burrow containing this point, or null.</summary>
        public static Burrow FindContaining(Vector3 point)
        {
            for (int i = 0; i < activeBurrows.Count; i++)
            {
                if (activeBurrows[i].Contains(point)) return activeBurrows[i];
            }
            return null;
        }

        /// <summary>The closest burrow within maxRange of the point, or null.</summary>
        public static Burrow FindClosest(Vector3 point, float maxRange)
        {
            Burrow closest = null;
            float bestSqr = maxRange * maxRange;
            foreach (Burrow burrow in activeBurrows)
            {
                float sqr = SimUtils.FlatSqrDistance(point, burrow.Position);
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    closest = burrow;
                }
            }
            return closest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isHomeBurrow ? new Color(1f, 0.8f, 0.2f) : new Color(0.6f, 0.4f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
