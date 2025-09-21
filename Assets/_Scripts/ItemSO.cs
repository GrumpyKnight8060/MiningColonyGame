using UnityEngine;

[CreateAssetMenu(menuName = "MiningGame/Item")]
public class ItemSO : StackableSO, IConstructible
{
    // 'tier' already lives in StackableSO – don't redeclare it here.
    public string itemName;

    public string ObjectType => "Item";
}
