// ============================================================================
//  PopulationHUD.cs
//  On-screen information drawn with Unity's immediate-mode GUI (OnGUI),
//  so no Canvas / UI package / external asset is needed.
//   - Left panel  : time, speed buttons, population per species, mini graph
//   - Right panel : colour legend of the status lights, main causes of death
// ============================================================================

using System.Linq;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.UI
{
    public class PopulationHUD : MonoBehaviour
    {
        [SerializeField] private bool showLegend = true;
        [SerializeField] private bool showGraph = true;
        [SerializeField] private int deathCausesShown = 6;

        private const float PanelWidth = 280f;
        private const float GraphHeight = 90f;

        private static readonly float[] SpeedPresets = { 0f, 1f, 2f, 4f };

        private void OnGUI()
        {
            SimulationManager manager = SimulationManager.Instance;
            if (manager == null) return;

            DrawLeftPanel(manager);
            if (showLegend) DrawRightPanel(manager);
        }

        // --------------------------------------------------------------------
        private void DrawLeftPanel(SimulationManager manager)
        {
            GUILayout.BeginArea(new Rect(10, 10, PanelWidth, Screen.height - 20), GUI.skin.box);

            GUILayout.Label($"<b>EcoSim - Predator & Prey</b>   t = {manager.ElapsedTime:0}s", RichLabel());

            // Speed buttons (loop over an array instead of copy-pasting 4 buttons).
            GUILayout.BeginHorizontal();
            foreach (float speed in SpeedPresets)
            {
                string label = speed == 0f ? "Pause" : $"x{speed}";
                bool isCurrent = Mathf.Approximately(Time.timeScale, speed);
                GUI.enabled = !isCurrent;
                if (GUILayout.Button(label)) manager.SetSimulationSpeed(speed);
                GUI.enabled = true;
            }
            if (GUILayout.Button("Restart")) manager.RestartSimulation();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            foreach (Species species in SimulationManager.AllSpecies)
                DrawColoredRow(SimUtils.GetSpeciesColor(species), $"{species}: {manager.CountOf(species)}");

            GUILayout.Label($"Births: {manager.TotalBirths}    Deaths: {manager.TotalDeaths}");

            if (showGraph)
            {
                GUILayout.Space(4);
                GUILayout.Label("Population trend (each species normalized):");
                Rect graphRect = GUILayoutUtility.GetRect(PanelWidth - 20, GraphHeight);
                DrawGraph(graphRect, manager);
            }

            GUILayout.Space(4);
            GUILayout.Label("<i>WASD move  -  Q/E orbit  -  wheel zoom</i>", RichLabel());
            GUILayout.EndArea();
        }

        private void DrawRightPanel(SimulationManager manager)
        {
            GUILayout.BeginArea(new Rect(Screen.width - PanelWidth - 10, 10, PanelWidth, Screen.height - 20), GUI.skin.box);

            GUILayout.Label("<b>Status light = state</b>  (size = energy)", RichLabel());
            foreach (AgentState state in System.Enum.GetValues(typeof(AgentState)))
                DrawColoredRow(SimUtils.GetStateColor(state), state.ToString());

            GUILayout.Space(6);
            GUILayout.Label("<b>Main causes of death</b>", RichLabel());

            // LINQ: sort the dictionary by count, keep the top N.
            var topCauses = manager.DeathCauses.OrderByDescending(pair => pair.Value).Take(deathCausesShown);
            foreach (var cause in topCauses)
                GUILayout.Label($"{cause.Value} x  {cause.Key}");

            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------
        //  Drawing helpers
        // --------------------------------------------------------------------
        private static void DrawColoredRow(Color color, string text)
        {
            GUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
            DrawRect(swatch, color);
            GUILayout.Label(text);
            GUILayout.EndHorizontal();
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>
        /// Scatter-line graph of the census history. Every species is scaled
        /// to its own maximum so prey/predator cycles are visible side by side.
        /// </summary>
        private static void DrawGraph(Rect rect, SimulationManager manager)
        {
            DrawRect(rect, new Color(0f, 0f, 0f, 0.5f));

            var history = manager.CensusHistory;
            if (history.Count < 2) return;

            foreach (Species species in SimulationManager.AllSpecies)
            {
                int index = (int)species;
                int max = Mathf.Max(1, history.Max(record => record.counts[index]));
                Color color = SimUtils.GetSpeciesColor(species);

                for (int i = 0; i < history.Count; i++)
                {
                    float x = rect.x + rect.width * i / (history.Count - 1);
                    float y = rect.yMax - rect.height * history[i].counts[index] / max;
                    DrawRect(new Rect(x - 1.5f, y - 1.5f, 3f, 3f), color);
                }
            }
        }

        private static GUIStyle RichLabel()
        {
            return new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
