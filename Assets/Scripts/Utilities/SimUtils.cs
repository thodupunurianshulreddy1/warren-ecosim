// ============================================================================
//  SimUtils.cs
//  STATIC utility class: stateless helper functions used by every agent.
//  A static class cannot be instantiated and holds no per-object data,
//  which is exactly what we want for pure "toolbox" functions.
//
//  Sections:
//    1. Vector / distance helpers   (Vector3 maths on the ground plane)
//    2. Random helpers              (wrappers around UnityEngine.Random)
//    3. Searching helpers           (generic "find closest / best" queries)
//    4. Visual helpers              (colours for states & species)
//    5. Extension methods
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using Random = UnityEngine.Random;   // avoid ambiguity with System.Random

namespace ThodupunuriAnshul.EcoSim.Utilities
{
    public static class SimUtils
    {
        // Shader property IDs cached once (faster than using strings every frame).
        // "_BaseColor" is used by URP/HDRP, "_Color" by the Built-in pipeline.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // ====================================================================
        //  1. VECTOR / DISTANCE HELPERS
        // ====================================================================

        /// <summary>Normalized direction from 'from' to 'to', ignoring height (Y).</summary>
        public static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;
        }

        /// <summary>Squared distance on the XZ plane (cheaper than Distance: no square root).</summary>
        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>True if the two points are closer than 'range' on the ground plane.</summary>
        public static bool IsWithinRange(Vector3 a, Vector3 b, float range)
        {
            return FlatSqrDistance(a, b) <= range * range;
        }

        /// <summary>Keeps a position inside the square arena [-halfSize, +halfSize].</summary>
        public static Vector3 ClampToArena(Vector3 position, float halfSize)
        {
            position.x = Mathf.Clamp(position.x, -halfSize, halfSize);
            position.z = Mathf.Clamp(position.z, -halfSize, halfSize);
            return position;
        }

        /// <summary>Returns true when the point is close to one of the arena walls.</summary>
        public static bool IsNearArenaEdge(Vector3 position, float halfSize, float margin)
        {
            return Mathf.Abs(position.x) > halfSize - margin || Mathf.Abs(position.z) > halfSize - margin;
        }

        /// <summary>Rotates a flat direction around the Y axis by 'degrees'.</summary>
        public static Vector3 RotateFlat(Vector3 direction, float degrees)
        {
            return Quaternion.Euler(0f, degrees, 0f) * direction;
        }

        // ====================================================================
        //  2. RANDOM HELPERS  (UnityEngine.Random)
        // ====================================================================

        /// <summary>Random point anywhere inside the square arena, on the ground.</summary>
        public static Vector3 RandomPointInArena(float halfSize)
        {
            return new Vector3(Random.Range(-halfSize, halfSize), 0f, Random.Range(-halfSize, halfSize));
        }

        /// <summary>Random point inside a circle of 'radius' around 'center' (on the ground).</summary>
        public static Vector3 RandomPointNear(Vector3 center, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return new Vector3(center.x + offset.x, 0f, center.z + offset.y);
        }

        /// <summary>Random point on the border of the arena (used for "immigrants").</summary>
        public static Vector3 RandomPointOnArenaEdge(float halfSize)
        {
            float along = Random.Range(-halfSize, halfSize);
            switch (Random.Range(0, 4))           // pick one of the four walls
            {
                case 0: return new Vector3(along, 0f, halfSize);
                case 1: return new Vector3(along, 0f, -halfSize);
                case 2: return new Vector3(halfSize, 0f, along);
                default: return new Vector3(-halfSize, 0f, along);
            }
        }

        /// <summary>Returns true with the given probability (0 = never, 1 = always).</summary>
        public static bool RollChance(float probability)
        {
            return Random.value < probability;
        }

        /// <summary>Returns 'value' randomly varied by +/- 'percent' (e.g. 0.1 = ±10%).</summary>
        public static float Vary(float value, float percent)
        {
            return value * Random.Range(1f - percent, 1f + percent);
        }

        /// <summary>Random facing rotation around the Y axis.</summary>
        public static Quaternion RandomYRotation()
        {
            return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // ====================================================================
        //  3. SEARCHING HELPERS  (generic methods with constraints)
        // ====================================================================

        /// <summary>
        /// Finds the closest living entity of type T within 'maxRange' of 'origin'.
        /// An optional filter lets the caller add extra conditions
        /// (e.g. "only plants that are grown enough").
        /// </summary>
        public static T FindClosest<T>(IReadOnlyList<SimEntity> pool, Vector3 origin, float maxRange,
                                       Func<T, bool> filter = null) where T : SimEntity
        {
            // A "closest" search is just a "best" search where the score is the distance.
            return FindBest<T>(pool, origin, maxRange, candidate => FlatSqrDistance(origin, candidate.Position), filter);
        }

        /// <summary>
        /// Finds the living entity of type T within range that has the LOWEST score.
        /// The score function lets each agent express its own preferences
        /// (e.g. wolves prefer deer over rabbits).
        /// </summary>
        public static T FindBest<T>(IReadOnlyList<SimEntity> pool, Vector3 origin, float maxRange,
                                    Func<T, float> score, Func<T, bool> filter = null) where T : SimEntity
        {
            T best = null;
            float bestScore = float.MaxValue;
            float maxRangeSqr = maxRange * maxRange;

            for (int i = 0; i < pool.Count; i++)
            {
                // "as" returns null if the entity is not of type T -> cheap type filter
                T candidate = pool[i] as T;
                if (candidate == null || !candidate.IsAlive) continue;
                if (FlatSqrDistance(origin, candidate.Position) > maxRangeSqr) continue;
                if (filter != null && !filter(candidate)) continue;

                float candidateScore = score(candidate);
                if (candidateScore < bestScore)
                {
                    bestScore = candidateScore;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>Collects every living entity of type T within range into 'results'.</summary>
        public static void FindAllInRange<T>(IReadOnlyList<SimEntity> pool, Vector3 origin, float maxRange,
                                             List<T> results) where T : SimEntity
        {
            results.Clear();
            float maxRangeSqr = maxRange * maxRange;
            foreach (SimEntity entity in pool)
            {
                if (entity is T typed && typed.IsAlive && FlatSqrDistance(origin, typed.Position) <= maxRangeSqr)
                    results.Add(typed);
            }
        }

        // ====================================================================
        //  4. VISUAL HELPERS
        // ====================================================================

        /// <summary>Colour code of each behavioural state (status light + HUD legend).</summary>
        public static Color GetStateColor(AgentState state)
        {
            switch (state)
            {
                case AgentState.Wandering:   return new Color(0.85f, 0.85f, 0.85f); // light grey
                case AgentState.SeekingFood: return new Color(0.30f, 0.90f, 0.30f); // green
                case AgentState.Hunting:     return new Color(1.00f, 0.15f, 0.15f); // red
                case AgentState.Eating:      return new Color(1.00f, 0.85f, 0.10f); // yellow
                case AgentState.Fleeing:     return new Color(0.20f, 0.60f, 1.00f); // blue
                case AgentState.SeekingMate: return new Color(1.00f, 0.45f, 0.80f); // pink
                case AgentState.Mating:      return new Color(0.75f, 0.20f, 1.00f); // purple
                case AgentState.Resting:     return new Color(0.30f, 0.30f, 0.45f); // dark blue-grey
                default:                     return Color.black;                    // Dead
            }
        }

        /// <summary>Colour of each species (HUD legend).</summary>
        public static Color GetSpeciesColor(Species species)
        {
            switch (species)
            {
                case Species.Grass:  return new Color(0.25f, 0.70f, 0.20f);
                case Species.Rabbit: return new Color(0.92f, 0.92f, 0.90f);
                case Species.Deer:   return new Color(0.60f, 0.40f, 0.20f);
                case Species.Fox:    return new Color(0.95f, 0.50f, 0.10f);
                case Species.Wolf:   return new Color(0.40f, 0.40f, 0.45f);
                default:             return Color.white;
            }
        }

        /// <summary>Slightly randomizes a colour so that individuals look different.</summary>
        public static Color VaryColor(Color color, float amount)
        {
            float shift = Random.Range(-amount, amount);
            return new Color(Mathf.Clamp01(color.r + shift), Mathf.Clamp01(color.g + shift),
                             Mathf.Clamp01(color.b + shift), color.a);
        }

        /// <summary>
        /// Sets a renderer's colour WITHOUT creating a new material instance
        /// (MaterialPropertyBlock). Works in both Built-in and URP pipelines.
        /// </summary>
        public static void SetRendererColor(Renderer renderer, Color color, MaterialPropertyBlock block)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        // ====================================================================
        //  5. EXTENSION METHODS  (only possible inside a static class)
        // ====================================================================

        /// <summary>Usage: someVector.Flattened()  -> same vector with y = 0.</summary>
        public static Vector3 Flattened(this Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }

        /// <summary>Usage: 0.42f.ToPercent() -> "42%".</summary>
        public static string ToPercent(this float value01)
        {
            return Mathf.RoundToInt(value01 * 100f) + "%";
        }
    }
}
