using UnityEngine;

public class ProcessorTester : MonoBehaviour
{
    [Header("=== Test Targets ===")]
    public InventoryProcessor inventoryProcessor; // 테스트할 인벤토리 프로세서 컴포넌트
    public RecipeDataSO testRecipe;               // 테스트해볼 레시피 SO 에셋

    [Header("=== Player Inventory Reference ===")]
    public PlayerController player;               // 플레이어 정보 (가방 참조용)

    private void Update()
    {
        // 🌟 키보드 T 키를 누르면 테스트를 시작합니다.
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (inventoryProcessor == null || testRecipe == null || player == null)
            {
                Debug.LogError("[테스터] 인스펙터 창에서 필요한 컴포넌트나 SO 에셋 연결이 누락되었습니다!");
                return;
            }

            // 1. 테스트를 위해 가방 설정을 수동으로 주입해줍니다.
            inventoryProcessor.inputInventory = player.playerInventory;
            inventoryProcessor.outputInventory = player.playerInventory;

            Debug.Log($"[테스터] 'T' 입력 감지: {testRecipe.name} 제작 시도를 프로세서에 명령합니다.");
            
            // 2. 프로세서 가동!
            inventoryProcessor.SetRecipeAndStart(testRecipe);
        }
    }
}