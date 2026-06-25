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

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;

    protected override void Start()
    {
        base.Start();
        Cursor.lockState = CursorLockMode.Locked;
        
        rb ??= GetComponent<Rigidbody>();
    }

    protected override void Update()
    {
        base.Update();
        HandleCamera();
        HandleJump();
        HandleShooting();
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

        if (isFiringPressed && gun != null && !gun.gunData.isAutomatic)
        {
            gun.Fire();
        }
    }

    public void OnReload(InputValue value)
    {
        if (value.isPressed)
        {
            gun?.StartReload();
        }
    }
    private void HandleMovement()
    {
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

        if (isFiringPressed && gun.gunData.isAutomatic)
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
}