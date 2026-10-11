// ============================================================================
//  SimEnums.cs
//  Shared enumerations used across the whole simulation.
//  Author : Anshul Thodupunuri  -  Homework 3 (Unity C# / OOP)
// ============================================================================

namespace ThodupunuriAnshul.EcoSim.Core
{
    /// <summary>
    /// Every kind of entity that can exist in the ecosystem.
    /// The SimulationManager uses this to know which prefab to spawn,
    /// and the HUD uses it to count populations.
    /// </summary>
    public enum Species
    {
        Grass,
        Rabbit,
        Deer,
        Fox,
        Wolf
    }

    /// <summary>
    /// The behavioural state of an animal (a simple finite state machine).
    /// The current state is shown in the scene by the colour of the
    /// small "status light" floating above each animal.
    /// </summary>
    public enum AgentState
    {
        Wandering,    // no particular goal, roaming randomly
        SeekingFood,  // herbivore walking toward a plant
        Hunting,      // carnivore running after a prey
        Eating,       // busy: consuming / digesting food
        Fleeing,      // herbivore running away from a predator
        SeekingMate,  // walking toward a partner of the same species
        Mating,       // busy: producing offspring
        Resting,      // busy: recovering after an exhausting chase
        Dead          // playing the death animation, about to be destroyed
    }
}
