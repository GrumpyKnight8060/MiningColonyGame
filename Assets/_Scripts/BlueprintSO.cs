// Assets/_Scripts/BlueprintSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Blueprint")]
public class BlueprintSO : ScriptableObject, IConstructible
{
    public string blueprintName;
    [Range(1, 6)] public int tier = 1;

    // What this blueprint unlocks (typically a structure or item)
    public Object unlocks; // StructureSO or ItemSO

    public AssetKind Kind => AssetKind.Blueprint;
}
