using System;
using System.Collections;
using UnityEngine;

public abstract class BaseProcessor : MonoBehaviour
{
    [Header("=== Base Production Settings ===")]
    public RecipeDataSO currentRecipe; 

    protected bool isProcessing = false;
    protected float progressNormalized = 0f; 

    protected virtual bool IsAutomation => true;

    // ====================================================================
    // [다른 팀원용 플러그인 레일] 상호작용 및 UI 연동용 이벤트 (Actions)
    // ====================================================================
    public event Action OnProductionStarted;                     // 제작이 시작될 때 실행할 행동들
    public event Action<RecipeDataSO> OnProductionCompleted;     // 제작이 끝났을 때 (결과물 데이터 전달)
    public event Action OnProductionStopped;                     // 재료 부족 등으로 기계가 멈췄을 때
    public event Action OnRecipeChanged;                         // 레시피가 교체되었을 때

    /// <summary>
    /// 외부 호출용 API 1: 기계나 제작대에 레시피를 주입하고 가동시킵니다.
    /// </summary>
    public void SetRecipeAndStart(RecipeDataSO recipe)
    {
        if (recipe == null) return;
        
        currentRecipe = recipe;
        OnRecipeChanged?.Invoke(); // "레시피 바뀌었다!" 고 알림

        if (!isProcessing)
        {
            StartCoroutine(ProductionRoutine());
        }
    }

    /// <summary>
    /// 외부 호출용 API 2: 가동 중인 공정을 강제로 정지시킵니다.
    /// </summary>
    public void StopProduction()
    {
        currentRecipe = null;
        // 코루틴은 다음 루프 검사 때 currentRecipe가 null인 것을 보고 자연스럽게 종료됩니다.
    }

    private IEnumerator ProductionRoutine()
    {
        isProcessing = true;
        OnProductionStarted?.Invoke(); // "오브젝트 가동 시작!" 이벤트 발동

        while (currentRecipe != null)
        {
            if (!HasEnoughIngredients())
            {
                OnProductionStopped?.Invoke(); // "재료 부족으로 일시정지" 이벤트 발동
                if (IsAutomation)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                else break;
            }

            ConsumeIngredients();
            progressNormalized = 0f;

            float timer = 0f;
            float duration = currentRecipe.craftTime;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                progressNormalized = Mathf.Clamp01(timer / duration);
                yield return null;
            }

            // 결과물 임시 캐싱 후 지급
            RecipeDataSO finishedRecipe = currentRecipe;
            GiveOutputs();
            
            Debug.Log($"<color=cyan>[{gameObject.name}] {currentRecipe.name} 생산 완료!</color>");
            // "제작 완료!" 이벤트 발동 (상호작용 담당 팀원은 여기에 이펙트나 UI 갱신을 묶음)
            OnProductionCompleted?.Invoke(finishedRecipe); 

            progressNormalized = 0f;

            if (!IsAutomation) break;
        }

        isProcessing = false;
        OnProductionStopped?.Invoke(); // "최종 가동 종료" 이벤트 발동
    }

    protected abstract bool HasEnoughIngredients();
    protected abstract void ConsumeIngredients();
    protected abstract void GiveOutputs();

    // ====================================================================
    // [다른 팀원용 정보 제공 포트] UI 슬라이더나 에임 정보창에 연동할 Getter
    // ====================================================================
    public float GetProgress() => progressNormalized; // 현재 진행률 (0.0 ~ 1.0)
    public bool IsProcessing() => isProcessing;       // 현재 돌아가는 중인가?
    public string GetRecipeName() => currentRecipe != null ? currentRecipe.name : "None";
}