using UnityEngine;
using UnityEngine.UI;
using System.Collections;

using System.Collections.Generic;
using System;

public class ItemSocket : MonoBehaviour
{

    [SerializeField]
    Image it_sprite;
    [SerializeField]
    ItemDataSO itemDataSO;

    public void SetItem(ItemDataSO item)
    {
        itemDataSO = item;
        it_sprite.sprite = item.icon;
    }
    public ItemDataSO GetItem()
    {
        return itemDataSO;
    }
}
