using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : StackableSO, IConstructible
{
    public AssetKind kind = AssetKind.Item;
}
