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
        foreach (var x in recipeSockets)
        {
            if (x != null)
                Destroy(x.gameObject);
        }



        if (building.Data is AssemblerDataSO assemblerData)
        {
            // 2. Building의 Behavior(동작 인스턴스)가 AssemblerBehavior인지 확인하고 캐스팅
            // (주의: building 클래스 내부에 IBuildingBehavior를 담고 있는 변수명에 맞춰 수정하세요. 
            // 여기서는 가칭으로 'Behavior' 또는 'behavior'라고 가정합니다.)
            if (building.Behavior is AssemblerBehavior assemblerBehavior)
            {
                // 3. 레시피 세팅 (예: availableRecipes 배열의 첫 번째 레시피를 할당)
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
