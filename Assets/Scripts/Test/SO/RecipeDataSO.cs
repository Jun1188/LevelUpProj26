using UnityEngine;
using System;

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Factory/Recipe")]
public class RecipeDataSO : ScriptableObject
{
    [Serializable]
    public struct Slot { public ItemDataSO item; public int amount; }

    public new string name;
    public Slot[]     inputs;
    public Slot[]     outputs;
    public float      craftTime = 2f;
}
