using JetBrains.Annotations;
using UnityEngine;

[System.Serializable]
public class  Ingredient
{
    public ItemDataSO item;
    public int amount = 1;
}

[CreateAssetMenu(fileName = "New_Recipe", menuName = "ScriptableObjects/Recipe")]
public class RecipeDataSO : ScriptableObject
{
    public string name = "New_Recipe";
    public Ingredient[] inputs;  // {item, amount}
    public Ingredient[] outputs;
    public float craftTime;

    // 예: 철광석 2개 → 철판 1개 (2초)
}