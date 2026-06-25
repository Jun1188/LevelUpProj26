using UnityEngine;

public class ItemDataHolder : MonoBehaviour
{
    [SerializeField]
    ItemDataSO item;

    public ItemDataSO GetItem()
    {
        return item;
    }

}
