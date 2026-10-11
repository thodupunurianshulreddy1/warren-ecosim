// ============================================================================
//  AbilityType.cs   [H4]
//  Abilities the player unlocks as the warren grows (progression loop).
//  Which warren size unlocks which ability is data, set on the GameManager
//  (ProgressionSettings), not hard-coded here.
// ============================================================================

namespace ThodupunuriAnshul.EcoSim.Player
{
    public enum AbilityType
    {
        Thump,       // active : alarm every warren rabbit around you (re-uses the herd alarm)
        DigBurrow,   // active : create a new safe zone where you stand
        SwiftFeet    // passive: +15% speed, the reward for a big warren
    }
}
