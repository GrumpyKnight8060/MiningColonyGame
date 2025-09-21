namespace MiningColonyGame
{
    /// <summary>
    /// Classification for all ScriptableObjects in the project.
    /// Used in validation to ensure recipes only output valid types.
    /// </summary>
    public enum AssetKind
    {
        Resource,   // e.g. Iron Ore, Steel Bar, Fiber
        Item,       // e.g. Armor, Packs, Optics
        Structure,  // e.g. Smelters, Assemblers, Refineries
        Recipe,     // Crafting instructions (inputs → outputs)
        Blueprint   // Unlockable designs for items/structures
    }
}
