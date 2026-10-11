// ============================================================================
//  SimEntity.cs
//  ROOT of the OOP genealogy. Every living thing in the simulation
//  (plants AND animals) derives from this abstract class.
//
//  Genealogy:
//      SimEntity (abstract)
//       ├── Plant
//       └── Animal (abstract)
//            ├── Herbivore (abstract)  ── Rabbit, Deer
//            └── Carnivore (abstract)  ── Fox, Wolf
// ============================================================================

using System.Collections;
using UnityEngine;

namespace ThodupunuriAnshul.EcoSim.Core
{
    public abstract class SimEntity : MonoBehaviour
    {
        // --------------------------------------------------------------------
        //  Fields
        // --------------------------------------------------------------------
        [Header("Entity")]
        [Tooltip("Duration (seconds) of the shrinking animation played on death.")]
        [SerializeField] protected float deathAnimationDuration = 1.0f;

        protected bool isAlive = true;   // false as soon as Die() is called
        protected float age;             // seconds since this entity was spawned
        protected Vector3 baseScale;     // scale of the prefab, captured in Awake

        // --------------------------------------------------------------------
        //  Public read-only properties
        // --------------------------------------------------------------------
        public bool IsAlive => isAlive;
        public float Age => age;
        public Vector3 Position => transform.position;

        /// <summary>Each concrete class says which species it is (overridden in every leaf class).</summary>
        public abstract Species Species { get; }

        /// <summary>
        /// [H4] Raised once when this entity dies (entity, cause).
        /// The GameManager listens to it to know when the player's rabbit is killed,
        /// without the agents having to know that a game layer exists.
        /// </summary>
        public event System.Action<SimEntity, string> Died;

        // --------------------------------------------------------------------
        //  Unity lifecycle (virtual so that children can extend them)
        // --------------------------------------------------------------------
        protected virtual void Awake()
        {
            baseScale = transform.localScale;
        }

        protected virtual void OnEnable()
        {
            // Register ourselves so other agents can "see" us through the manager.
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        protected virtual void OnDisable()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        protected virtual void Update()
        {
            if (!isAlive) return;
            age += Time.deltaTime;
        }

        // --------------------------------------------------------------------
        //  Life & death
        // --------------------------------------------------------------------

        /// <summary>
        /// Kills the entity. Virtual: Animal overrides it to add a "falling over"
        /// effect and to stop its behaviour coroutines.
        /// </summary>
        public virtual void Die(string cause)
        {
            if (!isAlive) return;                 // guard: can only die once
            isAlive = false;

            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.Unregister(this);
                SimulationManager.Instance.ReportDeath(Species, cause);
            }

            Died?.Invoke(this, cause);   // [H4] notify listeners (e.g. GameManager)

            StartCoroutine(ShrinkAndDestroyRoutine(deathAnimationDuration));
        }

        /// <summary>
        /// COROUTINE: smoothly shrinks the entity to zero over 'duration'
        /// seconds, then destroys the GameObject.
        /// </summary>
        protected IEnumerator ShrinkAndDestroyRoutine(float duration)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;                // wait one frame
            }

            Destroy(gameObject);
        }

        /// <summary>Text displayed in logs / debug labels. Overridden by children.</summary>
        public virtual string GetStatusText()
        {
            return $"{Species} (age {age:0}s)";
        }
    }
}
