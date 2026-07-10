using Sirenix.OdinInspector;
using UnityEngine;

public class RecipeSocket : MonoBehaviour
{

    public Transform inputSocket;
    public Transform outputSocket;
    public Transform slotPrefab;

    [HideInInspector]
    public AssemblerBehavior target;

    ItemSocket[] slots;
    RecipeDataSO recipe;

    public void Setup(RecipeDataSO _recipe)
    {
        if (_recipe == null) { Debug.LogError("trying setup recipeSocket without proper recipe"); return; }

        recipe = _recipe;

        //foreach (var x in slots)
        //{
        //    if(x != null)
        //        Destroy(x.gameObject);
        //}

        slots = new ItemSocket[_recipe.inputs.Length + _recipe.outputs.Length];

        for (int i = 0; i < _recipe.inputs.Length; i++)
        {
            slots[i] = Instantiate(slotPrefab, inputSocket).GetComponent<ItemSocket>();
            slots[i].SetItem(_recipe.inputs[i].item, _recipe.inputs[i].amount);
        }

        for (int i = 0; i < _recipe.outputs.Length; i++)
        {
            slots[i + _recipe.inputs.Length] = Instantiate(slotPrefab, outputSocket).GetComponent<ItemSocket>();
            slots[i+_recipe.outputs.Length].SetItem(_recipe.outputs[i].item, _recipe.outputs[i].amount);
        }
    }

 
    public void OnClicked()
    {
        if (recipe == null) { Debug.LogError("trying select recipe without proper recipe"); return; }
        if(target == null) { Debug.LogError("trying select recipe without proper target"); return; }
        
        target.SetRecipe(recipe);

        

    }

}
