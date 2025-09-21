using UnityEngine;

[CreateAssetMenu(menuName = "MiningGame/Recipe")]
public class RecipeSO : ScriptableObject, IConstructible
{
    public ScriptableObject[] inputs;
    public ScriptableObject output;
    public int time;
    public int tier;

    public string ObjectType => "Recipe";
}
