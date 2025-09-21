using UnityEngine;

[CreateAssetMenu(menuName = "MiningGame/Blueprint")]
public class BlueprintSO : ScriptableObject, IConstructible
{
    public string blueprintName;
    public int tier;

    public string ObjectType => "Blueprint";
}
