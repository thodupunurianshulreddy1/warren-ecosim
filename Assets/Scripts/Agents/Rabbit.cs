// ============================================================================
//  Rabbit.cs   (Herbivore -> Animal -> SimEntity)
//  Small, fast-breeding prey. [H4] Also the PLAYABLE species.
//  Overrides:
//   - CreateDefaultStats      : fast, short life, quick maturity
//   - GetLitterSize           : random litter of 1 to maxLitterSize babies
//   - GetFleeDirection        : [H4] run to a nearby burrow, otherwise zig-zag
//   - GetVisualBobOffset      : hopping animation, [H4] sinks when hidden
//   - IsHidden                : [H4] true inside a burrow
//   - OnOffspringBorn         : [H4] babies of a warren member join the warren
//   - ChooseWanderDestination : [H4] warren members stay around their leader
// ============================================================================

using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Habitat;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.Agents
{
    public class Rabbit : Herbivore
    {
        [Header("Rabbit specifics")]
        [Tooltip("Maximum number of babies per litter (random between 1 and this).")]
        [SerializeField] private int maxLitterSize = 3;
        [Tooltip("Amplitude (degrees) of the zig-zag while fleeing.")]
        [SerializeField] private float zigZagAngle = 35f;
        [Tooltip("Speed of the zig-zag oscillation.")]
        [SerializeField] private float zigZagFrequency = 6f;
        [Tooltip("Height of a hop.")]
        [SerializeField] private float hopHeight = 0.25f;
        [Tooltip("Hops per second.")]
        [SerializeField] private float hopFrequency = 2.5f;

        [Header("[H4] Burrows & warren")]
        [Tooltip("A fleeing rabbit runs into a burrow closer than this (if the burrow is not behind the predator).")]
        [SerializeField] private float burrowSeekRadius = 10f;
        [Tooltip("How far the body sinks into the ground while hidden in a burrow.")]
        [SerializeField] private float hiddenSinkDepth = -0.3f;
        [Tooltip("Warren members wander around their leader while closer than this.")]
        [SerializeField] private float followLeaderRadius = 18f;
        [Tooltip("Radius of the wander points chosen around the leader.")]
        [SerializeField] private float leaderWanderRadius = 4f;
        [Tooltip("Gold tag on the back of warren members (auto-found as 'Visual/WarrenTag').")]
        [SerializeField] private GameObject warrenTag;

        private bool isWarrenMember;
        private float zigZagPhase;   // random per rabbit, so they don't all zig-zag in sync

        /// <summary>
        /// [H4] The rabbit the warren follows (the player's rabbit). Set by the GameManager;
        /// static because there is one warren per game and the agents should not depend on the game layer.
        /// </summary>
        public static Animal WarrenLeader { get; set; }

        /// <summary>[H4] Raised when any rabbit joins the warren (founder or newborn).</summary>
        public static event System.Action<Rabbit> JoinedWarren;

        public override Species Species => Species.Rabbit;
        public bool IsWarrenMember => isWarrenMember;

        /// <summary>[H4] A rabbit inside a burrow is hidden: predators ignore it.</summary>
        public override bool IsHidden => isAlive && Burrow.FindContaining(Position) != null;

        protected override AnimalStats CreateDefaultStats()
        {
            // Object initializer: start from the defaults, change only what differs.
            return new AnimalStats
            {
                walkSpeed = 2.2f,
                runSpeedMultiplier = 2.2f,
                senseRadius = 7f,
                maxEnergy = 80f,
                energyDrainPerSecond = 2.2f,
                meatValue = 45f,
                maturityAge = 12f,
                maxAge = 90f,
                reproductionCooldown = 14f,
                reproductionCost = 0.3f,
                newbornScale = 0.45f
            };
        }

        protected override void Awake()
        {
            base.Awake();
            zigZagPhase = Random.Range(0f, Mathf.PI * 2f);   // [H4] replaces GetInstanceID() (obsolete in Unity 6.6+)
            if (warrenTag == null)
            {
                Transform tagTransform = transform.Find("Visual/WarrenTag");
                if (tagTransform != null) warrenTag = tagTransform.gameObject;
            }
            if (warrenTag != null) warrenTag.SetActive(false);
        }

        // --------------------------------------------------------------------
        //  [H4] Warren
        // --------------------------------------------------------------------

        /// <summary>Makes this rabbit part of the player's warren (shows the gold tag).</summary>
        public void JoinWarren()
        {
            if (isWarrenMember || !isAlive) return;
            isWarrenMember = true;
            if (warrenTag != null) warrenTag.SetActive(true);
            JoinedWarren?.Invoke(this);
        }

        /// <summary>Warren membership is inherited from EITHER parent.</summary>
        protected override void OnOffspringBorn(Animal baby, Animal partner)
        {
            base.OnOffspringBorn(baby, partner);

            bool partnerInWarren = partner is Rabbit rabbitPartner && rabbitPartner.IsWarrenMember;
            if ((isWarrenMember || partnerInWarren) && baby is Rabbit babyRabbit)
                babyRabbit.JoinWarren();
        }

        /// <summary>Warren members stay around their leader; wild rabbits roam freely.</summary>
        protected override Vector3 ChooseWanderDestination()
        {
            Animal leader = WarrenLeader;
            bool followsLeader = isWarrenMember && leader != null && leader != this && leader.IsAlive
                                 && SimUtils.IsWithinRange(Position, leader.Position, followLeaderRadius);

            return followsLeader
                ? SimUtils.RandomPointNear(leader.Position, leaderWanderRadius)
                : base.ChooseWanderDestination();
        }

        // --------------------------------------------------------------------
        //  Overrides
        // --------------------------------------------------------------------

        /// <summary>Rabbits have litters of random size.</summary>
        protected override int GetLitterSize()
        {
            // Random.Range with ints: max is EXCLUSIVE, hence the +1.
            return Random.Range(1, maxLitterSize + 1);
        }

        /// <summary>
        /// [H4] First choice: a nearby burrow that is not behind the predator.
        /// Otherwise: the normal escape direction, oscillating left/right (zig-zag).
        /// </summary>
        protected override Vector3 GetFleeDirection(SimEntity threat)
        {
            Burrow refuge = Burrow.FindClosest(Position, burrowSeekRadius);
            if (refuge != null)
            {
                if (refuge.Contains(Position)) return Vector3.zero;             // safe: stay still

                Vector3 toBurrow = SimUtils.FlatDirection(Position, refuge.Position);
                Vector3 awayFromThreat = SimUtils.FlatDirection(threat.Position, Position);
                bool burrowIsNotBehindThreat = Vector3.Dot(toBurrow, awayFromThreat) > -0.3f;
                if (burrowIsNotBehindThreat) return toBurrow;
            }

            Vector3 baseDirection = base.GetFleeDirection(threat);          // reuse parent logic
            float angle = Mathf.Sin(Time.time * zigZagFrequency + zigZagPhase) * zigZagAngle;
            return SimUtils.RotateFlat(baseDirection, angle);
        }

        /// <summary>Hop only while actually moving; sink into the ground while hidden.</summary>
        protected override float GetVisualBobOffset()
        {
            if (IsHidden) return hiddenSinkDepth;

            bool isMoving = !isBusy && state != AgentState.Dead && (!isPlayerControlled || IsManuallyMoving);
            if (!isMoving) return 0f;

            float frequency = state == AgentState.Fleeing ? hopFrequency * 2f : hopFrequency;
            return Mathf.Abs(Mathf.Sin(Time.time * frequency * Mathf.PI)) * hopHeight;
        }
    }
}
