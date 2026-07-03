using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Info")]
    public string promptMessage = "열기"; // 화면 중앙 조준점 근처에 띄울 글자 (예: "상자 열기")

    // 플레이어가 바라보고 E키를 눌렀을 때 실행될 함수 (자식들이 직접 구현)
    public abstract void OnInteract(PlayerController player);
}