// ============================================================================
//  Animal.cs
//  Abstract base class of every MOVING agent.
//
//  Responsibilities:
//   - Energy, ageing, natural death           (Update)
//   - Decision making (priority-based FSM)    (DecideNextAction - virtual)
//   - Executing the current state             (ExecuteCurrentState - switch)
//   - Movement with Transform / Vector3 / Quaternion
//   - Timed "busy" actions with COROUTINES    (eat, mate, rest...)
//   - Visual feedback (status light colour = state, size = energy)
//   - [H4] POSSESSION: any animal can be driven by the player instead of
//     its AI. The player layer only sends commands (SetMoveInput,
//     TryEatNearby, TryMateNearby...). The animal keeps its own rules
//     (energy, speed, coroutines), so the player plays BY the simulation.
//
//  What children MUST provide (abstract):
//   - CreateDefaultStats()  : the species tuning
//   - FindFood(range)       : what this animal eats  ([H4] was SenseFood())
//   - FoodSeekingState      : SeekingFood (herbivore) or Hunting (carnivore)
//   - Species               : (from SimEntity)
//
//  What children CAN customize (virtual): SenseFood, SenseThreat, SenseMate,
//  GetPursuitSpeed, GetFleeDirection, ChooseWanderDestination,
//  GetLitterSize, OnOffspringBorn, GetVisualBobOffset, OnStateChanged,
//  ShouldInterruptBusyAction...
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public abstract class Animal : SimEntity
    {
        // --------------------------------------------------------------------
        //  Inspector fields
        // --------------------------------------------------------------------
        [Header("Species tuning")]
        [SerializeField] protected AnimalStats stats = new AnimalStats();

        [Header("Visual references (auto-found by name if left empty)")]
        [Tooltip("Child holding the body meshes (named 'Visual'). Moved for hop / death effects.")]
        [SerializeField] protected Transform visualRoot;
        [Tooltip("Small sphere above the animal (named 'StatusIndicator'). Colour = state, size = energy.")]
        [SerializeField] protected Renderer statusIndicator;

        // --------------------------------------------------------------------
        //  Runtime state
        // --------------------------------------------------------------------
        protected AgentState state = AgentState.Wandering;
        protected float energy;
        protected SimEntity currentTarget;        // food, threat or mate depending on state
        protected Vector3 wanderDestination;
        protected float stateTimer;               // seconds spent in the current state
        protected bool isBusy;                    // true while a timed coroutine action runs
        protected bool isOnReproductionCooldown;

        // [H4] Player control (possession)
        protected bool isPlayerControlled;
        private Vector3 manualMoveDirection;      // last direction sent by the player layer
        private bool manualSprint;
        private float manualSpeedMultiplier = 1f;
        private float lifespanMultiplier = 1f;    // the possessed animal lives longer

        private float decisionTimer;
        private float wanderTimer;
        private Coroutine busyRoutine;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 indicatorBaseScale;
        private AgentState lastDisplayedState = AgentState.Dead;   // forces a first refresh

        private const float WanderDestinationTolerance = 0.5f;
        private const float WanderTimeout = 8f;
        private const float ArenaEdgeMargin = 2f;
        private const float MinMoveInputSqr = 0.01f;

        // --------------------------------------------------------------------
        //  Public read-only API (used by other agents, the player layer & the HUD)
        // --------------------------------------------------------------------
        public AgentState State => state;
        public float Energy01 => energy / stats.maxEnergy;
        public bool IsHungry => Energy01 < stats.hungerThreshold;
        public bool IsAdult => age >= stats.maturityAge;
        public bool IsBusy => isBusy;                                   // [H4]
        public SimEntity CurrentTarget => currentTarget;                // [H4] HUD: "who is chasing me?"
        public bool IsPlayerControlled => isPlayerControlled;          // [H4]
        public float MaxAge => stats.maxAge * lifespanMultiplier;       // [H4]
        public float Age01 => Mathf.Clamp01(age / MaxAge);             // [H4]

        /// <summary>
        /// [H4] Every biological requirement to mate (refactored out of IsReadyToMate
        /// so that the player and the AI share exactly the same rule).
        /// </summary>
        protected bool MeetsMatingRequirements =>
            isAlive && !isBusy && IsAdult && !IsHungry && !isOnReproductionCooldown
            && Energy01 >= stats.reproductionEnergyThreshold;

        /// <summary>
        /// Combined condition: every requirement must be true to accept a partner.
        /// [H4] A possessed animal is never picked by the AI: the player decides when to mate.
        /// </summary>
        public bool IsReadyToMate => MeetsMatingRequirements && !isPlayerControlled;

        /// <summary>[H4] Same rule, seen from the player's side (used for the HUD hint).</summary>
        public bool CanPlayerMate => MeetsMatingRequirements;

        /// <summary>Shortcut to the list of every entity in the world.</summary>
        protected IReadOnlyList<SimEntity> World => SimulationManager.Instance.Entities;
        protected float ArenaHalfSize => SimulationManager.Instance.ArenaHalfSize;

        /// <summary>[H4] True while the player is pushing the movement keys.</summary>
        protected bool IsManuallyMoving => isPlayerControlled && manualMoveDirection.sqrMagnitude > MinMoveInputSqr;

        // --------------------------------------------------------------------
        //  Abstract members (each subclass MUST implement them)
        // --------------------------------------------------------------------
        protected abstract AnimalStats CreateDefaultStats();

        /// <summary>[H4] Best food within 'range' (was SenseFood(): the range is now a parameter
        /// so the same query serves the AI senses AND the player's short "reach").</summary>
        protected abstract SimEntity FindFood(float range);

        protected abstract AgentState FoodSeekingState { get; }

        // ====================================================================
        //  SETUP
        // ====================================================================

        /// <summary>Called by the editor builder (and by Reset) to load the species defaults.</summary>
        public void ApplySpeciesDefaults()
        {
            stats = CreateDefaultStats();
        }

        /// <summary>Unity calls Reset() when the component is first added in the editor.</summary>
        protected virtual void Reset()
        {
            ApplySpeciesDefaults();
        }

        protected override void Awake()
        {
            base.Awake();
            propertyBlock = new MaterialPropertyBlock();

            // Fallback: find visual children by name if not assigned in the prefab.
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (statusIndicator == null)
            {
                Transform indicator = transform.Find("StatusIndicator");
                if (indicator != null) statusIndicator = indicator.GetComponent<Renderer>();
            }
            if (statusIndicator != null) indicatorBaseScale = statusIndicator.transform.localScale;

            energy = stats.maxEnergy * stats.startEnergyFraction;
            decisionTimer = Random.Range(0f, stats.decisionInterval);   // de-synchronize agents
            wanderDestination = Position;
            transform.rotation = SimUtils.RandomYRotation();
        }

        /// <summary>
        /// Used by the manager for the initial population: the animal becomes
        /// an adult of random age with random energy, so they don't all die together.
        /// </summary>
        public void RandomizeAsAdult()
        {
            age = Random.Range(stats.maturityAge, stats.maxAge * 0.5f);
            energy = stats.maxEnergy * Random.Range(0.6f, 0.9f);
            UpdateGrowthScale();
        }

        // ====================================================================
        //  MAIN LOOP
        // ====================================================================
        protected override void Update()
        {
            base.Update();                    // ageing (SimEntity)
            if (!isAlive) return;

            float dt = Time.deltaTime;
            stateTimer += dt;

            UpdateGrowthScale();
            ConsumeEnergy(dt);
            if (CheckNaturalDeath()) return;

            // Decisions are taken on a timer, movement happens every frame.
            decisionTimer -= dt;
            bool thinkNow = decisionTimer <= 0f;
            if (thinkNow) decisionTimer = stats.decisionInterval;

            if (isBusy)
            {
                // A busy action (eating, mating...) can be cancelled by danger.
                if (thinkNow && ShouldInterruptBusyAction()) InterruptBusyAction();
            }

            if (!isBusy)
            {
                // [H4] Same body, two possible "brains": the player or the AI.
                if (isPlayerControlled)
                {
                    ExecutePlayerControl();
                }
                else
                {
                    if (thinkNow) DecideNextAction();
                    ExecuteCurrentState();
                }
            }

            UpdateVisuals();
        }

        private void ConsumeEnergy(float dt)
        {
            float drain = stats.energyDrainPerSecond;

            // Ternary + switch-like logic: running costs more, resting costs less.
            bool isRunning = state == AgentState.Fleeing || state == AgentState.Hunting;
            drain *= isRunning ? stats.runEnergyMultiplier : 1f;
            if (isBusy) drain *= 0.5f;

            energy -= drain * dt;
        }

        /// <summary>Returns true if the animal died this frame.</summary>
        private bool CheckNaturalDeath()
        {
            if (energy <= 0f)
            {
                Die("starvation");
                return true;
            }
            if (age >= MaxAge)
            {
                Die("old age");
                return true;
            }
            return false;
        }

        // ====================================================================
        //  DECISION MAKING  (priority list: survive > eat > reproduce > wander)
        // ====================================================================
        protected virtual void DecideNextAction()
        {
            // Priority 1 - survival: run away from any visible threat.
            SimEntity threat = SenseThreat();
            if (threat != null)
            {
                ChangeState(AgentState.Fleeing, threat);
                return;
            }

            // Priority 2 - hunger: go for the food we can sense.
            if (IsHungry)
            {
                SimEntity food = SenseFood();
                if (food != null)
                {
                    ChangeState(FoodSeekingState, food);
                    return;
                }
            }

            // Priority 3 - reproduction: only when healthy, adult and not on cooldown.
            if (IsReadyToMate)
            {
                Animal mate = SenseMate();
                if (mate != null)
                {
                    ChangeState(AgentState.SeekingMate, mate);
                    return;
                }
            }

            // Default - nothing to do: wander around.
            if (state != AgentState.Wandering) ChangeState(AgentState.Wandering, null);
        }

        /// <summary>Food the animal can see. Carnivores override it to apply the night bonus.</summary>
        protected virtual SimEntity SenseFood()
        {
            return FindFood(stats.senseRadius);
        }

        /// <summary>By default animals fear nothing. Herbivores override this.</summary>
        protected virtual SimEntity SenseThreat()
        {
            return null;
        }

        /// <summary>Closest adult of the SAME species that is also ready to mate.</summary>
        protected virtual Animal SenseMate()
        {
            return FindMate(stats.senseRadius);
        }

        /// <summary>[H4] Shared mate query (AI senses and player reach).</summary>
        protected Animal FindMate(float range)
        {
            return SimUtils.FindClosest<Animal>(World, Position, range,
                other => other != this && other.Species == Species && other.IsReadyToMate);
        }

        /// <summary>Single entry point for every state change.</summary>
        protected void ChangeState(AgentState newState, SimEntity target = null)
        {
            AgentState previous = state;
            currentTarget = target;

            if (newState == previous) return;     // same state: only the target may have changed

            state = newState;
            stateTimer = 0f;
            OnStateChanged(previous, newState);
        }

        /// <summary>Hook for subclasses (e.g. wolves call their pack, deer alert the herd).</summary>
        protected virtual void OnStateChanged(AgentState previous, AgentState next) { }

        // ====================================================================
        //  STATE EXECUTION  (called every frame while not busy)
        // ====================================================================
        protected virtual void ExecuteCurrentState()
        {
            switch (state)
            {
                case AgentState.Wandering:
                    Wander();
                    break;

                case AgentState.SeekingFood:
                case AgentState.Hunting:
                    PursueFood();
                    break;

                case AgentState.Fleeing:
                    Flee();
                    break;

                case AgentState.SeekingMate:
                    PursueMate();
                    break;

                default:
                    // Eating / Mating / Resting are handled by coroutines.
                    break;
            }
        }

        private void Wander()
        {
            wanderTimer += Time.deltaTime;
            bool reached = SimUtils.IsWithinRange(Position, wanderDestination, WanderDestinationTolerance);

            if (reached || wanderTimer > WanderTimeout)
            {
                wanderDestination = SimUtils.ClampToArena(ChooseWanderDestination(), ArenaHalfSize - 0.5f);
                wanderTimer = 0f;
            }

            MoveTowards(wanderDestination, stats.walkSpeed * 0.6f);   // relaxed pace
        }

        /// <summary>Random point around the animal. Deer override this to stay in a herd.</summary>
        protected virtual Vector3 ChooseWanderDestination()
        {
            return SimUtils.RandomPointNear(Position, stats.wanderRadius);
        }

        private void PursueFood()
        {
            // "as" cast: is the target still something edible?
            IEdible food = currentTarget as IEdible;
            if (currentTarget == null || !currentTarget.IsAlive || food == null || !food.CanBeEaten)
            {
                ChangeState(AgentState.Wandering);
                return;
            }

            float distance = Vector3.Distance(Position.Flattened(), currentTarget.Position.Flattened());
            MoveTowards(currentTarget.Position, GetPursuitSpeed(distance));

            if (distance <= stats.contactRange)
                StartBusyAction(EatRoutine(food));
        }

        /// <summary>Speed used to reach food. Carnivores run, foxes stalk then pounce.</summary>
        protected virtual float GetPursuitSpeed(float distanceToTarget)
        {
            return stats.walkSpeed;
        }

        private void Flee()
        {
            if (currentTarget == null || !currentTarget.IsAlive)
            {
                ChangeState(AgentState.Wandering);
                return;
            }
            MoveInDirection(GetFleeDirection(currentTarget), stats.walkSpeed * stats.runSpeedMultiplier);
        }

        /// <summary>Direction to run away from a threat. Rabbits override it to zig-zag.</summary>
        protected virtual Vector3 GetFleeDirection(SimEntity threat)
        {
            Vector3 away = SimUtils.FlatDirection(threat.Position, Position);

            // Cornered against a wall? Blend toward the arena centre to slide along it.
            if (SimUtils.IsNearArenaEdge(Position, ArenaHalfSize, ArenaEdgeMargin))
                away = (away + SimUtils.FlatDirection(Position, Vector3.zero)).normalized;

            return away;
        }

        private void PursueMate()
        {
            Animal mate = currentTarget as Animal;
            if (mate == null || !mate.IsReadyToMate || !IsReadyToMate)
            {
                ChangeState(AgentState.Wandering);
                return;
            }

            MoveTowards(mate.Position, stats.walkSpeed);

            if (SimUtils.IsWithinRange(Position, mate.Position, stats.contactRange * 1.5f))
            {
                mate.JoinMating(this);                               // partner becomes busy too
                StartBusyAction(MatingRoutine(mate, true));          // only the initiator spawns babies
            }
        }

        /// <summary>Called by the partner that initiated the mating.</summary>
        public void JoinMating(Animal initiator)
        {
            StartBusyAction(MatingRoutine(initiator, false));
        }

        // ====================================================================
        //  [H4] PLAYER CONTROL  (commands sent by PlayerController)
        // ====================================================================

        /// <summary>
        /// Switches this animal between AI and player control.
        /// 'lifespan' multiplies the max age while possessed (the hero lives longer).
        /// </summary>
        public void SetPlayerControlled(bool controlled, float lifespan = 1f)
        {
            isPlayerControlled = controlled;
            lifespanMultiplier = controlled ? Mathf.Max(1f, lifespan) : 1f;
            manualMoveDirection = Vector3.zero;
            manualSprint = false;
            if (!isBusy) ChangeState(AgentState.Wandering);
        }

        /// <summary>
        /// Movement wish for this frame (world direction on the ground).
        /// The animal applies it with its OWN speed, turn rate and energy cost.
        /// </summary>
        public void SetMoveInput(Vector3 worldDirection, bool sprint, float speedMultiplier)
        {
            worldDirection.y = 0f;
            manualMoveDirection = worldDirection;
            manualSprint = sprint;
            manualSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
        }

        /// <summary>Eats the best food within 'reach'. Returns false if there is none.</summary>
        public bool TryEatNearby(float reach)
        {
            if (!isAlive || isBusy) return false;

            SimEntity target = FindFood(reach);
            IEdible food = target as IEdible;
            if (food == null || !food.CanBeEaten) return false;

            currentTarget = target;
            StartBusyAction(EatRoutine(food));
            return true;
        }

        /// <summary>Mates with a ready partner within 'reach'. Returns false if impossible.</summary>
        public bool TryMateNearby(float reach)
        {
            if (!MeetsMatingRequirements) return false;

            Animal mate = FindMate(reach);
            if (mate == null) return false;

            currentTarget = mate;
            mate.JoinMating(this);
            StartBusyAction(MatingRoutine(mate, true));
            return true;
        }

        /// <summary>Explains why mating is not possible right now (null = possible).</summary>
        public string GetMatingBlocker()
        {
            if (!IsAdult) return "too young";
            if (isOnReproductionCooldown) return "still recovering from the last litter";
            if (Energy01 < Mathf.Max(stats.hungerThreshold, stats.reproductionEnergyThreshold))
                return $"needs {Mathf.Max(stats.hungerThreshold, stats.reproductionEnergyThreshold).ToPercent()} energy";
            if (isBusy) return "busy";
            return null;
        }

        /// <summary>Lets the player abort a meal (the food already eaten is lost).</summary>
        public bool CancelBusyAction()
        {
            if (!isBusy || state != AgentState.Eating) return false;
            InterruptBusyAction();
            return true;
        }

        /// <summary>Removes a fraction of the max energy (cost of an ability).</summary>
        public void SpendEnergy(float fractionOfMax)
        {
            energy -= stats.maxEnergy * Mathf.Max(0f, fractionOfMax);
        }

        /// <summary>Walk or sprint along the direction sent by the player.</summary>
        private void ExecutePlayerControl()
        {
            bool isMoving = IsManuallyMoving;
            bool isSprinting = isMoving && manualSprint;

            // Re-using the existing states keeps the energy cost and the status light consistent:
            // sprinting costs as much as fleeing.
            AgentState movementState = isSprinting ? AgentState.Fleeing : AgentState.Wandering;
            if (state != movementState) ChangeState(movementState);

            if (!isMoving) return;

            float speed = stats.walkSpeed * manualSpeedMultiplier * (isSprinting ? stats.runSpeedMultiplier : 1f);
            MoveInDirection(manualMoveDirection.normalized, speed);
        }

        // ====================================================================
        //  MOVEMENT  (Transform, Vector3, Quaternion)
        // ====================================================================

        /// <summary>Moves toward a point without overshooting it.</summary>
        protected void MoveTowards(Vector3 destination, float speed)
        {
            Vector3 flatDestination = new Vector3(destination.x, Position.y, destination.z);
            Vector3 nextPosition = Vector3.MoveTowards(Position, flatDestination, speed * Time.deltaTime);
            ApplyPosition(nextPosition);
            FaceDirection(SimUtils.FlatDirection(Position, destination));
        }

        /// <summary>Moves along a direction (used for fleeing, no destination).</summary>
        protected void MoveInDirection(Vector3 direction, float speed)
        {
            ApplyPosition(Position + direction * (speed * Time.deltaTime));
            FaceDirection(direction);
        }

        private void ApplyPosition(Vector3 newPosition)
        {
            transform.position = SimUtils.ClampToArena(newPosition, ArenaHalfSize);
        }

        /// <summary>Smoothly turns the animal (limited by turnSpeed).</summary>
        protected void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                                                          stats.turnSpeed * Time.deltaTime);
        }

        // ====================================================================
        //  TIMED ACTIONS  (COROUTINES)
        // ====================================================================

        /// <summary>Starts a coroutine during which the animal stops thinking and moving.</summary>
        protected void StartBusyAction(IEnumerator routine)
        {
            if (isBusy || !isAlive) return;
            isBusy = true;
            busyRoutine = StartCoroutine(routine);
        }

        /// <summary>Must be called at the end of every busy coroutine.</summary>
        protected void EndBusyAction()
        {
            isBusy = false;
            busyRoutine = null;
            decisionTimer = 0f;                       // think again immediately
            ChangeState(AgentState.Wandering);
        }

        /// <summary>Cancels the running busy coroutine (e.g. a predator shows up).</summary>
        protected void InterruptBusyAction()
        {
            if (busyRoutine != null) StopCoroutine(busyRoutine);
            EndBusyAction();
        }

        /// <summary>By default nothing interrupts a busy action. Herbivores override it.</summary>
        protected virtual bool ShouldInterruptBusyAction()
        {
            return false;
        }

        /// <summary>COROUTINE: consume the food, then stay still while digesting.</summary>
        private IEnumerator EatRoutine(IEdible food)
        {
            ChangeState(AgentState.Eating, currentTarget);
            float energyGained = food.GetEaten(this);      // the food reacts (dies / is cut)

            yield return new WaitForSeconds(stats.eatDuration);

            energy = Mathf.Min(stats.maxEnergy, energy + energyGained);
            EndBusyAction();
        }

        /// <summary>COROUTINE: both parents stay together, then the initiator gives birth.</summary>
        private IEnumerator MatingRoutine(Animal partner, bool isInitiator)
        {
            ChangeState(AgentState.Mating, partner);
            FaceDirection(SimUtils.FlatDirection(Position, partner.Position));

            yield return new WaitForSeconds(stats.matingDuration);

            energy -= stats.maxEnergy * stats.reproductionCost;
            bool partnerStillThere = partner != null && partner.IsAlive;
            if (isInitiator && partnerStillThere) SpawnOffspring(partner);

            StartCoroutine(ReproductionCooldownRoutine());
            EndBusyAction();
        }

        /// <summary>COROUTINE: blocks reproduction for a while (runs in parallel to everything else).</summary>
        private IEnumerator ReproductionCooldownRoutine()
        {
            isOnReproductionCooldown = true;
            yield return new WaitForSeconds(stats.reproductionCooldown);
            isOnReproductionCooldown = false;
        }

        /// <summary>COROUTINE: stand still to recover (used by carnivores after a failed chase).</summary>
        protected IEnumerator RestRoutine(float duration)
        {
            ChangeState(AgentState.Resting);
            yield return new WaitForSeconds(duration);
            EndBusyAction();
        }

        // ====================================================================
        //  REPRODUCTION
        // ====================================================================
        private void SpawnOffspring(Animal partner)
        {
            int litterSize = GetLitterSize();
            for (int i = 0; i < litterSize; i++)
            {
                Vector3 birthPosition = SimUtils.ClampToArena(SimUtils.RandomPointNear(Position, 1f), ArenaHalfSize);
                SimEntity spawned = SimulationManager.Instance.Spawn(Species, birthPosition);
                if (spawned is Animal baby) OnOffspringBorn(baby, partner);
            }
        }

        /// <summary>How many babies per mating. Rabbits override with a random litter.</summary>
        protected virtual int GetLitterSize()
        {
            return 1;
        }

        /// <summary>[H4] Hook called for each baby (rabbits pass on warren membership).</summary>
        protected virtual void OnOffspringBorn(Animal baby, Animal partner) { }

        // ====================================================================
        //  DEATH
        // ====================================================================
        public override void Die(string cause)
        {
            if (!isAlive) return;

            if (busyRoutine != null) StopCoroutine(busyRoutine);
            isBusy = false;
            state = AgentState.Dead;
            UpdateVisuals();

            // Visual cue: the body falls on its side before shrinking.
            if (visualRoot != null) visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);

            base.Die(cause);   // SimEntity handles unregistering + shrink coroutine
        }

        // ====================================================================
        //  VISUALS
        // ====================================================================

        /// <summary>Babies grow linearly from newbornScale to full size at maturity.</summary>
        private void UpdateGrowthScale()
        {
            float growth = Mathf.Clamp01(age / stats.maturityAge);
            transform.localScale = baseScale * Mathf.Lerp(stats.newbornScale, 1f, growth);
        }

        private void UpdateVisuals()
        {
            // Status light colour: only refreshed when the state actually changes.
            if (statusIndicator != null && state != lastDisplayedState)
            {
                SimUtils.SetRendererColor(statusIndicator, SimUtils.GetStateColor(state), propertyBlock);
                lastDisplayedState = state;
            }

            // Status light size: proportional to energy (a quick "health bar").
            if (statusIndicator != null)
                statusIndicator.transform.localScale = indicatorBaseScale * Mathf.Lerp(0.5f, 1.3f, Energy01);

            // Optional vertical bob (rabbits hop).
            if (visualRoot != null && isAlive)
                visualRoot.localPosition = new Vector3(0f, GetVisualBobOffset(), 0f);
        }

        /// <summary>Vertical offset of the body. Overridden by Rabbit to make it hop.</summary>
        protected virtual float GetVisualBobOffset()
        {
            return 0f;
        }

        public override string GetStatusText()
        {
            return $"{base.GetStatusText()} - {state} - energy {Energy01.ToPercent()}";
        }

        /// <summary>Editor-only debug drawing: sense radius + line to the current target.</summary>
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, stats.senseRadius);

            if (currentTarget != null)
            {
                Gizmos.color = SimUtils.GetStateColor(state);
                Gizmos.DrawLine(transform.position + Vector3.up, currentTarget.Position + Vector3.up);
            }
        }
    }
}
