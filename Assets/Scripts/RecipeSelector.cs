using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeSelector : MonoBehaviour
{
    [SerializeField] Transform recipeSocketPrefab;
    [SerializeField] Transform recipeSocketParent;
    RecipeSocket[] recipeSockets;


    public void Init(Building building)
    {
        GameManager.Instance.interacting = true;

        foreach (var x in recipeSockets)
        {
            if (x != null)
                Destroy(x.gameObject);
        }



        if (building.Data is AssemblerDataSO assemblerData)
        {

            if (building.Behavior is AssemblerBehavior assemblerBehavior)
            {
                if (assemblerData.availableRecipes != null && assemblerData.availableRecipes.Length > 0)
                { 

                    recipeSockets = new RecipeSocket[assemblerData.availableRecipes.Length];

                    for (int i = 0; i < recipeSockets.Length; i++)
                    {
                        var recipeSocket = Instantiate(recipeSocketPrefab, recipeSocketParent).GetComponent<RecipeSocket>();
                        recipeSocket.Setup(assemblerData.availableRecipes[i]);
                        recipeSockets[i] = recipeSocket;
                        recipeSocket.target = assemblerBehavior;
                    }
                }
            }
        }
    }

}
