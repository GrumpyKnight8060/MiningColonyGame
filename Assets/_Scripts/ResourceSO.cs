using UnityEngine;

[CreateAssetMenu(menuName = "MiningGame/Resource")]
public class ResourceSO : StackableSO, IConstructible
{
    // 'tier' already lives in StackableSO – don't redeclare it here.
    public string resourceName;
    public int baseValue;

    public string ObjectType => "Resource";
}
