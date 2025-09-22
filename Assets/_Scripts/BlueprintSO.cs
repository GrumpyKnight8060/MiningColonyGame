using UnityEngine;

[CreateAssetMenu(menuName = "Game/Blueprint")]
public class BlueprintSO : ScriptableObject, IConstructible
{
    public AssetKind kind = AssetKind.Blueprint;

    public string blueprintName;
    public StructureSO targetStructure; // what this blueprint builds
    public int researchTierRequired;
}
