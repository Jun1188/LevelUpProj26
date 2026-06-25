using UnityEngine;

public class FactoryTest : MonoBehaviour
{
    [Header("ScriptableObjects — Inspector에서 연결")]
    public BuildingDataSO minerSO;
    public BuildingDataSO beltSO;
    public BuildingDataSO assemblerSO;
    public ItemDataSO ironOreSO;
    public ItemDataSO ironIngotSO;

    void Start()
    {
        MiningService.GetItemAt = _ => ironOreSO;

        var miner = PlacementBridge.Place(minerSO, new Vector2Int(0, 0));
        var belt1 = PlacementBridge.Place(beltSO, new Vector2Int(1, 0));
        var belt2 = PlacementBridge.Place(beltSO, new Vector2Int(2, 0));
        var assembler = PlacementBridge.Place(assemblerSO, new Vector2Int(3, 0));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var inst = GridRegistry.Instance.GetAt(new Vector2Int(3, 0));
            if (inst == null) return;

            var inv = inst.Inventory; 
            Debug.Log($"[Assembler] 재료 대기(Ore): {inv.InputAmount(ironOreSO)} 개 / 제작 완료(Ingot): {inv.OutputAmount(ironIngotSO)} 개");
        }
    }
}