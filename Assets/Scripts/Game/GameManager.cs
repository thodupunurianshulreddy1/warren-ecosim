// ============================================================================
//  GameManager.cs   [H4]
//  The game layer on top of the Homework 3 simulation (Singleton).
//  The simulation does not know this class exists: the GameManager only
//  listens to events and uses public APIs (loose coupling).
//
//  Responsibilities:
//   - Run state machine: Title -> Playing <-> Paused -> GameOver / Victory
//   - Starting a run: burrows, founder rabbit, possession, day/night cycle
//   - LINEAGE: when the player's rabbit dies, control passes to the best
//     adult of the warren (generation +1); no heir = game over
//   - Difficulty curve: night / day modifiers, new predators each night
//   - Progression: warren size, ability unlocks, score, stars, best run
//   - Messages for the HUD (Notification event)
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Habitat;
using ThodupunuriAnshul.EcoSim.Player;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Game
{
    public class GameManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        //  Singleton
        // --------------------------------------------------------------------
        public static GameManager Instance { get; private set; }

        // --------------------------------------------------------------------
        //  Inspector
        // --------------------------------------------------------------------
        [Header("Scene references (assigned by the EcoSim builder)")]
        [SerializeField] private SimulationManager simulation;
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerAbilities playerAbilities;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private Burrow burrowPrefab;

        [Header("Run rules")]
        [Tooltip("The run is won at the dawn after this day.")]
        [SerializeField] private int daysToSurvive = 7;
        [Tooltip("Burrows placed in the wild at the start (plus the home burrow in the centre).")]
        [SerializeField] private int wildBurrowCount = 3;
        [SerializeField] private float minDistanceBetweenBurrows = 10f;
        [Tooltip("The possessed rabbit lives this many times longer than a wild one.")]
        [SerializeField] private float playerLifespanMultiplier = 2f;
        [Tooltip("Seconds between the death of your rabbit and taking control of the heir.")]
        [SerializeField] private float lineageTransferDelay = 1.5f;

        [Header("Balancing")]
        [SerializeField] private DifficultySettings difficulty = new DifficultySettings();
        [SerializeField] private ProgressionSettings progression = new ProgressionSettings();

        // --------------------------------------------------------------------
        //  Runtime
        // --------------------------------------------------------------------
        private const float WarrenScanInterval = 0.5f;
        private static bool startRunAfterReload;    // "Restart" reloads the scene, then starts directly

        private RunRecord currentRun = new RunRecord();
        private Rabbit possessedRabbit;
        private Vector3 lastPlayerPosition;
        private float warrenScanTimer;

        public GameState State { get; private set; } = GameState.Title;
        public int Day => dayNightCycle != null ? dayNightCycle.Day : 1;
        public bool IsNight => dayNightCycle != null && dayNightCycle.IsNight;
        public int DaysToSurvive => daysToSurvive;
        public int WarrenSize { get; private set; }
        public int Generation => currentRun.generations;
        public RunRecord CurrentRun => currentRun;
        public RunRecord BestRun { get; private set; }
        public bool IsNewBest { get; private set; }
        public string LastDeathCause { get; private set; }
        public bool IsTransferringLineage { get; private set; }

        public DayNightCycle DayNight => dayNightCycle;
        public PlayerController PlayerController => playerController;
        public PlayerAbilities PlayerAbilities => playerAbilities;
        public ProgressionSettings Progression => progression;
        public SimulationManager Simulation => simulation;

        public event Action<GameState> StateChanged;
        public event Action<string> Notification;

        // ====================================================================
        //  LIFECYCLE
        // ====================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[Warren] More than one GameManager in the scene.");
            Instance = this;
            burrowPrefab = PrefabLookup.Resolve(burrowPrefab, "Burrow");   // editor safety net
        }

        private void Start()
        {
            if (!HasAllReferences()) return;

            BestRun = RunRecordStorage.LoadBest();
            dayNightCycle.DayStarted += OnDayStarted;
            dayNightCycle.NightStarted += OnNightStarted;
            playerAbilities.AbilityUnlocked += OnAbilityUnlocked;
            playerController.Feedback += Notify;
            Rabbit.JoinedWarren += OnRabbitJoinedWarren;

            if (startRunAfterReload)
            {
                startRunAfterReload = false;
                StartRun();
            }
            else
            {
                SetState(GameState.Title);
            }
        }

        private void OnDestroy()
        {
            if (dayNightCycle != null)
            {
                dayNightCycle.DayStarted -= OnDayStarted;
                dayNightCycle.NightStarted -= OnNightStarted;
            }
            if (playerAbilities != null) playerAbilities.AbilityUnlocked -= OnAbilityUnlocked;
            if (playerController != null) playerController.Feedback -= Notify;
            Rabbit.JoinedWarren -= OnRabbitJoinedWarren;   // static event: must always unsubscribe
            if (possessedRabbit != null) possessedRabbit.Died -= OnPossessedRabbitDied;

            Rabbit.WarrenLeader = null;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (InputHelper.GetKeyDown(GameKey.Pause)) TogglePause();

            switch (State)
            {
                case GameState.Title:
                    if (InputHelper.GetKeyDown(GameKey.Confirm)) StartRun();
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                    if (InputHelper.GetKeyDown(GameKey.Confirm)) RestartRun();
                    break;

                case GameState.Playing:
                    TickWarren(Time.deltaTime);
                    break;
            }
        }

        private bool HasAllReferences()
        {
            bool ok = simulation != null && dayNightCycle != null && playerController != null
                      && playerAbilities != null && followCamera != null && burrowPrefab != null;
            if (!ok) Debug.LogError("[Warren] GameManager is missing references. Run 'EcoSim > Build Everything'.");
            return ok;
        }

        // ====================================================================
        //  STATE MACHINE
        // ====================================================================
        private void SetState(GameState newState)
        {
            State = newState;
            playerController.SetInputEnabled(newState == GameState.Playing);
            StateChanged?.Invoke(newState);
        }

        /// <summary>Title -> Playing. Also called directly after a restart reload.</summary>
        public void StartRun()
        {
            if (State == GameState.Playing || State == GameState.Paused) return;

            Time.timeScale = 1f;
            currentRun = new RunRecord { date = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            IsNewBest = false;
            LastDeathCause = null;
            playerAbilities.ResetAll();

            Vector3 home = Vector3.zero;
            SpawnBurrows(home);

            Rabbit founder = AcquireFounder(home);
            if (founder == null)
            {
                Debug.LogError("[Warren] Could not find or spawn a rabbit for the player.");
                return;
            }
            founder.JoinWarren();       // before SetState(Playing): the founder is not counted as a birth
            Possess(founder);

            ApplyDifficulty(1, false);
            SetState(GameState.Playing);
            dayNightCycle.StartCycle(); // raises DayStarted(1)
            RefreshWarren();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
            else if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Playing);
            }
        }

        /// <summary>Reloads the scene and starts a new run immediately.</summary>
        public void RestartRun()
        {
            startRunAfterReload = true;
            ReloadScene();
        }

        /// <summary>Reloads the scene and stays on the title screen.</summary>
        public void QuitToTitle()
        {
            startRunAfterReload = false;
            ReloadScene();
        }

        private void ReloadScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void EndRun(bool victory)
        {
            if (State != GameState.Playing && State != GameState.Paused) return;

            Time.timeScale = 1f;
            currentRun.victory = victory;
            currentRun.daysSurvived = victory ? daysToSurvive : Mathf.Max(0, Day - 1);
            currentRun.score = progression.ComputeScore(currentRun);
            currentRun.stars = progression.ComputeStars(currentRun);

            IsNewBest = RunRecordStorage.IsBetter(currentRun, BestRun);
            if (IsNewBest)
            {
                BestRun = currentRun.Clone();
                RunRecordStorage.SaveBest(BestRun);
            }

            dayNightCycle.StopCycle();
            playerController.Release();
            followCamera.ShowOverview();
            SetState(victory ? GameState.Victory : GameState.GameOver);
        }

        // ====================================================================
        //  RUN SETUP
        // ====================================================================
        private void SpawnBurrows(Vector3 home)
        {
            Transform container = new GameObject("Burrows").transform;

            Burrow homeBurrow = Instantiate(burrowPrefab, home, Quaternion.identity, container);
            homeBurrow.name = "Burrow_Home";
            homeBurrow.SetAsHome(true);

            float halfSize = simulation.ArenaHalfSize - 3f;
            int placed = 0;
            for (int attempt = 0; attempt < 50 && placed < wildBurrowCount; attempt++)
            {
                Vector3 candidate = SimUtils.RandomPointInArena(halfSize);
                if (Burrow.FindClosest(candidate, minDistanceBetweenBurrows) != null) continue;   // too close

                Burrow wild = Instantiate(burrowPrefab, candidate, Quaternion.identity, container);
                wild.name = $"Burrow_Wild_{++placed}";
            }
        }

        /// <summary>A fresh adult rabbit at home, or (if the rabbit cap is reached) the closest wild one.</summary>
        private Rabbit AcquireFounder(Vector3 home)
        {
            if (simulation.SpawnAdult(Species.Rabbit, home) is Rabbit spawned) return spawned;
            return SimUtils.FindClosest<Rabbit>(simulation.Entities, home, 1000f, rabbit => !rabbit.IsWarrenMember);
        }

        private void Possess(Rabbit rabbit)
        {
            if (possessedRabbit != null) possessedRabbit.Died -= OnPossessedRabbitDied;

            possessedRabbit = rabbit;
            possessedRabbit.Died += OnPossessedRabbitDied;
            lastPlayerPosition = rabbit.Position;

            playerController.Possess(rabbit, playerLifespanMultiplier);
            followCamera.SetTarget(rabbit.transform);
            Rabbit.WarrenLeader = rabbit;
        }

        // ====================================================================
        //  LINEAGE  (the player's rabbit died)
        // ====================================================================
        private void OnPossessedRabbitDied(SimEntity entity, string cause)
        {
            entity.Died -= OnPossessedRabbitDied;
            lastPlayerPosition = entity.Position;
            possessedRabbit = null;
            playerController.Release();
            LastDeathCause = cause;

            if (State == GameState.Playing || State == GameState.Paused)
                StartCoroutine(LineageTransferRoutine(cause));
        }

        /// <summary>COROUTINE: short pause on the death, then control passes to the heir.</summary>
        private IEnumerator LineageTransferRoutine(string cause)
        {
            IsTransferringLineage = true;
            Notify($"Your rabbit died ({cause}).");

            yield return new WaitForSeconds(lineageTransferDelay);   // scaled: freezes while paused

            IsTransferringLineage = false;
            if (State != GameState.Playing && State != GameState.Paused) yield break;

            Rabbit heir = FindHeir();
            if (heir == null)
            {
                Notify("No rabbit of your warren is left...");
                EndRun(false);
                yield break;
            }

            currentRun.generations++;
            Possess(heir);
            Notify($"Your lineage continues: generation {currentRun.generations}.");
        }

        /// <summary>Best warren rabbit to continue: adults first, then the most energy, then the closest.</summary>
        private Rabbit FindHeir()
        {
            return SimUtils.FindBest<Rabbit>(simulation.Entities, lastPlayerPosition, 1000f,
                rabbit => (rabbit.IsAdult ? 0f : 1000f) - rabbit.Energy01 * 100f
                          + SimUtils.FlatSqrDistance(lastPlayerPosition, rabbit.Position) * 0.01f,
                rabbit => rabbit.IsWarrenMember);
        }

        // ====================================================================
        //  WARREN & PROGRESSION
        // ====================================================================
        private void TickWarren(float dt)
        {
            if (possessedRabbit != null) lastPlayerPosition = possessedRabbit.Position;

            warrenScanTimer -= dt;
            if (warrenScanTimer > 0f) return;
            warrenScanTimer = WarrenScanInterval;
            RefreshWarren();
        }

        private void RefreshWarren()
        {
            int count = 0;
            foreach (SimEntity entity in simulation.Entities)
            {
                if (entity is Rabbit rabbit && rabbit.IsWarrenMember) count++;
            }
            WarrenSize = count;
            currentRun.peakWarren = Mathf.Max(currentRun.peakWarren, count);

            foreach (AbilityUnlock unlock in progression.unlocks)
            {
                if (count >= unlock.warrenSizeRequired) playerAbilities.Unlock(unlock.ability);
            }

            // Safety net: nobody left and no transfer pending.
            if (count == 0 && possessedRabbit == null && !IsTransferringLineage) EndRun(false);
        }

        private void OnRabbitJoinedWarren(Rabbit rabbit)
        {
            if (State != GameState.Playing) return;
            currentRun.babiesBorn++;
        }

        private void OnAbilityUnlocked(AbilityType ability)
        {
            GameKey? key = PlayerAbilities.GetKey(ability);
            string how = key.HasValue ? $"press {InputHelper.GetKeyLabel(key.Value)}" : "always active";
            Notify($"New ability: {PlayerAbilities.GetDisplayName(ability)} ({how})!");
        }

        // ====================================================================
        //  DAY / NIGHT  (difficulty curve)
        // ====================================================================
        private void OnDayStarted(int day)
        {
            if (State != GameState.Playing && State != GameState.Paused) return;

            if (day > daysToSurvive)
            {
                EndRun(true);
                return;
            }

            currentRun.daysSurvived = day - 1;
            ApplyDifficulty(day, false);
            Notify(day == 1
                ? "Day 1: eat (E), find a mate (F), hide in burrows. Survive 7 nights."
                : $"Day {day} of {daysToSurvive}. The sun rises: forage while it is safer.");
        }

        private void OnNightStarted(int day)
        {
            if (State != GameState.Playing && State != GameState.Paused) return;

            ApplyDifficulty(day, true);

            int newPredators = difficulty.GetPredatorsForNight(day);
            int arrived = 0;
            for (int i = 0; i < newPredators; i++)
            {
                Species species = i % 2 == 0 ? Species.Fox : Species.Wolf;   // alternate fox / wolf
                arrived += simulation.SpawnGroupAtArenaEdge(species, 1);
            }

            Notify(arrived > 0
                ? $"Night {day}: {arrived} new predator(s) roam. Predators see further at night!"
                : $"Night {day}: predators see further at night. Stay near a burrow!");
        }

        private void ApplyDifficulty(int day, bool isNight)
        {
            simulation.PredatorSenseMultiplier = difficulty.GetSenseMultiplier(day, isNight);
            simulation.PredatorSpeedMultiplier = difficulty.GetSpeedMultiplier(day);
        }

        // ====================================================================
        //  MESSAGES
        // ====================================================================
        public void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message)) Notification?.Invoke(message);
        }
    }
}
