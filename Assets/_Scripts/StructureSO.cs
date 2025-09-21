using UnityEngine;

[CreateAssetMenu(menuName = "MiningGame/Structure")]
public class StructureSO : ScriptableObject, IConstructible
{
    public string structureName;
    public int tier;

    public string ObjectType => "Structure";
}
