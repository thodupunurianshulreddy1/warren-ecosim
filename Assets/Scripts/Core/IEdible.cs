// ============================================================================
//  IEdible.cs
//  Interface implemented by anything that can be eaten.
//  - Plant      implements it (eaten by herbivores)
//  - Herbivore  implements it (eaten by carnivores)
//  Thanks to this interface, the eating logic in Animal does not need to
//  know WHAT it is eating: it only talks to the IEdible contract.
// ============================================================================

namespace ThodupunuriAnshul.EcoSim.Core
{
    public interface IEdible
    {
        /// <summary>True if this food source can currently be eaten.</summary>
        bool CanBeEaten { get; }

        /// <summary>
        /// Called by the eater when it reaches the food.
        /// Returns the amount of energy the eater gains.
        /// The food decides itself what happens to it (plant is cut, prey dies...).
        /// </summary>
        float GetEaten(SimEntity eater);
    }
}
