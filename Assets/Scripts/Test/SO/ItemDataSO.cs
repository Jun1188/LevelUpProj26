using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item")]
public class ItemDataSO : ScriptableObject
{
    public new string name;
    public Sprite icon;
    public ItemType type;
}