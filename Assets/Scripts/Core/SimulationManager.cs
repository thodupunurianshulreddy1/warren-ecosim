// ============================================================================
//  SimulationManager.cs
//  Central "world" object (Singleton pattern).
//   - Keeps the registry of every living entity (agents query it to sense)
//   - Spawns the initial population and every newborn (prefab per Species)
//   - Enforces population caps
//   - COROUTINES: periodic plant spawning + periodic census / immigration
//   - Optional fixed Random seed for reproducible runs
//   - [H4] World modifiers (predator senses / speed) that the game layer
//     changes at night and as days pass, plus reusable spawn helpers
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Core
{
    /// <summary>One snapshot of the population, taken by the census coroutine.</summary>
    public struct CensusRecord
    {
        public float time;
        public int[] counts;   // indexed by (int)Species
    }

    public class SimulationManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        //  Singleton
        // --------------------------------------------------------------------
        public static SimulationManager Instance { get; private set; }

        // --------------------------------------------------------------------
        //  Inspector settings
        // --------------------------------------------------------------------
        [Header("Arena")]
        [Tooltip("Half the width of the square play area (ground plane is 2x this).")]
        [SerializeField] private float arenaHalfSize = 25f;

        [Header("Randomness")]
        [Tooltip("If enabled, every run with the same seed gives the same simulation.")]
        [SerializeField] private bool useFixedSeed = false;
        [SerializeField] private int randomSeed = 12345;

        [Header("Prefabs")]
        [SerializeField] private Plant grassPrefab;
        [SerializeField] private Rabbit rabbitPrefab;
        [SerializeField] private Deer deerPrefab;
        [SerializeField] private Fox foxPrefab;
        [SerializeField] private Wolf wolfPrefab;

        [Header("Initial population")]
        [SerializeField] private int initialGrass = 70;
        [SerializeField] private int initialRabbits = 20;
        [SerializeField] private int initialDeer = 10;
        [SerializeField] private int initialFoxes = 3;
        [SerializeField] private int initialWolves = 3;

        [Header("Population caps")]
        [SerializeField] private int maxGrass = 160;
        [SerializeField] private int maxRabbits = 70;
        [SerializeField] private int maxDeer = 30;
        [SerializeField] private int maxFoxes = 12;
        [SerializeField] private int maxWolves = 10;

        [Header("Plant spawner (coroutine)")]
        [SerializeField] private float plantSpawnInterval = 1.5f;
        [SerializeField] private int plantsPerSpawn = 2;

        [Header("Census & immigration (coroutine)")]
        [Tooltip("Seconds between two population snapshots.")]
        [SerializeField] private float censusInterval = 3f;
        [Tooltip("Chance per census that an extinct species gets 2 immigrants.")]
        [Range(0f, 1f)] [SerializeField] private float immigrationChance = 0.35f;
        [SerializeField] private bool logCensusToConsole = true;
        [SerializeField] private int maxHistoryLength = 120;

        // --------------------------------------------------------------------
        //  Runtime data
        // --------------------------------------------------------------------
        private readonly List<SimEntity> entities = new List<SimEntity>();
        private readonly HashSet<SimEntity> registered = new HashSet<SimEntity>();
        private readonly Dictionary<Species, int> population = new Dictionary<Species, int>();
        private readonly Dictionary<Species, Transform> containers = new Dictionary<Species, Transform>();
        private readonly Dictionary<string, int> deathCauses = new Dictionary<string, int>();
        private readonly List<CensusRecord> censusHistory = new List<CensusRecord>();
        private int spawnCounter;   // unique number in each spawned object's name

        public static readonly Species[] AllSpecies =
            (Species[])System.Enum.GetValues(typeof(Species));

        // --------------------------------------------------------------------
        //  Public read-only API
        // --------------------------------------------------------------------
        public IReadOnlyList<SimEntity> Entities => entities;
        public float ArenaHalfSize => arenaHalfSize;
        public float ElapsedTime { get; private set; }
        public int TotalBirths { get; private set; }
        public int TotalDeaths { get; private set; }
        public IReadOnlyDictionary<string, int> DeathCauses => deathCauses;
        public IReadOnlyList<CensusRecord> CensusHistory => censusHistory;

        public int CountOf(Species species) => population[species];

        // --------------------------------------------------------------------
        //  [H4] World modifiers (written by the GameManager, read by agents)
        //  The simulation still works alone: by default both are 1.
        // --------------------------------------------------------------------

        /// <summary>Multiplies the sense radius of every carnivore (higher at night, grows each day).</summary>
        public float PredatorSenseMultiplier { get; set; } = 1f;

        /// <summary>Multiplies the chase speed of every carnivore (grows each day).</summary>
        public float PredatorSpeedMultiplier { get; set; } = 1f;

        // ====================================================================
        //  LIFECYCLE
        // ====================================================================
        private void Awake()
        {
            // Singleton: the most recently loaded manager becomes the global access point.
            // (We overwrite instead of destroying, so a scene reload never kills the new manager.)
            if (Instance != null && Instance != this)
                Debug.LogWarning("[EcoSim] More than one SimulationManager in the scene.");
            Instance = this;
            ResolveMissingPrefabs();   // [H4] editor safety net

            if (useFixedSeed) Random.InitState(randomSeed);

            // One counter + one tidy hierarchy folder per species.
            foreach (Species species in AllSpecies)
            {
                population[species] = 0;
                var container = new GameObject(species + "_Container").transform;
                container.SetParent(transform);
                containers[species] = container;
            }
        }

        private void Start()
        {
            SpawnInitialPopulation();
            StartCoroutine(PlantSpawnerRoutine());
            StartCoroutine(CensusRoutine());
        }

        private void Update()
        {
            ElapsedTime += Time.deltaTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ====================================================================
        //  REGISTRY
        // ====================================================================
        public void Register(SimEntity entity)
        {
            if (!entity.IsAlive || !registered.Add(entity)) return;   // HashSet.Add is false if already there
            entities.Add(entity);
            population[entity.Species]++;
        }

        public void Unregister(SimEntity entity)
        {
            if (!registered.Remove(entity)) return;
            entities.Remove(entity);
            population[entity.Species]--;
        }

        public void ReportDeath(Species species, string cause)
        {
            if (species == Species.Grass) return;     // plants are not tracked in statistics
            TotalDeaths++;
            string key = $"{species} - {cause}";
            deathCauses.TryGetValue(key, out int count);
            deathCauses[key] = count + 1;
        }

        // ====================================================================
        //  SPAWNING
        // ====================================================================

        /// <summary>Spawns one entity of the given species (returns null if capped).</summary>
        public SimEntity Spawn(Species species, Vector3 position)
        {
            if (CountOf(species) >= GetCap(species)) return null;

            SimEntity prefab = GetPrefab(species);
            if (prefab == null)
            {
                Debug.LogWarning($"[EcoSim] No prefab assigned for {species}.");
                return null;
            }

            SimEntity instance = Instantiate(prefab, position, Quaternion.identity, containers[species]);
            // [H4] A simple counter (GetInstanceID() is obsolete in Unity 6.6+).
            instance.name = $"{species}_{++spawnCounter}";
            if (species != Species.Grass) TotalBirths++;
            return instance;
        }

        private void SpawnInitialPopulation()
        {
            SpawnMany(Species.Grass, initialGrass);
            SpawnMany(Species.Rabbit, initialRabbits);
            SpawnMany(Species.Deer, initialDeer);
            SpawnMany(Species.Fox, initialFoxes);
            SpawnMany(Species.Wolf, initialWolves);
            TotalBirths = 0;   // the starting population does not count as births
        }

        private void SpawnMany(Species species, int amount)
        {
            for (int i = 0; i < amount; i++)
                SpawnAdult(species, SimUtils.RandomPointInArena(arenaHalfSize - 1f));
        }

        /// <summary>
        /// [H4] Spawns an entity and, if it is an animal, makes it a random adult
        /// (refactored out of SpawnMany / HandleExtinctions, now also used by the GameManager).
        /// </summary>
        public SimEntity SpawnAdult(Species species, Vector3 position)
        {
            SimEntity spawned = Spawn(species, position);

            // Pattern matching: only animals have an age to randomize.
            if (spawned is Animal animal) animal.RandomizeAsAdult();
            return spawned;
        }

        /// <summary>
        /// [H4] Spawns a small group of adults entering from a random point of the arena edge.
        /// Returns how many were actually spawned (population caps may block some).
        /// </summary>
        public int SpawnGroupAtArenaEdge(Species species, int count)
        {
            Vector3 entryPoint = SimUtils.RandomPointOnArenaEdge(arenaHalfSize - 1f);
            int spawnedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (SpawnAdult(species, SimUtils.RandomPointNear(entryPoint, 1.5f)) != null) spawnedCount++;
            }
            return spawnedCount;
        }

        /// <summary>
        /// [H4] If the scene lost a prefab reference, reload it from Assets/Prefabs (editor only).
        /// OnValidate also runs it when the scene is opened, so the Inspector shows the fix.
        /// </summary>
        private void ResolveMissingPrefabs()
        {
            grassPrefab = PrefabLookup.Resolve(grassPrefab, "Grass");
            rabbitPrefab = PrefabLookup.Resolve(rabbitPrefab, "Rabbit");
            deerPrefab = PrefabLookup.Resolve(deerPrefab, "Deer");
            foxPrefab = PrefabLookup.Resolve(foxPrefab, "Fox");
            wolfPrefab = PrefabLookup.Resolve(wolfPrefab, "Wolf");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveMissingPrefabs();
        }
#endif

        private SimEntity GetPrefab(Species species)
        {
            switch (species)
            {
                case Species.Grass:  return grassPrefab;
                case Species.Rabbit: return rabbitPrefab;
                case Species.Deer:   return deerPrefab;
                case Species.Fox:    return foxPrefab;
                case Species.Wolf:   return wolfPrefab;
                default:             return null;
            }
        }

        private int GetCap(Species species)
        {
            switch (species)
            {
                case Species.Grass:  return maxGrass;
                case Species.Rabbit: return maxRabbits;
                case Species.Deer:   return maxDeer;
                case Species.Fox:    return maxFoxes;
                case Species.Wolf:   return maxWolves;
                default:             return 0;
            }
        }

        private int GetInitialCount(Species species)
        {
            switch (species)
            {
                case Species.Rabbit: return initialRabbits;
                case Species.Deer:   return initialDeer;
                case Species.Fox:    return initialFoxes;
                case Species.Wolf:   return initialWolves;
                default:             return initialGrass;
            }
        }

        // ====================================================================
        //  COROUTINES
        // ====================================================================

        /// <summary>COROUTINE: new grass sprouts at random places every few seconds.</summary>
        private IEnumerator PlantSpawnerRoutine()
        {
            var wait = new WaitForSeconds(plantSpawnInterval);
            while (true)
            {
                yield return wait;
                for (int i = 0; i < plantsPerSpawn; i++)
                    Spawn(Species.Grass, SimUtils.RandomPointInArena(arenaHalfSize - 1f));
            }
        }

        /// <summary>COROUTINE: records population history and handles extinctions.</summary>
        private IEnumerator CensusRoutine()
        {
            var wait = new WaitForSeconds(censusInterval);
            while (true)
            {
                RecordCensus();
                yield return wait;
                HandleExtinctions();
            }
        }

        private void RecordCensus()
        {
            var record = new CensusRecord { time = ElapsedTime, counts = new int[AllSpecies.Length] };
            foreach (Species species in AllSpecies) record.counts[(int)species] = CountOf(species);

            censusHistory.Add(record);
            if (censusHistory.Count > maxHistoryLength) censusHistory.RemoveAt(0);

            if (logCensusToConsole)
            {
                Debug.Log($"[EcoSim] t={ElapsedTime:0}s  Grass {record.counts[0]} | Rabbits {record.counts[1]} | " +
                          $"Deer {record.counts[2]} | Foxes {record.counts[3]} | Wolves {record.counts[4]}");
            }
        }

        /// <summary>An extinct animal species may randomly get a pair of immigrants.</summary>
        private void HandleExtinctions()
        {
            foreach (Species species in AllSpecies)
            {
                bool isAnimal = species != Species.Grass;
                bool isExtinct = CountOf(species) == 0 && GetInitialCount(species) > 0;
                if (!isAnimal || !isExtinct || !SimUtils.RollChance(immigrationChance)) continue;

                int arrived = SpawnGroupAtArenaEdge(species, 2);
                if (logCensusToConsole) Debug.Log($"[EcoSim] {arrived} {species} immigrant(s) arrived.");
            }
        }

        // ====================================================================
        //  CONTROLS (called by the HUD buttons)
        // ====================================================================
        public void SetSimulationSpeed(float timeScale)
        {
            Time.timeScale = Mathf.Max(0f, timeScale);
        }

        public void RestartSimulation()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
