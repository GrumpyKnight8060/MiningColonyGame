using UnityEngine;

public enum StationType { Furnace, Foundry, ChemPlant, Workshop, Factory, SpaceStation }

[System.Serializable]
public struct RecipeInput
{
    public StackableSO ingredient; // <-- object picker for ResourceSO or other StackableSO
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
    public StationType[] stations;         // multiple valid stations
    public RecipeInput[] inputs;
    public ResourceSO output;              // produced resource (usually a Bar)
    public int outputAmount = 1;
    public int craftTimeSeconds = 0;

    [Header("Availability")]
    public bool unlockedByDefault = true;
    public string unlockTag;
}
