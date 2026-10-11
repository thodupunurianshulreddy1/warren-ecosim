// ============================================================================
//  GameState.cs   [H4]
//  The states of a run, owned by the GameManager (a small state machine):
//
//     Title --Start--> Playing <--Pause/Resume--> Paused
//                         |  \
//                (warren gone)  (day 8 reached)
//                         v      v
//                    GameOver   Victory --Restart--> Playing
// ============================================================================

namespace ThodupunuriAnshul.EcoSim.Game
{
    public enum GameState
    {
        Title,      // simulation runs in the background, no rabbit possessed
        Playing,    // the run is on
        Paused,     // Time.timeScale = 0
        GameOver,   // the whole warren died
        Victory     // the warren survived every night
    }
}
