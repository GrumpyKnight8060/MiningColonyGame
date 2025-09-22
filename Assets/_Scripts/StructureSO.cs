using UnityEngine;

[CreateAssetMenu(menuName = "Game/Structure")]
public class StructureSO : ScriptableObject, IConstructible
{
    public AssetKind kind = AssetKind.Structure;
}
