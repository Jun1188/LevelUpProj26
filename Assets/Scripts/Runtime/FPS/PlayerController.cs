using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : Entity
{
    #region [1. Variables - Inspector Settings]
    
    [Header("Core Components")]
    public Rigidbody rb;

    [Header("Camera & Mouse Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 1f; 
    public float MAX_CAMERA_ROTATION_X = 90f;
    private float cameraRotationX = 0f;

    [Header("Movement Settings")]
    public float jumpForce = 10f;

    [Header("Gun & Combat Settings")]
    public Gun gun;
    private bool isFiringPressed = false;

    [Header("Recoil Control System")]
    public float maxRecoilVelocity = 12f;      
    public float recoilDecaySpeed = 20f;       
    private Vector3 activeRecoilVelocity;      

    [Header("Inventory Backend")]
    public Inventory playerInventory; 
    private bool isInventoryOpen = false;
    private Inventory currentOpenedInventory = null; 

    [Header("Inventory & HUD UI")]
    public GameObject inventoryUIPanel; 
    public InventoryUI inventoryUI;     
    public InventoryUI chestInventoryUI;
    public GameObject crosshairUI;      
    public TMPro.TextMeshProUGUI promptText; 

    [Header("Debug / Cheat Tools")]
    public ItemDataSO debugTestItem; 

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;
    [Header("Inventory & Hotbar Size Settings (★중앙 제어 타워)")]
    public int hotbarSlotCount = 9;       // 핫바 칸 수 (0번부터 hotbarSlotCount-1번까지 핫바로 사용)
    private int currentHotbarIndex = 0;   // 현재 선택된 핫바 인덱스 (0 ~ hotbarSlotCount-1)
    public int CurrentHotbarIndex => currentHotbarIndex;
    #endregion

    #region [2. Unity Lifecycle]

    protected override void Start()
    {
        base.Start();
        
        CloseInventory();
        moveSpeed = 5f; 

        if (InventoryManager.Instance != null && playerInventory != null)
        {
            InventoryManager.Instance.CheckWeaponEquip(playerInventory);
        }
    }
    // Get 속성을 열어두어 다른 스크립트(UI 등)에서 현재 몇 번 슬롯이 활성화 상태인지 알 수 있게 합니다.
    protected override void Update()
    {
        base.Update();

        if (!isInventoryOpen)
        {
            HandleCameraRotation();
            HandleInteractionRaycast();
            HandleHotbarInput();
        }
        else
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
        }
    }

    private void FixedUpdate()
    {
        if (!isInventoryOpen)
        {
            HandleMovement();
            HandleJump();
        }
    }

    #endregion

    #region [3. New Input System Callbacks]

    public void OnInventory(InputValue value)
    {
        //if (!value.isPressed) return;
        ToggleInventory();
    }

    public void OnInteract(InputValue value)
    {
        if (!value.isPressed) return;

        // UI 창이 열려있을 때 E를 누르면 창을 닫아줍니다.
        if (isInventoryOpen)
        {
            CloseInventory();
            return;
        }

        // 창이 닫혀있을 때는 레이캐스트를 발사해 조준 중인 상자를 엽니다.
        Ray ray = new(playerCamera.position, playerCamera.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 4f, LayerMask.GetMask("Interactable")))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable != null)
            {
                interactable.OnInteract(this);
            }
        }
    }

    public void OnMove(InputValue value)
    {
        if (isInventoryOpen) 
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (isInventoryOpen) 
        {
            mouseInput = Vector2.zero;
            return;
        }
        mouseInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (isInventoryOpen) return;
        isJumpPressed = value.isPressed;
    }

    public void OnFire(InputValue value)
    {
        if (isInventoryOpen || gun == null || gun.gunData == null) return;
        
        isFiringPressed = value.isPressed;
        gun.SetFiringPressed(isFiringPressed);
        
        if (isFiringPressed && !gun.gunData.isAutomatic)
        {
            gun.Fire();
        }
    }

    public void OnReload(InputValue value)
    {
        if (isInventoryOpen || gun == null) return;
        if (value.isPressed) gun.StartReload();
    }

    public void OnQuickDrop(InputValue value)
    {
        if (!value.isPressed) return;
        DropActiveHotbarItem();
    }

    #endregion

    #region [4. Core Mechanics - Movement & Camera]

    private void HandleCameraRotation()
    {
        float mouseX = mouseInput.x * mouseSensitivity * 0.1f;
        float mouseY = mouseInput.y * mouseSensitivity * 0.1f;

        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        Vector3 targetVelocity = moveDir * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; 
        
        rb.linearVelocity = targetVelocity;
    }

    private void HandleJump()
    {
        if (isJumpPressed && Mathf.Abs(rb.linearVelocity.y) < 0.001f)
        {
            rb.AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.Impulse);
            isJumpPressed = false;
        }
    }

    public void AddRecoil(Vector3 recoilDirection, float verticalRecoil, float horizontalRecoil)
    {
        cameraRotationX -= verticalRecoil; 
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);

        float currentRecoilSpeed = Vector3.Dot(rb.linearVelocity, recoilDirection);
        if (currentRecoilSpeed < maxRecoilVelocity)
        {
            rb.AddForce(recoilDirection * horizontalRecoil, ForceMode.Impulse);
        }
    }

    #endregion

    #region [5. Core Mechanics - Interaction & Raycast]

    private void HandleInteractionRaycast()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 4f, LayerMask.GetMask("Interactable")))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            
            if (interactable != null)
            {
                if (promptText != null)
                {
                    promptText.gameObject.SetActive(true);
                    promptText.text = $"[E] {interactable.promptMessage}";
                }
                return; 
            }
        }

        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    #endregion

    #region [6. Core Mechanics - Inventory System Management]

    public void ToggleInventory()
    {
        if (isInventoryOpen)
        {
            CloseInventory();
        }
        else
        {
            OpenPlayerInventory();
        }
    }

    public void OpenPlayerInventory()
    {
        isInventoryOpen = true;
        ResetInputValues();

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);
        if (inventoryUI != null) inventoryUI.RefreshAllUI();
        
        ToggleCursorAndHUD(false);
    }

    public void OpenTargetInventory(Inventory targetInventory)
    {
        isInventoryOpen = true;
        currentOpenedInventory = targetInventory;
        ResetInputValues();

        if (chestInventoryUI != null)
        {
            chestInventoryUI.inventory = targetInventory;
            chestInventoryUI.gameObject.SetActive(true);
            chestInventoryUI.RefreshAllUI();
        }

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);
        if (inventoryUI != null) inventoryUI.RefreshAllUI();

        ToggleCursorAndHUD(false);
    }

    public void CloseInventory()
    {
        // 인벤토리를 닫을 때 손에 든 아이템이 있다면 월드로 드롭!
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.DropMouseCarriageItem();
        }

        isInventoryOpen = false;
        currentOpenedInventory = null;

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
        if (chestInventoryUI != null) chestInventoryUI.gameObject.SetActive(false);
        if (promptText != null) promptText.gameObject.SetActive(false);

        ToggleCursorAndHUD(true);
    }

    private void ResetInputValues()
    {
        moveInput = Vector2.zero;
        mouseInput = Vector2.zero;
        isFiringPressed = false;
        if (gun != null) gun.SetFiringPressed(false);
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); 
    }

    private void ToggleCursorAndHUD(bool gameplayMode)
    {
        if (crosshairUI != null) crosshairUI.SetActive(gameplayMode);
        
        if (gameplayMode)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void HandleHotbarInput()
    {
        // 🔥 이제 다른 곳을 참조하지 않고, 본인 인스펙터에 적힌 hotbarSlotCount를 직접 기준으로 삼습니다!
        int currentHotbarSize = hotbarSlotCount; 

        // 1. 숫자키 감지: 설정된 핫바 크기만큼만 루프 구동
        for (int i = 0; i < currentHotbarSize; i++)
        {
            if (i >= 9) break; // 표준 키보드 숫자키(1~9) 상한선 안전장치

            KeyCode alphaKey = KeyCode.Alpha1 + i;
            KeyCode keypadKey = KeyCode.Keypad1 + i;

            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                currentHotbarIndex = i; // 0 ~ (핫바크기 - 1) 사이로 자동 매핑
                OnHotbarIndexChanged();
                break;
            }
        }

        // 2. 마우스 휠 스크롤 감지: 유동적인 크기 안에서 유기적으로 회전(Wrap-around)
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (scroll > 0) currentHotbarIndex--;
            else if (scroll < 0) currentHotbarIndex++;

            if (currentHotbarIndex < 0) 
            {
                currentHotbarIndex = currentHotbarSize - 1;
            }
            if (currentHotbarIndex >= currentHotbarSize) 
            {
                currentHotbarIndex = 0;
            }

            OnHotbarIndexChanged();
        }
    }

    private void OnHotbarIndexChanged()
    {
        if (HotbarUI.Instance != null) 
            HotbarUI.Instance.RefreshHotbar();

        if (InventoryManager.Instance != null && playerInventory != null) 
            InventoryManager.Instance.CheckWeaponEquip(playerInventory);
    }
    private void DropActiveHotbarItem()
    {
        int currentSlot = currentHotbarIndex; // 현재 활성화된 핫바 인덱스

        if (playerInventory == null || playerInventory.slots.Length <= currentSlot) return;

        ItemStack hotbarSlot = playerInventory.slots[currentSlot];
        if (hotbarSlot == null || hotbarSlot.item == null || hotbarSlot.amount <= 0) return;

        // 버릴 아이템 정보 캐싱
        ItemDataSO item = hotbarSlot.item;

        // ====================================================================
        // 🔥 [수정] 프리팹 없이 코드로 3D 드롭 아이템 오브젝트 실시간 동적 생성
        // ====================================================================
        
        // 1. 플레이어 위치 기준 정면 1.5m 앞, 약간 위쪽을 스폰 위치로 지정
        Vector3 spawnPos = transform.position + playerCamera.forward * 1.5f + Vector3.up * 0.5f;

        // 2. 빈 게임 오브젝트를 동적 생성하고 이름 부여
        GameObject dropObj = new($"Dropped_{item.name}");
        dropObj.transform.position = spawnPos;

        // 레이어를 "Interactable"로 설정 (InventoryManager와 동일)
        int interactableLayerIndex = LayerMask.NameToLayer("Interactable");
        if (interactableLayerIndex != -1)
        {
            dropObj.layer = interactableLayerIndex;
        }
        else
        {
            Debug.LogWarning("[레이어 경고] 프로젝트에 'Interactable' 레이어가 존재하지 않습니다.");
        }

        // 3. 물리(Rigidbody) 및 충돌체(BoxCollider) 추가
        Rigidbody rb = dropObj.AddComponent<Rigidbody>();
        BoxCollider col = dropObj.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.4f, 0.4f); // 적당한 크기의 히트박스

        // 4. 비주얼(마인크래프트처럼 둥둥 떠서 도는 아이콘) 자식 생성
        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(dropObj.transform);
        visualObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon; // 아이템 고유 아이콘 매핑
        visualObj.AddComponent<ItemRotator>(); // 빙글빙글 회전 컴포넌트 추가

        // 5. 상호작용 스크립트 추가 및 데이터 주입 (Q키는 1개씩 던지므로 수량은 1)
        DroppedItem droppedScript = dropObj.AddComponent<DroppedItem>();
        droppedScript.Setup(item, 1, sr);

        // 6. 플레이어가 바라보는 정면 방향으로 툭 던지는 물리적 힘(Impulse) 부여
        rb.AddForce(playerCamera.forward * 3.5f, ForceMode.Impulse);

        Debug.Log($"[핫바 드롭] {item.name} 아이템을 1개 던졌습니다.");

        // ====================================================================
        // 7. 백엔드 데이터 차감 및 UI 새로고침
        // ====================================================================
        hotbarSlot.amount--;
        if (hotbarSlot.amount <= 0)
        {
            playerInventory.slots[currentSlot] = null;
        }

        // InventoryManager에 만들어 두신 만능 UI/무기 통합 갱신 함수를 호출합니다!
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RefreshAllGameUIs(playerInventory);
        }
    }

    #endregion
}