using UnityEngine;

public enum StationType
{
    // Assemblers
    BasicAssembler,       // T1, 1 input (solids only)
    IndustrialAssembler,  // T3, 2 inputs (solids only)
    AutomatedAssembler,   // T4, 2 inputs, one may be liquid
    MassAssembler,        // T6, 3 inputs, one may be liquid

    // Smelters
    StoneSmelter,         // T1, 1 input
    ElectricSmelter,      // T3, 1 input
    BlastFurnace,         // T4, 2 inputs (alloys OK)
    QuantumSmelter,       // T6, 2 inputs (alloys OK)

    // Refineries (liquids)
    BasicRefinery,        // T3, 2 inputs (liquids OK), 2 outputs
    IndustrialRefinery,   // T4, 3 inputs (liquids OK), 2 outputs
    MassRefinery          // T6, 3 inputs (liquids OK), 3 outputs
}

[System.Serializable]
public struct RecipeInput
{
    public StackableSO ingredient;
    public int amount;
    public bool isLiquid; // mark true if the input is a liquid (Oil, Water, Electrolyte, etc.)
}

[System.Serializable]
public struct RecipeOutput
{
    public StackableSO product;
    public int amount;
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Game/Recipe", order = 1)]
public class RecipeSO : ScriptableObject
{
    [Header("Identity")]
    public string recipeName;
    public Sprite icon;
    [Range(1, 9)] public int tier = 1;

    [Header("Crafting")]
    public StationType[] stations; // which stations can run this recipe
    public RecipeInput[] inputs;   // inputs (some stations allow a liquid slot)
    public RecipeOutput[] outputs; // one or more outputs (refineries support multi-output)
    public int craftTimeSeconds = 0;

    [Header("Availability")]
    public bool unlockedByDefault = true;
    public string unlockTag;
}
