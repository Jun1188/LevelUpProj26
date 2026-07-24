#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// JSON 파싱용 구조
[Serializable]
public class RecipeJsonData
{
    public string fileName;
    public string displayName;
    public float craftTime;
    public SlotJsonData[] inputs;
    public SlotJsonData[] outputs;
}

[Serializable]
public class SlotJsonData
{
    public string itemId;
    public int amount;
}

// JSON 배열 읽기 래퍼
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{\"Items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.Items;
    }

    [Serializable]
    private class Wrapper<T> { public T[] Items; }
}

public class RecipeImporter : EditorWindow
{
    // 유니티 메뉴바 [Tools] -> [Generate Recipe SOs from JSON]
    [MenuItem("Tools/Generate Recipe SOs from JSON")]
    public static void GenerateRecipes()
    {
        // 1. JSON 위치: Assets/Datas/Recipe/Recipes.json 경로로 지정!
        string jsonPath = Path.Combine(Application.dataPath, "Data/Recipe/Recipes.json");

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[RecipeImporter] {jsonPath} 경로에 JSON 파일이 존재하지 않습니다! 파일명을 확인해주세요.");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        RecipeJsonData[] recipeList = JsonHelper.FromJson<RecipeJsonData>(jsonContent);

        // 2. SO 에셋을 생성/저장할 폴더 경로
        string saveFolderPath = "Assets/Data/Recipe";
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }

        // 3. 프로젝트의 아이템 SO 데이터베이스 구축
        Dictionary<string, ItemDataSO> itemDatabase = BuildItemDatabase();

        int createdCount = 0;

        foreach (var recipeData in recipeList)
        {
            string assetPath = $"{saveFolderPath}/{recipeData.fileName}.asset";
            
            RecipeDataSO recipeSO = AssetDatabase.LoadAssetAtPath<RecipeDataSO>(assetPath);
            if (recipeSO == null)
            {
                recipeSO = ScriptableObject.CreateInstance<RecipeDataSO>();
                AssetDatabase.CreateAsset(recipeSO, assetPath);
            }

            recipeSO.displayName = recipeData.displayName;
            recipeSO.craftTime = recipeData.craftTime;

            recipeSO.inputs = ConvertSlots(recipeData.inputs, itemDatabase);
            recipeSO.outputs = ConvertSlots(recipeData.outputs, itemDatabase);

            EditorUtility.SetDirty(recipeSO);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[RecipeImporter] 성공! 총 {createdCount}개의 RecipeDataSO가 '{saveFolderPath}'에 생성/갱신되었습니다.</color>");
    }

    private static Dictionary<string, ItemDataSO> BuildItemDatabase()
    {
        var db = new Dictionary<string, ItemDataSO>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:ItemDataSO");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemDataSO item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
            if (item != null)
            {
                if (!db.ContainsKey(item.name)) db.Add(item.name, item);
                if (!string.IsNullOrEmpty(item.displayName) && !db.ContainsKey(item.displayName))
                    db.Add(item.displayName, item);
            }
        }
        return db;
    }

    private static RecipeDataSO.Slot[] ConvertSlots(SlotJsonData[] jsonSlots, Dictionary<string, ItemDataSO> itemDb)
    {
        if (jsonSlots == null) return new RecipeDataSO.Slot[0];

        List<RecipeDataSO.Slot> slotList = new List<RecipeDataSO.Slot>();
        foreach (var jSlot in jsonSlots)
        {
            if (itemDb.TryGetValue(jSlot.itemId, out ItemDataSO itemSO))
            {
                slotList.Add(new RecipeDataSO.Slot { item = itemSO, amount = jSlot.amount });
            }
            else
            {
                Debug.LogWarning($"[RecipeImporter] 아이템 '{jSlot.itemId}'을(를) 에셋에서 찾을 수 없습니다.");
            }
        }
        return slotList.ToArray();
    }
}
#endif