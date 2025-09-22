using UnityEngine;

[CreateAssetMenu(menuName = "Game/Recipe")]
public class RecipeSO : ScriptableObject, IConstructible
{
    public AssetKind kind = AssetKind.Recipe;

    public string recipeName;
    public ScriptableObject[] inputs;   // Resources or Items
    public ScriptableObject output;     // Resource, Item, or Structure
    public float craftTime;
}
