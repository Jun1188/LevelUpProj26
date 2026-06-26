using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : Entity
{
    [Header("Gun Settings")]
    public Gun gun;
    private bool isFiringPressed = false;
    
    [Header("Recoil Control (반동 제어 시스템)")]
    public float maxRecoilVelocity = 12f;      // 순간 최대 밀림 속도
    public float recoilDecaySpeed = 20f;       // 밀린 후 브레이크 잡히는 속도 (높을수록 빨리 멈춤)
    
    [Space(10)]
    [Header("🎯 Recoil Distance Limit (반동 거리 제한 옵션)")]
    public float maxRecoilDistance = 1.2f;     // 최대 뒤로 밀릴 수 있는 거리 (미터 단위, 1.2m 넘으면 강제 정지)
    public float recoilDistanceRecoverySpeed = 6f; // 연사를 멈췄을 때 반동 한계치가 원상복구되는 속도
    
    private Vector3 activeRecoilVelocity;      // 현재 반동 속도
    private float currentRecoilDistance = 0f;  // 현재 누적된 반동 이동 거리

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
    public InventoryUI inventoryUI; 
    public Inventory playerInventory; 
    private bool isInventoryOpen = false;
    public ItemDataSO debugTestItem;

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;

    protected override void Start()
    {
        base.Start();
        SetCursorState(false);
        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        HandleCamera();
        HandleJump();

        // 인벤토리 2중 안전장치 키
        if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }
        
        if (isInventoryOpen && Keyboard.current.gKey.wasPressedThisFrame)
        {
            TriggerDebugItemInject();
        }
    }

    private void FixedUpdate()
    {
        HandleMovement();
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
        
        if (inventoryUIPanel != null)
        {
            inventoryUIPanel.SetActive(isInventoryOpen);
            if (isInventoryOpen && inventoryUI != null)
            {
                inventoryUI.RefreshAllUI();
            }
        }

        SetCursorState(isInventoryOpen);

        if (isInventoryOpen)
        {
            moveInput = Vector2.zero;
            isFiringPressed = false;
            if (gun != null) gun.SetFiringPressed(false);
        }
    }

    private void SetCursorState(bool viewUI)
    {
        if (viewUI)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void HandleMovement()
    {
        if (isInventoryOpen) 
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            activeRecoilVelocity = Vector3.zero;
            currentRecoilDistance = 0f;
            return;
        }

        // 1. 몸통 반동 속도를 매 프레임 0으로 빠르게 감쇄 (브레이크 작용)
        activeRecoilVelocity = Vector3.MoveTowards(activeRecoilVelocity, Vector3.zero, Time.fixedDeltaTime * recoilDecaySpeed);

        // 2. 반동 속도가 완벽히 제어되면 누적된 반동 거리 한계치를 복구시킴
        if (activeRecoilVelocity.sqrMagnitude < 0.001f)
        {
            activeRecoilVelocity = Vector3.zero;
            currentRecoilDistance = Mathf.MoveTowards(currentRecoilDistance, 0f, Time.fixedDeltaTime * recoilDistanceRecoverySpeed);
        }

        // 3. 이번 프레임에 반동으로 인해 이동할 가상 거리를 미리 계산
        float frameRecoilMovement = activeRecoilVelocity.magnitude * Time.fixedDeltaTime;
        
        // 4. 만약 설정한 maxRecoilDistance를 초과하려고 하면 뒤로 가는 힘을 강제로 제로화(벽에 막힌 것처럼)
        if (currentRecoilDistance + frameRecoilMovement > maxRecoilDistance)
        {
            float allowedMovement = maxRecoilDistance - currentRecoilDistance;
            if (allowedMovement <= 0f)
            {
                activeRecoilVelocity = Vector3.zero;
                frameRecoilMovement = 0f;
            }
            else
            {
                // 한계선 직전까지만 움직이도록 속도 재조정
                activeRecoilVelocity = activeRecoilVelocity.normalized * (allowedMovement / Time.fixedDeltaTime);
                frameRecoilMovement = allowedMovement;
            }
        }
        
        // 최종 누적 거리 최신화
        currentRecoilDistance += frameRecoilMovement;

        // 일반 키보드 이동 계산
        Vector3 moveDirection = transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y)).normalized;
        Vector3 moveVelocity = moveDirection * moveSpeed;

        // 최종 Rigidbody 속도 조립 (이동 속도 + 철저히 제한된 반동 속도)
        rb.linearVelocity = new Vector3(moveVelocity.x + activeRecoilVelocity.x, rb.linearVelocity.y, moveVelocity.z + activeRecoilVelocity.z);
    }

    private void HandleCamera()
    {
        if (isInventoryOpen) return;

        float mouseX = mouseInput.x * mouseSensitivity * 0.1f * Time.deltaTime;
        float mouseY = mouseInput.y * mouseSensitivity * 0.1f * Time.deltaTime;
        
        transform.Rotate(Vector3.up * mouseX);

        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    // 화면 위로 튕기는 반동
    public void AddCameraRecoil(float verticalForce)
    {
        cameraRotationX -= verticalForce;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    // 몸통 뒤로 밀리는 반동
    public void AddHorizontalBodyRecoil(Vector3 recoilDirection, float force)
    {
        // 이미 정해진 한계 거리에 도달했다면 추가적인 밀림 속도를 원천 차단
        if (currentRecoilDistance >= maxRecoilDistance)
        {
            return;
        }

        activeRecoilVelocity += recoilDirection * force;
        
        if (activeRecoilVelocity.magnitude > maxRecoilVelocity)
        {
            activeRecoilVelocity = activeRecoilVelocity.normalized * maxRecoilVelocity;
        }
    }

    public void OnFire(InputValue value)
    {
        if (isInventoryOpen || gun == null) return;
        
        isFiringPressed = value.isPressed;
        gun.SetFiringPressed(isFiringPressed);
    }

    public void OnReload(InputValue value)
    {
        if (isInventoryOpen || gun == null) return;
        if (value.isPressed) gun.StartReload();
    }

    public void OnJump(InputValue value)
    {
        if (isInventoryOpen) return;
        isJumpPressed = value.isPressed;
    }

    public void OnMove(InputValue value)
    {
        if (isInventoryOpen) return;
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (isInventoryOpen) return;
        mouseInput = value.Get<Vector2>();
    }

    private void HandleJump()
    {
        if (isJumpPressed && Mathf.Abs(rb.linearVelocity.y) < 0.001f)
        {
            rb.AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.Impulse);
            isJumpPressed = false;
        }
    }

    private void TriggerDebugItemInject()
    {
        if (playerInventory != null && debugTestItem != null)
        {
            playerInventory.AddItem(debugTestItem, 5);
            if (inventoryUI != null) inventoryUI.RefreshAllUI();
            Debug.Log($"[치트] {debugTestItem.name} 5개 주입!");
        }
    }
}