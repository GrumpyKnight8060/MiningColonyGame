// Assets/_Scripts/ResourceSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Resource")]
public class ResourceSO : StackableSO, IConstructible
{
    public int baseValue = 1; // economy/balance anchor
    public bool refined = false;

    public AssetKind Kind => AssetKind.Resource;
}
