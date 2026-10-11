// ============================================================================
//  DayNightCycle.cs   [H4]
//  The slow "time loop" layered on top of the core loop (see the GDD):
//  day = safe-ish foraging, night = predators see further and new ones arrive.
//
//  - Counts days and switches Day <-> Night on timers
//  - Nights get longer every day (difficulty curve)
//  - Raises C# events (DayStarted, NightStarted) instead of calling the game:
//    the habitat does not know the GameManager exists (loose coupling)
//  - Drives the sun, ambient light and sky colour so the player FEELS the night
// ============================================================================

using System;
using UnityEngine;

namespace ThodupunuriAnshul.EcoSim.Habitat
{
    public class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The scene's directional light.")]
        [SerializeField] private Light sun;
        [Tooltip("Camera whose background colour is the sky.")]
        [SerializeField] private Camera skyCamera;

        [Header("Durations (seconds)")]
        [SerializeField] private float dayDuration = 60f;
        [SerializeField] private float firstNightDuration = 25f;
        [Tooltip("Each night lasts this much longer than the previous one.")]
        [SerializeField] private float nightGrowthPerDay = 5f;
        [Tooltip("Fraction of the day used for sunrise / sunset light transitions.")]
        [Range(0.05f, 0.4f)] [SerializeField] private float twilightFraction = 0.15f;

        [Header("Day look")]
        [SerializeField] private float dayIntensity = 1.1f;
        [SerializeField] private Color daySunColor = new Color(1f, 0.96f, 0.88f);
        [SerializeField] private Color dayAmbient = new Color(0.55f, 0.58f, 0.62f);
        [SerializeField] private Color daySky = new Color(0.55f, 0.75f, 0.95f);

        [Header("Night look")]
        [SerializeField] private float nightIntensity = 0.18f;
        [SerializeField] private Color nightSunColor = new Color(0.55f, 0.65f, 1f);
        [SerializeField] private Color nightAmbient = new Color(0.12f, 0.14f, 0.24f);
        [SerializeField] private Color nightSky = new Color(0.04f, 0.05f, 0.12f);

        private float phaseTimer;
        private bool isRunning;

        // --------------------------------------------------------------------
        //  Public API
        // --------------------------------------------------------------------
        public int Day { get; private set; } = 1;
        public bool IsNight { get; private set; }
        public bool IsRunning => isRunning;
        public float CurrentPhaseDuration => IsNight ? GetNightDuration(Day) : dayDuration;
        public float PhaseProgress01 => Mathf.Clamp01(phaseTimer / CurrentPhaseDuration);
        public float TimeLeftInPhase => Mathf.Max(0f, CurrentPhaseDuration - phaseTimer);

        /// <summary>Raised with the new day number (Day 1 is raised by StartCycle).</summary>
        public event Action<int> DayStarted;
        /// <summary>Raised with the number of the day this night belongs to.</summary>
        public event Action<int> NightStarted;

        public float GetNightDuration(int day)
        {
            return firstNightDuration + nightGrowthPerDay * (day - 1);
        }

        public void StartCycle()
        {
            Day = 1;
            IsNight = false;
            phaseTimer = 0f;
            isRunning = true;
            ApplyLighting();
            DayStarted?.Invoke(Day);
        }

        public void StopCycle()
        {
            isRunning = false;
        }

        // --------------------------------------------------------------------
        //  Loop
        // --------------------------------------------------------------------
        private void Start()
        {
            ApplyLighting();   // show the day look before the run starts (title screen)
        }

        private void Update()
        {
            if (!isRunning) return;

            phaseTimer += Time.deltaTime;
            if (phaseTimer >= CurrentPhaseDuration) AdvancePhase();

            ApplyLighting();
        }

        private void AdvancePhase()
        {
            phaseTimer = 0f;
            if (IsNight)
            {
                IsNight = false;
                Day++;
                DayStarted?.Invoke(Day);
            }
            else
            {
                IsNight = true;
                NightStarted?.Invoke(Day);
            }
        }

        // --------------------------------------------------------------------
        //  Lighting
        // --------------------------------------------------------------------

        /// <summary>
        /// 0 at night, 1 in full day, with smooth sunrise / sunset ramps at both ends
        /// of the day (so there is no visual jump when the phase switches).
        /// </summary>
        private float GetDaylight01()
        {
            if (!isRunning && Day == 1 && !IsNight) return 1f;   // title screen: plain day
            if (IsNight) return 0f;

            float t = PhaseProgress01;
            float sunrise = Mathf.SmoothStep(0f, 1f, t / twilightFraction);
            float sunset = Mathf.SmoothStep(0f, 1f, (1f - t) / twilightFraction);
            return Mathf.Min(sunrise, sunset);
        }

        private void ApplyLighting()
        {
            float daylight = GetDaylight01();

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, daylight);
                sun.color = Color.Lerp(nightSunColor, daySunColor, daylight);
                // Low sun at dawn/dusk (long shadows), high at noon.
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(20f, 55f, daylight), -30f, 0f);
            }

            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight);
            if (skyCamera != null) skyCamera.backgroundColor = Color.Lerp(nightSky, daySky, daylight);
        }
    }
}
