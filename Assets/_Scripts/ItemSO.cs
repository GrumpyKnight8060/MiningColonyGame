using UnityEngine;

[System.Serializable]
public struct RecipeCost
{
    public ResourceSO resource;
    public int amount;
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item (Craftable)", order = 1)]
public class ItemSO : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    public Sprite icon;
    public int tier; // for unlocks/progression

    [Header("Crafting")]
    public RecipeCost[] recipeInputs;
    public int craftTimeSeconds = 0;   // 0 = instant for basics
    public int outputQuantity = 1;
}
