using UnityEngine;
using UnityEngine.InputSystem; // 1. 신버전 인풋 시스템 네임스페이스 필수 추가!

public class PlayerController : Entity
{
    [Header("Gun Settings")]
    public Gun gun;
    private bool isFiringPressed = false;
    
    [Header("Advanced Recoil Settings")]
    public float maxRecoilVelocity = 3f; // 반동으로 인해 뒤로 밀리는 최대 속도 제한

    [Header("Player Settings")]
    public float jumpForce = 10f;
    public Rigidbody rb;

    [Header("Camera Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 100f;
    public float MAX_CAMERA_ROTATION_X = 90f;
    private float cameraRotationX = 0f;

    [Header("Inventory UI Trigger")]
    public GameObject inventoryUIPanel; 
    public InventoryUI inventoryUI; // 인벤토리 스크립트 참조
    public Inventory playerInventory; // 플레이어가 가진 인벤토리 스크립트 참조
    private bool isInventoryOpen = false;
    public ItemDataSO debugTestItem;

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;

    protected override void Start()
    {
        base.Start();
        Cursor.lockState = CursorLockMode.Locked;
        
    }

    protected override void Update()
    {
        base.Update();
        HandleCamera();
        HandleJump();
        HandleShooting();

        if (isInventoryOpen && Keyboard.current.gKey.wasPressedThisFrame)
        {
            TriggerDebugItemInject();
        }
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    // 인풋 액션의 'Move'와 연동 (WASD)
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // 인풋 액션의 'Look'과 연동 (마우스 움직임)
    public void OnLook(InputValue value)
    {
        mouseInput = value.Get<Vector2>();
    }

    // 인풋 액션의 'Jump'와 연동
    public void OnJump(InputValue value)
    {
        isJumpPressed = value.isPressed;
    }

    // 인풋 액션의 'Fire'와 연동 (마우스 좌클릭) - 
    public void OnFire(InputValue value)
    {
        float fireValue = value.Get<float>();
        
        isFiringPressed = fireValue > 0.5f;

        if (isFiringPressed && gun != null && !gun.gunData.isAutomatic && !isInventoryOpen)
        {
            gun.Fire();
        }
    }

    public void OnReload(InputValue value)
    {
        if (isInventoryOpen) return;

        if (value.isPressed)
        {
            gun?.StartReload();
        }
    }

    public void OnInventory(InputValue value)
    {
        if (value.isPressed)
        {
            ToggleInventory();
        }
    }
    private void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;

        // 인벤토리 UI 패널 켜고 끄기
        if (inventoryUIPanel != null)
        {
            inventoryUIPanel.SetActive(isInventoryOpen);
            
            // 창이 열릴 때 화면 최신화 강제 시키기
            if (isInventoryOpen)
            {
                inventoryUI.RefreshAllUI();
            }
        }

        // 마우스 커서 상태 조절
        SetCursorState(isInventoryOpen);

        if (isInventoryOpen)
        {
            moveInput = Vector2.zero;
        }
    }
    private void SetCursorState(bool viewUI)
    {
        if (viewUI)
        {
            Cursor.lockState = CursorLockMode.None; // 마우스 고정 해제
            Cursor.visible = true;                  // 마우스 커서 보임
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked; // 마우스 화면 중앙 고정
            Cursor.visible = false;                   // 마우스 커서 숨김
        }
    }
    private void HandleMovement()
    {
        if (isInventoryOpen) 
        {
            // 움직이지 못하게 물리 속도만 딱 잡아두고 리턴
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        
        Vector3 targetPosition = rb.position + transform.TransformDirection(moveDirection) * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }

    private void HandleJump()
    {
        if (isJumpPressed && Mathf.Abs(rb.linearVelocity.y) < 0.001f)
        {
            rb.AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.Impulse);
            isJumpPressed = false;
        }
    }

    private void HandleCamera()
    {
        if (isInventoryOpen) return;
        
        float mouseX = mouseInput.x * mouseSensitivity * 0.1f * Time.deltaTime;
        float mouseY = mouseInput.y * mouseSensitivity * 0.1f * Time.deltaTime;
        
        // 좌우 회전 (몸통 돌리기)
        transform.Rotate(Vector3.up * mouseX);

        // 위아래 고개 숙이기 (카메라만 돌리기)
        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);

        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    private void HandleShooting()
    {
        if (gun == null || gun.gunData == null) return;

        if (isFiringPressed && gun.gunData.isAutomatic && !isInventoryOpen)
        {
            gun.Fire();
        }
    }

    public void AddRecoil(Vector3 recoilDirection, float force)
    {
        // 총을 쏠 때마다 카메라 x축 회전값을 1~2도씩 위로 
        cameraRotationX -= force * 0.5f; 
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);

        // 몸통 뒤로 밀림 제한 
        // 현재 플레이어의 속도 중 반동 방향(뒤쪽) 성분만 추출해서 검사
        float currentRecoilSpeed = Vector3.Dot(rb.linearVelocity, recoilDirection);

        if (currentRecoilSpeed < maxRecoilVelocity)
        {
            // 과도하게 날아가는 걸 막기 위해 힘을 살짝 보정해서 줌
            rb.AddForce(recoilDirection * force, ForceMode.Impulse);
        }
    }

    // 🛠️ 치트키 구동 로직: 가방에 아이템을 강제로 넣고 UI 리프레시
    private void TriggerDebugItemInject()
    {
        if (playerInventory != null && debugTestItem != null)
        {
            // 인벤토리에 디버그용 아이템 5개 추가 시도!
            playerInventory.AddItem(debugTestItem, 5); 
            
            // UI 다시 그리기
            if (inventoryUI != null) inventoryUI.RefreshAllUI();
            
            Debug.Log($"[치트키] {debugTestItem.name} 아이템 5개가 인벤토리에 추가되었습니다!");
        }
        else
        {
            Debug.LogWarning("Player에 Inventory가 없거나, PlayerController 인스펙터에 Debug Test Item이 비어있습니다.");
        }
    }
}