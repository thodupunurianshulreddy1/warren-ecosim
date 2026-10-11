// ============================================================================
//  GameHUD.cs   [H4]
//  Everything the player reads, drawn with Unity's immediate-mode GUI
//  (OnGUI, like the Homework 3 PopulationHUD: no Canvas or package needed).
//
//  One screen per GameState:
//   Title    : name, goal, controls, best run, Start button
//   Playing  : day / night clock, warren, energy & stamina bars, status,
//              ability slots, danger banner, message toasts
//   Paused   : Resume / Restart / Title
//   GameOver / Victory : run summary, stars, best run, Restart
//
//  All drawing happens on a virtual 1600x900 canvas scaled to the window,
//  so the layout looks the same at any resolution.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Game;
using ThodupunuriAnshul.EcoSim.Player;
using ThodupunuriAnshul.EcoSim.Utilities;

namespace ThodupunuriAnshul.EcoSim.UI
{
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private float toastDuration = 3.5f;
        [SerializeField] private int maxToasts = 4;

        private const float VirtualHeight = 900f;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color EnergyColor = new Color(0.35f, 0.85f, 0.35f);
        private static readonly Color LowEnergyColor = new Color(0.95f, 0.35f, 0.25f);
        private static readonly Color StaminaColor = new Color(0.35f, 0.7f, 1f);
        private static readonly Color ExhaustedColor = new Color(0.45f, 0.45f, 0.55f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
        private static readonly Color DangerColor = new Color(0.85f, 0.1f, 0.1f, 0.85f);
        private static readonly Color WarningColor = new Color(0.95f, 0.55f, 0.1f, 0.8f);

        /// <summary>A message shown for a few seconds at the top of the screen.</summary>
        private struct Toast
        {
            public string text;
            public float expiresAt;   // unscaled time
        }

        private readonly List<Toast> toasts = new List<Toast>();
        private GameManager game;
        private bool stylesReady;
        private GUIStyle titleStyle, headerStyle, labelStyle, smallStyle, smallCenteredStyle, centeredStyle, bannerStyle, buttonStyle;
        private float virtualWidth;

        // ====================================================================
        //  Lifecycle
        // ====================================================================
        private void Start()
        {
            game = GameManager.Instance;
            if (game != null) game.Notification += AddToast;
        }

        private void OnDestroy()
        {
            if (game != null) game.Notification -= AddToast;
        }

        private void AddToast(string message)
        {
            toasts.Add(new Toast { text = message, expiresAt = Time.unscaledTime + toastDuration });
            while (toasts.Count > maxToasts) toasts.RemoveAt(0);
        }

        // ====================================================================
        //  OnGUI entry point
        // ====================================================================
        private void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();

            // Scale a virtual 900-pixel-high canvas to the real window.
            float scale = Screen.height / VirtualHeight;
            virtualWidth = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            switch (game.State)
            {
                case GameState.Title:
                    DrawTitleScreen();
                    break;
                case GameState.Playing:
                    DrawPlayingHUD();
                    break;
                case GameState.Paused:
                    DrawPlayingHUD();
                    DrawPauseMenu();
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    DrawEndScreen(game.State == GameState.Victory);
                    break;
            }

            DrawToasts();
        }

        // ====================================================================
        //  TITLE
        // ====================================================================
        private void DrawTitleScreen()
        {
            Rect panel = CenteredRect(640f, 600f);
            DrawRect(panel, PanelColor);
            GUILayout.BeginArea(Inset(panel, 30f));

            GUILayout.Label("WARREN", titleStyle);
            GUILayout.Label("Survive as a rabbit. Grow your warren. Outlast 7 nights.", centeredStyle);
            GUILayout.Space(18);

            GUILayout.Label("<b>How to play</b>", headerStyle);
            GUILayout.Label(
                "• Eat grown grass to keep your energy up. Eating takes time: watch out!\n" +
                "• Mate with ready rabbits (pink light): babies join your warren (gold tag).\n" +
                "• Burrows hide you from predators. Nights are darker and more dangerous.\n" +
                "• If your rabbit dies, you continue as an adult of your warren.\n" +
                "• A bigger warren unlocks Thump, Dig Burrow and Swift Feet.", labelStyle);
            GUILayout.Space(10);

            GUILayout.Label("<b>Controls</b>", headerStyle);
            GUILayout.Label(
                "WASD move   ·   Shift sprint   ·   E eat   ·   F mate\n" +
                "Space thump   ·   B dig   ·   right mouse / Z C orbit   ·   wheel zoom   ·   Esc pause", labelStyle);
            GUILayout.Space(10);

            RunRecord best = game.BestRun;
            GUILayout.Label(best == null
                ? "<i>No best run yet.</i>"
                : $"<b>Best run:</b> {best.score} pts · {best.daysSurvived} days · warren {best.peakWarren} · {best.stars}/3 stars",
                labelStyle);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start  (Enter)", buttonStyle, GUILayout.Height(52))) game.StartRun();
            GUILayout.EndArea();
        }

        // ====================================================================
        //  PLAYING
        // ====================================================================
        private void DrawPlayingHUD()
        {
            DrawRunPanel();
            DrawRabbitPanel();
            DrawAbilityPanel();
            DrawDangerBanner();
            GUI.Label(new Rect(16f, VirtualHeight - 34f, 700f, 24f),
                      "<i>WASD move · Shift sprint · E eat · F mate · Space thump · B dig · Esc pause</i>", smallStyle);
        }

        /// <summary>Top-left: clock, warren, generation, populations.</summary>
        private void DrawRunPanel()
        {
            Rect panel = new Rect(16f, 16f, 330f, 200f);
            DrawRect(panel, PanelColor);
            GUILayout.BeginArea(Inset(panel, 12f));

            var dayNight = game.DayNight;
            string phase = dayNight.IsNight ? "<color=#8FA8FF>NIGHT</color>" : "<color=#FFE27A>DAY</color>";
            GUILayout.Label($"<b>Day {game.Day} / {game.DaysToSurvive}</b>   {phase}   {Mathf.CeilToInt(dayNight.TimeLeftInPhase)}s left", headerStyle);
            Rect clock = GUILayoutUtility.GetRect(300f, 10f);
            DrawBar(clock, dayNight.PhaseProgress01, dayNight.IsNight ? new Color(0.4f, 0.45f, 0.9f) : GoldColor, null);

            GUILayout.Space(6);
            GUILayout.Label($"<b>Warren:</b> {game.WarrenSize}   (peak {game.CurrentRun.peakWarren})", labelStyle);
            GUILayout.Label($"<b>Generation:</b> {game.Generation}    <b>Babies:</b> {game.CurrentRun.babiesBorn}", labelStyle);

            SimulationManager sim = game.Simulation;
            GUILayout.Label(
                $"Rabbits {sim.CountOf(Species.Rabbit)} · Deer {sim.CountOf(Species.Deer)} · " +
                $"<color=#FF9A40>Foxes {sim.CountOf(Species.Fox)}</color> · <color=#B0B0C0>Wolves {sim.CountOf(Species.Wolf)}</color>",
                smallStyle);

            GUILayout.EndArea();
        }

        /// <summary>Bottom-centre: status line, energy and stamina bars.</summary>
        private void DrawRabbitPanel()
        {
            PlayerController player = game.PlayerController;
            Rect panel = new Rect(virtualWidth * 0.5f - 230f, VirtualHeight - 150f, 460f, 106f);
            DrawRect(panel, PanelColor);

            if (!player.HasRabbit)
            {
                string text = game.IsTransferringLineage ? "Your lineage passes on..." : "";
                GUI.Label(Inset(panel, 12f), text, centeredStyle);
                return;
            }

            Rabbit rabbit = player.Possessed;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 26f), GetStatusText(rabbit), centeredStyle);

            Rect energyBar = new Rect(panel.x + 20f, panel.y + 40f, panel.width - 40f, 22f);
            Color energyColor = rabbit.IsHungry ? LowEnergyColor : EnergyColor;
            DrawBar(energyBar, rabbit.Energy01, energyColor, $"Energy {rabbit.Energy01.ToPercent()}");

            Rect staminaBar = new Rect(panel.x + 20f, panel.y + 68f, panel.width - 40f, 14f);
            DrawBar(staminaBar, player.Stamina01, player.IsExhausted ? ExhaustedColor : StaminaColor,
                    player.IsExhausted ? "Out of breath" : null);

            float age01 = rabbit.Age01;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 84f, panel.width - 40f, 20f),
                      $"Age {age01.ToPercent()} of life{(rabbit.IsAdult ? "" : " (young)")}", smallStyle);
        }

        private string GetStatusText(Rabbit rabbit)
        {
            if (rabbit.IsHidden) return "<color=#9BE39B>Hidden in a burrow: predators can't see you</color>";

            switch (rabbit.State)
            {
                case AgentState.Eating: return "Eating... (move to drop the meal)";
                case AgentState.Mating: return "<color=#FF8AD0>Mating...</color>";
            }

            if (rabbit.IsHungry) return "<color=#FF8060>Hungry! Find grass (E)</color>";
            if (rabbit.CanPlayerMate) return "<color=#FF8AD0>Ready to mate: press F next to a rabbit</color>";

            string blocker = rabbit.GetMatingBlocker();
            return blocker != null ? $"Can't mate yet: {blocker}" : "";
        }

        /// <summary>Bottom-right: one slot per ability (locked / cooldown / ready).</summary>
        private void DrawAbilityPanel()
        {
            PlayerAbilities abilities = game.PlayerAbilities;
            const float slotWidth = 128f, slotHeight = 64f, gap = 8f;
            int count = PlayerAbilities.AllAbilities.Length;
            float totalWidth = count * slotWidth + (count - 1) * gap;
            float x = virtualWidth - totalWidth - 16f;
            float y = VirtualHeight - slotHeight - 16f;

            foreach (AbilityType ability in PlayerAbilities.AllAbilities)
            {
                Rect slot = new Rect(x, y, slotWidth, slotHeight);
                DrawRect(slot, PanelColor);

                string displayName = PlayerAbilities.GetDisplayName(ability);
                GameKey? key = PlayerAbilities.GetKey(ability);
                string keyLabel = key.HasValue ? InputHelper.GetKeyLabel(key.Value) : "passive";
                GUI.Label(new Rect(slot.x + 6f, slot.y + 4f, slot.width - 12f, 22f), $"<b>{displayName}</b>", centeredStyle);

                string detail;
                if (!abilities.IsUnlocked(ability))
                {
                    detail = $"<color=#999999>warren {game.Progression.GetRequirement(ability)}</color>";
                }
                else
                {
                    float remaining = abilities.GetCooldownRemaining(ability);
                    float duration = abilities.GetCooldownDuration(ability);
                    if (remaining > 0f && duration > 0f)
                    {
                        DrawRect(new Rect(slot.x, slot.yMax - 6f, slot.width * (1f - remaining / duration), 6f), StaminaColor);
                        detail = $"{Mathf.CeilToInt(remaining)}s";
                    }
                    else
                    {
                        detail = $"<color=#9BE39B>READY</color> [{keyLabel}]";
                        DrawRect(new Rect(slot.x, slot.yMax - 6f, slot.width, 6f), GoldColor);
                    }
                }
                GUI.Label(new Rect(slot.x + 6f, slot.y + 30f, slot.width - 12f, 24f), detail, centeredStyle);
                x += slotWidth + gap;
            }
        }

        /// <summary>Top-centre red banner when chased, orange hint when a fox is stalking you.</summary>
        private void DrawDangerBanner()
        {
            PlayerController player = game.PlayerController;
            if (!player.HasRabbit) return;

            Rect banner = new Rect(virtualWidth * 0.5f - 300f, 16f, 600f, 40f);
            Carnivore chaser = player.ActiveChaser;
            if (chaser != null)
            {
                DrawRect(banner, DangerColor);
                GUI.Label(banner, $"<b>A {chaser.Species.ToString().ToUpper()} IS CHASING YOU! Sprint to a burrow!</b>", bannerStyle);
            }
            else if (player.IsBeingStalked)
            {
                DrawRect(banner, WarningColor);
                GUI.Label(banner, "<b>You feel watched...</b>", bannerStyle);
            }
        }

        // ====================================================================
        //  PAUSE
        // ====================================================================
        private void DrawPauseMenu()
        {
            DrawRect(new Rect(0f, 0f, virtualWidth, VirtualHeight), new Color(0f, 0f, 0f, 0.4f));
            Rect panel = CenteredRect(360f, 290f);
            DrawRect(panel, PanelColor);
            GUILayout.BeginArea(Inset(panel, 24f));
            GUILayout.Label("PAUSED", titleStyle);
            GUILayout.Space(8);
            if (GUILayout.Button("Resume  (Esc)", buttonStyle, GUILayout.Height(44))) game.TogglePause();
            if (GUILayout.Button("Restart run", buttonStyle, GUILayout.Height(44))) game.RestartRun();
            if (GUILayout.Button("Back to title", buttonStyle, GUILayout.Height(44))) game.QuitToTitle();
            GUILayout.EndArea();
        }

        // ====================================================================
        //  END SCREEN
        // ====================================================================
        private void DrawEndScreen(bool victory)
        {
            Rect panel = CenteredRect(560f, 520f);
            DrawRect(panel, PanelColor);
            GUILayout.BeginArea(Inset(panel, 28f));

            GUILayout.Label(victory ? "THE WARREN LIVES ON" : "THE WARREN IS GONE", titleStyle);
            GUILayout.Label(victory
                ? $"Your lineage survived all {game.DaysToSurvive} nights."
                : $"Last death: {game.LastDeathCause ?? "unknown"}.", centeredStyle);

            GUILayout.Space(10);
            Rect starsRect = GUILayoutUtility.GetRect(100f, 50f);
            DrawStars(starsRect, game.CurrentRun.stars);

            RunRecord run = game.CurrentRun;
            GUILayout.Space(6);
            GUILayout.Label($"Days survived: <b>{run.daysSurvived}</b>", labelStyle);
            GUILayout.Label($"Peak warren: <b>{run.peakWarren}</b>     Babies born: <b>{run.babiesBorn}</b>", labelStyle);
            GUILayout.Label($"Generations: <b>{run.generations}</b>", labelStyle);
            GUILayout.Label($"Score: <b>{run.score}</b>{(game.IsNewBest ? "   <color=#FFD040>NEW BEST!</color>" : "")}", headerStyle);
            if (!game.IsNewBest && game.BestRun != null)
                GUILayout.Label($"Best: {game.BestRun.score} pts", labelStyle);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Play again  (Enter)", buttonStyle, GUILayout.Height(48))) game.RestartRun();
            if (GUILayout.Button("Back to title", buttonStyle, GUILayout.Height(36))) game.QuitToTitle();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Three diamonds, gold for each star earned. Each diamond is drawn as a stack of
        /// horizontal strips (no font glyph and no GUI rotation needed).
        /// </summary>
        private static void DrawStars(Rect area, int earned)
        {
            const int halfRows = 8;
            const float rowHeight = 2.5f, stripStep = 3.2f, spacing = 60f;
            float startX = area.center.x - spacing;

            for (int star = 0; star < 3; star++)
            {
                Color color = star < earned ? GoldColor : new Color(1f, 1f, 1f, 0.15f);
                float centerX = startX + star * spacing;
                for (int row = -halfRows; row <= halfRows; row++)
                {
                    float halfWidth = (halfRows - Mathf.Abs(row) + 1) * stripStep;
                    DrawRect(new Rect(centerX - halfWidth, area.center.y + row * rowHeight, halfWidth * 2f, rowHeight), color);
                }
            }
        }

        // ====================================================================
        //  TOASTS
        // ====================================================================
        private void DrawToasts()
        {
            toasts.RemoveAll(toast => Time.unscaledTime > toast.expiresAt);

            float y = 66f;
            for (int i = toasts.Count - 1; i >= 0; i--)   // newest on top
            {
                Rect rect = new Rect(virtualWidth * 0.5f - 320f, y, 640f, 30f);
                DrawRect(rect, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(rect, toasts[i].text, bannerStyle);
                y += 34f;
            }
        }

        // ====================================================================
        //  DRAWING HELPERS
        // ====================================================================
        private void EnsureStyles()
        {
            if (stylesReady) return;   // GUI.skin can only be read inside OnGUI

            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16, wordWrap = true };
            smallStyle = new GUIStyle(labelStyle) { fontSize = 13 };
            smallCenteredStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
            headerStyle = new GUIStyle(labelStyle) { fontSize = 19 };
            centeredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            titleStyle = new GUIStyle(labelStyle) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle = new GUIStyle(centeredStyle) { fontSize = 17, wordWrap = false };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, richText = true };
            stylesReady = true;
        }

        private Rect CenteredRect(float width, float height)
        {
            return new Rect((virtualWidth - width) * 0.5f, (VirtualHeight - height) * 0.5f, width, height);
        }

        private static Rect Inset(Rect rect, float padding)
        {
            return new Rect(rect.x + padding, rect.y + padding, rect.width - 2f * padding, rect.height - 2f * padding);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawBar(Rect rect, float value01, Color fill, string text)
        {
            DrawRect(rect, new Color(1f, 1f, 1f, 0.12f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), fill);
            if (!string.IsNullOrEmpty(text)) GUI.Label(rect, text, smallCenteredStyle);
        }
    }
}
