using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : Entity
{
    [Header("Gun Settings")]
    public Gun gun;
    private bool isFiringPressed = false;
    
    [Header("Recoil Control (반동 제어 시스템)")]
    public float maxRecoilVelocity = 12f;      
    public float recoilDecaySpeed = 20f;       
    
    [Space(10)]
    [Header("🎯 Recoil Distance Limit (반동 거리 제한 옵션)")]
    public float maxRecoilDistance = 1.2f;     
    public float recoilDistanceRecoverySpeed = 6f; 
    
    private Vector3 activeRecoilVelocity;      
    private float currentRecoilDistance = 0f;  

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
    public Inventory playerInventory; // 플레이어의 고유 인벤토리 백엔드
    private bool isInventoryOpen = false;
    public ItemDataSO debugTestItem;
    private int currentHotbarIndex = 0; 
    [Header("Weapon Models (Visual)")]
    // 인스펙터에서 플레이어 손 위치에 자식으로 넣어둔 총 모델들을 배열로 연결합니다.
    // 예: 0번 피스톨 모델, 1번 SMG 모델, 2번 스나이퍼 모델 등
    public GameObject[] weaponModels;

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;

    protected override void Start()
    {
        base.Start();
        SetCursorState(false);
        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
        UpdateEquippedWeapon();
    }

    protected override void Update()
    {
        base.Update();
        HandleCamera();
        HandleJump();
        HandleAutomaticFire();

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

    private void UpdateEquippedWeapon()
    {
        if (playerInventory == null || gun == null) return;

        // 현재 선택된 슬롯의 아이템 가져오기
        ItemStack currentStack = playerInventory.slots[currentHotbarIndex];

        // 슬롯이 비어있지 않고, 그 아이템이 '무기(WeaponItemSO)' 라면?
        if (currentStack != null && currentStack.item is WeaponItemSO weaponItem)
        {
            // 1. 총 기능 활성화 및 데이터 주입 (능력치, 총알 종류 변경)
            gun.gameObject.SetActive(true);
            gun.SetupGunData(weaponItem.gunData); 

            // 2. 시각적 이미지/모델 변경
            UpdateWeaponVisuals(weaponItem.gunData.weaponType);
        }
        else
        {
            // 무기가 아니거나 빈 슬롯이면 총을 안 들고 있게 만듦 맨손 처리
            gun.gameObject.SetActive(false);
            DisableAllWeaponModels();
        }
    }
    private void ChangeHotbarSlot(int newIndex)
    {
        if (currentHotbarIndex == newIndex) return;
        
        currentHotbarIndex = newIndex;
        Debug.Log($"현재 단축키 {currentHotbarIndex + 1}번 슬롯 선택됨");
        
        // 슬롯이 바뀌었으니 손에 든 무기 데이터와 외형을 업데이트
        UpdateEquippedWeapon();
    }
    // 무기 종류(Enum)에 따라 손에 든 3D 모델을 켜고 끄는 로직
    private void UpdateWeaponVisuals(WeaponType type)
    {
        DisableAllWeaponModels();

        // 열거형(Enum) 순서대로 모델 오브젝트를 매칭해 사전에 인스펙터에 등록해두면 편합니다.
        int modelIndex = (int)type; 
        if (modelIndex >= 0 && modelIndex < weaponModels.Length && weaponModels[modelIndex] != null)
        {
            weaponModels[modelIndex].SetActive(true);
        }
    }
    private void DisableAllWeaponModels()
    {
        foreach (var model in weaponModels)
        {
            if (model != null) model.SetActive(false);
        }
    }

    // [건 스왑 핵심 기능] 무기를 갈아 끼우는 함수
    public void EquipWeapon(WeaponItemSO weaponItem)
    {
        if (gun != null && weaponItem != null && weaponItem.gunData != null)
        {
            if (gun.gunData == weaponItem.gunData) return; // 이미 들고 있는 총과 같다면 연산 무시
            gun.ChangeGunData(weaponItem.gunData);
        }
    }

    // 무기를 빼는 함수
    public void UnequipWeapon()
    {
        if (gun != null && gun.gunData != null)
        {
            gun.ClearGunData();
        }
    }

    private void HandleAutomaticFire()
    {
        // 총이 없거나, 장착된 총기 데이터(맨손)가 없으면 발사 처리 안 함 (Null 에러 원천 차단)
        if (gun == null || gun.gunData == null) return;

        if (isFiringPressed && gun.gunData.isAutomatic && !isInventoryOpen)
        {
            gun.Fire();
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

        activeRecoilVelocity = Vector3.MoveTowards(activeRecoilVelocity, Vector3.zero, Time.fixedDeltaTime * recoilDecaySpeed);

        if (activeRecoilVelocity.sqrMagnitude < 0.001f)
        {
            activeRecoilVelocity = Vector3.zero;
            currentRecoilDistance = Mathf.MoveTowards(currentRecoilDistance, 0f, Time.fixedDeltaTime * recoilDistanceRecoverySpeed);
        }

        float frameRecoilMovement = activeRecoilVelocity.magnitude * Time.fixedDeltaTime;
        
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
                activeRecoilVelocity = activeRecoilVelocity.normalized * (allowedMovement / Time.fixedDeltaTime);
                frameRecoilMovement = allowedMovement;
            }
        }
        
        currentRecoilDistance += frameRecoilMovement;

        Vector3 moveDirection = transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y)).normalized;
        Vector3 moveVelocity = moveDirection * moveSpeed;

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

    // Gun.cs의 규격에 딱 맞춘 유기적 통합 반동 리시버
    public void AddRecoil(Vector3 recoilDirection, float cameraRecoil, float bodyRecoil)
    {
        // 1. 화면 위로 올리는 수직 반동 (수치 보정 0.5f 제거 혹은 유지 자유)
        cameraRotationX -= cameraRecoil; 
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);
        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);

        // 2. 몸통 뒤로 밀림 처리 (bodyRecoil 반영)
        float currentRecoilSpeed = Vector3.Dot(rb.linearVelocity, recoilDirection);

        if (currentRecoilSpeed < maxRecoilVelocity)
        {
            rb.AddForce(recoilDirection * bodyRecoil, ForceMode.Impulse);
        }
    }

    public void OnFire(InputValue value)
    {
        if (isInventoryOpen || gun == null || !gun.gameObject.activeInHierarchy) return;
        
        isFiringPressed = value.isPressed;
        
        // ⭐️ 중요: 연사 무기 작동을 위해 Gun 컴포넌트의 상태도 동기화합니다.
        gun.SetFiringPressed(isFiringPressed);
        
        // 단발형 무기(Pistol, Sniper 등) 작동 보장용
        if (isFiringPressed && gun.gunData != null && !gun.gunData.isAutomatic)
        {
            gun.Fire();
        }
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

    // PlayerController.cs 내부의 치트 함수를 아래 내용으로 덮어써 주세요.
    private void TriggerDebugItemInject()
    {
        if (playerInventory != null && debugTestItem != null)
        {
            // 🛠️ 테스트하고 싶은 슬롯 번호 지정 (0 = 첫 번째 무기 슬롯 / 1 = 두 번째 슬롯)
            int targetIndex = 1; 
            int injectAmount = 10; // 한 번 누를 때마다 10개씩 스폰

            ItemStack targetSlot = playerInventory.slots[targetIndex];

            // Case 1: 해당 슬롯이 완전히 비어있는 경우 -> 새로 생성
            if (targetSlot == null || targetSlot.item == null)
            {
                playerInventory.slots[targetIndex] = new ItemStack(debugTestItem, injectAmount);
                Debug.Log($"[치트] {targetIndex}번 슬롯에 {debugTestItem.name} {injectAmount}개 새로 생성!");
            }
            // Case 2: 이미 같은 아이템이 들어있는 경우 -> 합치기(Stack) 작동 테스트
            else if (targetSlot.item == debugTestItem)
            {
                int canStackAmount = targetSlot.maxStackSize - targetSlot.amount; // 들어갈 수 있는 남은 공간
                int actualAdd = Mathf.Min(canStackAmount, injectAmount);         // 남은 공간과 스폰할 개수 중 최소값

                if (actualAdd > 0)
                {
                    targetSlot.amount += actualAdd;
                    Debug.Log($" {targetIndex}번 슬롯의 기존 아이템과 합쳐짐! (+{actualAdd}개 / 현재: {targetSlot.amount}/{targetSlot.maxStackSize})");
                }
                else
                {
                    Debug.LogWarning($"[치트] {targetIndex}번 슬롯이 이미 최대 수량({targetSlot.maxStackSize}개)으로 가득 찼습니다!");
                }
            }
            // Case 3: 슬롯에 다른 종류의 아이템이 꽂혀있는 경우
            else
            {
                Debug.LogError($"[치트] {targetIndex}번 슬롯에 다른 아이템({targetSlot.item.name})이 있어 생성할 수 없습니다. 슬롯을 비워주세요!");
            }

            // ⭐️ 중요: 치트키로 백엔드 데이터를 강제로 바꿨으므로 화면의 UI를 새로고침 해줍니다.
            InventoryUI[] allActiveUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
            foreach (InventoryUI ui in allActiveUIs)
            {
                if (ui.gameObject.activeSelf) ui.RefreshAllUI();
            }
        }
    } 
}