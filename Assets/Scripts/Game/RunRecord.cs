// ============================================================================
//  RunRecord.cs   [H4]
//  The data of one run (what the end screen shows) and its persistence.
//  The best run is saved between sessions with PlayerPrefs + JsonUtility,
//  so the title screen can show "Best: ..." (a persistent goal to beat).
// ============================================================================

using System;
using UnityEngine;

namespace ThodupunuriAnshul.EcoSim.Game
{
    [Serializable]
    public class RunRecord
    {
        public int daysSurvived;
        public int peakWarren;
        public int babiesBorn;
        public int generations = 1;
        public int score;
        public int stars;
        public bool victory;
        public string date;

        public RunRecord Clone()
        {
            return (RunRecord)MemberwiseClone();   // all fields are values or immutable strings
        }
    }

    /// <summary>Static helper that saves / loads the best run.</summary>
    public static class RunRecordStorage
    {
        private const string BestRunKey = "ThodupunuriAnshul.Warren.BestRun";

        public static RunRecord LoadBest()
        {
            if (!PlayerPrefs.HasKey(BestRunKey)) return null;
            try
            {
                return JsonUtility.FromJson<RunRecord>(PlayerPrefs.GetString(BestRunKey));
            }
            catch (ArgumentException)
            {
                return null;   // corrupted data: behave as if there was no record
            }
        }

        public static void SaveBest(RunRecord run)
        {
            PlayerPrefs.SetString(BestRunKey, JsonUtility.ToJson(run));
            PlayerPrefs.Save();
        }

        public static bool IsBetter(RunRecord candidate, RunRecord best)
        {
            return best == null || candidate.score > best.score;
        }
    }
}
