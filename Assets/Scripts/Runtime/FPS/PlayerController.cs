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
    [Header("Hotbar Settings (핫바 시스템)")]
    public int hotbarSlotCount = 5;       // 핫바 칸수 (1~5번 슬롯)
    private int currentHotbarIndex = 0;   // 현재 선택된 핫바 인덱스 (0 ~ hotbarSlotCount-1)

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
    public int CurrentHotbarIndex => currentHotbarIndex;
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
        // [방어 코드] 핫바 UI가 아직 생성되지 않았거나 없는 예외 상황을 위해 기본값 5 또는 9 설정
        int currentHotbarSize = 9; 

        if (HotbarUI.Instance != null)
        {
            currentHotbarSize = HotbarUI.Instance.HotbarSlotCount;
        }

        // 1. 숫자키 감지: 핫바가 동적으로 설정한 크기(currentHotbarSize)만큼만 루프를 돕니다.
        for (int i = 0; i < currentHotbarSize; i++)
        {
            // 표준 키보드 숫자키는 최대 9번(Alpha9)까지 지원하므로, 루프가 9회를 넘지 않도록 안전장치
            if (i >= 9) break; 

            KeyCode alphaKey = KeyCode.Alpha1 + i;
            KeyCode keypadKey = KeyCode.Keypad1 + i;

            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                currentHotbarIndex = i; // 0 ~ (핫바크기 - 1) 사이로 유기적 매핑
                OnHotbarIndexChanged();
                break;
            }
        }

        // 2. 마우스 휠 스크롤 감지: 유동적인 핫바 크기를 반영하여 Wrap-around(회전) 처리
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (scroll > 0) currentHotbarIndex--;
            else if (scroll < 0) currentHotbarIndex++;

            // 하드코딩된 0~8 범위를 지우고, 현재 핫바 사이즈를 대입하여 유기적으로 연산합니다.
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
    // 핫바 번호가 바뀌었을 때 실행할 동기화 헬퍼 함수
    private void OnHotbarIndexChanged()
    {

        if (HotbarUI.Instance != null) 
            HotbarUI.Instance.RefreshHotbar();

        if (InventoryManager.Instance != null && playerInventory != null) 
            InventoryManager.Instance.CheckWeaponEquip(playerInventory);
    }
    private void DropActiveHotbarItem()
    {
        // 현재 선택된 핫바의 인덱스 번호 (0 ~ 4)를 가져옵니다.
        // (★혹시 휠 스크롤이나 숫자로 제어 중인 변수명이 activeSlotIndex가 아니라 다른 이름이라면 그 이름으로 수정해 주세요!)
        int currentSlot = currentHotbarIndex; 

        if (playerInventory == null || playerInventory.slots.Length <= currentSlot) return;

        ItemStack hotbarSlot = playerInventory.slots[currentSlot];
        if (hotbarSlot == null || hotbarSlot.item == null || hotbarSlot.amount <= 0) return;

        // 1. 월드(3D 공간)에 버려진 아이템 프리팹 스폰하기
        // 플레이어 시선 정면 바닥 앞쪽공간 위치 계산
        Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
        
        // [기획 팁] 만약 팀원들이 만든 '필드 드롭 아이템 물리 스크립트'나 프리팹 오브젝트가 있다면 
        // 런타임에 Instantiate하고 해당 아이템 정보를 주입해주면 완벽합니다!
        // 예: GameObject droppedCube = Instantiate(hotbarSlot.item.dropPrefab, dropPosition, Quaternion.identity);
        
        Debug.Log($"[월드 드롭] {hotbarSlot.item.name} 아이템을 1개 떨어뜨렸습니다.");

        // 2. 백엔드 데이터 개수 1개 차감 처리
        hotbarSlot.amount--;
        if (hotbarSlot.amount <= 0)
        {
            playerInventory.slots[currentSlot] = null;
        }

        // 3. 아이템을 버렸으니 실시간으로 핫바 UI 새로고침 진행
        InventoryUI[] allActiveUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
        foreach (InventoryUI ui in allActiveUIs)
        {
            if (ui.gameObject.activeSelf) ui.RefreshAllUI();
        }

        // 4. 버린 아이템이 무기였다면 손에서 내려놓아야 하므로 무기 장착 상태 즉시 업데이트
        // (InventoryManager 내부의 CheckWeaponEquip를 public으로 열어두셨다면 아래처럼 호출이 가능합니다)
        if (InventoryManager.Instance != null)
        {
            // 드롭 후 들고 있는 칸이 빈칸이 되었으니 자동으로 무기가 해제되거나 변경됩니다.
            InventoryManager.Instance.HandleSlotLeftClick(playerInventory, currentSlot, null); 
            // 만약 클릭 유도가 귀찮다면 InventoryManager의 CheckWeaponEquip(playerInventory)을 public으로 바꾸고 직접 호출하셔도 됩니다!
        }
    }

    #endregion
}