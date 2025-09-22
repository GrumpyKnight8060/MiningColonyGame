// Assets/_Scripts/StructureSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Structure")]
public class StructureSO : ScriptableObject, IConstructible
{
    [Header("Structure")]
    public string displayName;
    [Range(1, 6)] public int tier = 1;

    public AssetKind Kind => AssetKind.Structure;
}
