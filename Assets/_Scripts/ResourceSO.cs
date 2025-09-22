using UnityEngine;

[CreateAssetMenu(menuName = "Game/Resource")]
public class ResourceSO : StackableSO, IConstructible
{
    public AssetKind kind = AssetKind.Resource;
}
