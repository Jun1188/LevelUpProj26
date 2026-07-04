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
        int currentSlot = currentHotbarIndex; 
        if (playerInventory == null || playerInventory.slots.Length <= currentSlot) return; 

        ItemStack hotbarSlot = playerInventory.slots[currentSlot]; 
        if (hotbarSlot == null || hotbarSlot.item == null || hotbarSlot.amount <= 0) return; 

        ItemDataSO item = hotbarSlot.item; 
        Vector3 spawnPos = transform.position + playerCamera.forward * 1.5f + Vector3.up * 0.5f; 

        // 1. 루트 오브젝트 생성 및 레이어 설정
        GameObject dropObj = new($"Dropped_{item.name}"); 
        dropObj.transform.position = spawnPos; 

        int interactableLayerIndex = LayerMask.NameToLayer("Interactable"); 
        if (interactableLayerIndex != -1) { 
            dropObj.layer = interactableLayerIndex; 
        } 

        // 2. 물리(Rigidbody) 설정
        Rigidbody itemRb = dropObj.AddComponent<Rigidbody>(); 
        itemRb.useGravity = true; 
        itemRb.constraints = RigidbodyConstraints.FreezeRotation; 

        // ====================================================================
        // [핵심] 한 오브젝트에 콜라이더 2개 모두 생성!
        // ====================================================================
        // ① 바닥 충돌용 고체 콜라이더 (isTrigger = false)
        BoxCollider solidCol = dropObj.AddComponent<BoxCollider>(); 
        solidCol.size = new Vector3(0.3f, 0.3f, 0.3f); 
        solidCol.isTrigger = false; 

        // ② 플레이어 획득 감지용 센서 콜라이더 (isTrigger = true)
        BoxCollider triggerCol = dropObj.AddComponent<BoxCollider>(); 
        triggerCol.size = new Vector3(1.5f, 1.5f, 1.5f); 
        triggerCol.isTrigger = true; 
        // ====================================================================

        // 3. 비주얼(둥둥 떠서 도는 아이콘) 자식 생성 (얘는 이제 순수 그래픽 역할만)
        GameObject visualObj = new("Visual");
        visualObj.transform.SetParent(dropObj.transform);
        visualObj.transform.localPosition = Vector3.zero; 
        visualObj.layer = dropObj.layer; 

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>(); 
        sr.sprite = item.icon; 
        visualObj.AddComponent<ItemRotator>(); 

        // 4. 상호작용 데이터 주입 및 물리 힘 전달
        DroppedItem droppedScript = dropObj.AddComponent<DroppedItem>(); 
        droppedScript.Setup(item, 1, sr); 

        itemRb.AddForce(playerCamera.forward * 3.5f, ForceMode.Impulse); 
        Debug.Log($"[핫바 드롭] {item.name} 아이템을 1개 던졌습니다."); 

        // 5. 인벤토리 데이터 차감 및 UI 새로고침
        hotbarSlot.amount--; 
        if (hotbarSlot.amount <= 0) { 
            playerInventory.slots[currentSlot] = null; 
        } 

        if (InventoryManager.Instance != null) { 
            InventoryManager.Instance.RefreshAllGameUIs(playerInventory); 
        } 
    }

    #endregion
}