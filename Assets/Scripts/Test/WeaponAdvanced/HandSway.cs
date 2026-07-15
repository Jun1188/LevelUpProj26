using UnityEngine;

/// <summary>
/// handPosition GameObject에 부착하는 Weapon Sway 컴포넌트.
///
/// ■ 동작 방식
///   Input.GetAxisRaw로 raw 마우스 델타를 읽어
///   frame-independent EMA(Exponential Moving Average)로 직접 smoothing합니다.
///   → Input.GetAxis의 Unity 내부 smoothing을 우회하므로
///     저주사율 마우스/저프레임 환경에서도 feel이 일정합니다.
///
/// ■ Position Sway
///   마우스 이동 반대 방향으로 localPosition 이동 → 관성 느낌
///
/// ■ Rotation Sway
///   Tilt  (Z축) : 마우스X에 비례 → 손목 기울기
///   Pitch (X축) : 마우스Y에 비례 → 앞뒤 기울기
///
/// ■ Movement Bob
///   CharacterController 수평 속도로 이동 감지 → sinusoidal bob
///   bob 오프셋은 sway targetPos에 합산됩니다.
///   (별도 += 시 Lerp에 의해 다음 프레임 즉시 상쇄되는 문제 방지)
///
/// ■ Movement Sway (새로 추가됨)
///   캐릭터의 로컬 이동 속도(CharacterController.velocity)를 바탕으로 
///   무기가 이동 반대 방향으로 밀리거나 기울어지는 관성(Inertia)을 구현합니다.
///
/// ■ 설치
///   handPosition GameObject에 이 컴포넌트를 Add Component.
///   CharacterController는 부모 계층에서 자동 탐색합니다.
///   handPosition의 localPosition/Rotation을 씬에서 원하는 위치로 맞춘 뒤
///   Context Menu → "Reset Sway Origin" 으로 기준점을 저장하세요.
/// </summary>
public class HandSway : MonoBehaviour
{
    // ─── Position Sway ───────────────────────────────────────────────
    [Header("Position Sway (Mouse)")]
    [Tooltip("마우스 이동에 따른 최대 위치 흔들림 (m). 권장: 0.04~0.08")]
    public float posSwayAmount = 0.06f;

    [Tooltip("위치가 원점으로 돌아오는 속도. 클수록 빠름. 권장: 6~12")]
    public float posSwaySmooth = 8f;

    // ─── Rotation Sway ───────────────────────────────────────────────
    [Header("Rotation Sway (Mouse)")]
    [Tooltip("마우스 이동에 따른 최대 회전 흔들림 (도). 권장: 2~6")]
    public float rotSwayAmount = 4f;

    [Tooltip("회전이 원점으로 돌아오는 속도. 클수록 빠름. 권장: 6~12")]
    public float rotSwaySmooth = 8f;

    // ─── Movement Bob ─────────────────────────────────────────────────
    [Header("Movement Bob")]
    public bool bobEnabled = true;

    [Tooltip("Bob 기본 주기 (Hz). 이동 속도에 따라 자동 스케일됩니다. 권장: 1.5~2.5")]
    public float bobFrequency = 1.8f;

    [Tooltip("좌우 Bob 진폭 (m). 권장: 0.005~0.012")]
    public float bobAmountX = 0.008f;

    [Tooltip("상하 Bob 진폭 (m). 권장: 0.004~0.010")]
    public float bobAmountY = 0.006f;

    [Tooltip("정지 시 bobTimer를 0으로 감쇠시키는 속도. 권장: 8~14")]
    public float bobResetSmooth = 10f;

    // ─── Movement Sway (추가됨) ────────────────────────────────────────
    [Header("Movement Sway (Inertia)")]
    public bool moveSwayEnabled = true;

    [Tooltip("캐릭터 이동에 따른 위치 관성 최대치. (X: 좌우 밀림, Y: 상하 밀림, Z: 앞뒤 밀림)")]
    public Vector3 movePosSwayAmount = new Vector3(0.02f, 0.02f, 0.02f);

    [Tooltip("캐릭터 이동에 따른 회전 관성 최대치 (도). (X: 앞뒤 기울기, Y: 좌우 회전, Z: 좌우 기울기)")]
    public Vector3 moveRotSwayAmount = new Vector3(3f, 0f, 2f);

    [Tooltip("이동 관성이 적용/복구되는 속도. 권장: 4~8")]
    public float moveSwaySmooth = 6f;

    [Tooltip("속도 정규화를 위한 기준 최고 이동 속도. 이 속도에 도달할 때 관성 최대치가 적용됩니다.")]
    public float maxReferenceSpeed = 5f;

    // ─── Input Smoothing ──────────────────────────────────────────────
    [Header("Mouse Input Smoothing")]
    [Tooltip(
        "EMA 계수 (1/60s 프레임 기준).\n" +
        "작을수록 부드럽고 느린 반응 / 클수록 즉각적.\n" +
        "내부적으로 frame-independent 보정이 적용되므로\n" +
        "프레임률과 무관하게 동일한 feel을 유지합니다.\n" +
        "권장: 0.10~0.20"
    )]
    [Range(0.01f, 1f)]
    public float inputSmoothing = 0.15f;

    // ─── 내부 상태 ────────────────────────────────────────────────────
    private Vector3 _originPos;
    private Quaternion _originRot;

    private Vector2 _smoothedDelta;   // EMA 적용된 마우스 델타
    private float _bobTimer;        // sinusoidal 누적 시간

    // 이동 관성을 위한 내부 상태 변수 (추가됨)
    private Vector3 _smoothedMovePos;
    private Quaternion _smoothedMoveRot = Quaternion.identity;

    private bool _wasGrounded = true;
    private float _fallDistance = 0f; // 체공 시간 기록용

    private Rigidbody _rb;

    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originPos = transform.localPosition;
        _originRot = transform.localRotation;
        _rb = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        SmoothMouseInput();
        CalculateMovementSway(); // 캐릭터 이동 관성 연산 (추가됨)

        // bob 오프셋을 먼저 계산해 sway target에 합산
        Vector3 bobOffset = ComputeBobOffset();


        ApplyLandingBob(); // 착지 반동 추가
        ApplyPositionSway(bobOffset);
        ApplyRotationSway();
    }

    // ─── 마우스 입력 EMA ──────────────────────────────────────────────
    private void SmoothMouseInput()
    {
        float rawX = Input.GetAxisRaw("Mouse X");
        float rawY = Input.GetAxisRaw("Mouse Y");

        float t = 1f - Mathf.Pow(1f - inputSmoothing, Time.deltaTime * 60f);
        _smoothedDelta = Vector2.Lerp(_smoothedDelta, new Vector2(rawX, rawY), t);
    }

    // ─── 캐릭터 이동 관성 연산 (새로 추가됨) ──────────────────────────────────
    private void CalculateMovementSway()
    {
        if (!moveSwayEnabled || _rb == null)
        {
            _smoothedMovePos = Vector3.Lerp(_smoothedMovePos, Vector3.zero, Time.deltaTime * moveSwaySmooth);
            _smoothedMoveRot = Quaternion.Slerp(_smoothedMoveRot, Quaternion.identity, Time.deltaTime * moveSwaySmooth);
            return;
        }

        // 월드 속도를 카메라(부모) 기준의 로컬 속도로 변환하여 앞/뒤/좌/우 판단
        Transform refTransform = transform.parent != null ? transform.parent : transform;
        Vector3 localVelocity = refTransform.InverseTransformDirection(_rb.linearVelocity);

        // 최대 속도 기준으로 -1 ~ 1 사이의 값으로 정규화
        Vector3 moveInput = localVelocity / maxReferenceSpeed;
        moveInput.x = Mathf.Clamp(moveInput.x, -1f, 1f);
        moveInput.y = Mathf.Clamp(moveInput.y, -1f, 1f);
        moveInput.z = Mathf.Clamp(moveInput.z, -1f, 1f);

        // Position 관성 (이동 방향의 반대로 무기가 밀림)
        Vector3 targetMovePos = new Vector3(
            -moveInput.x * movePosSwayAmount.x,
            -moveInput.y * movePosSwayAmount.y,
            -moveInput.z * movePosSwayAmount.z
        );

        // Rotation 관성
        Vector3 targetMoveRotEuler = new Vector3(
            moveInput.z * moveRotSwayAmount.x,  // 전진 시 무기가 살짝 아래로(Pitch) 숙여짐
            -moveInput.x * moveRotSwayAmount.y, // 우측 이동 시 좌측으로(Yaw) 틀어짐
            -moveInput.x * moveRotSwayAmount.z  // 우측 이동 시 좌측으로(Roll) 기울어짐
        );

        // 스무딩 처리
        _smoothedMovePos = Vector3.Lerp(_smoothedMovePos, targetMovePos, Time.deltaTime * moveSwaySmooth);
        _smoothedMoveRot = Quaternion.Slerp(_smoothedMoveRot, Quaternion.Euler(targetMoveRotEuler), Time.deltaTime * moveSwaySmooth);
    }

    // ─── Bob 오프셋 계산 ──────────────────────────────────────────────
    private Vector3 ComputeBobOffset()
    {
        if (!bobEnabled || _rb == null) return Vector3.zero;

        Vector3 v = _rb.linearVelocity;
        float horizSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        bool isMoving = horizSpeed > 0.5f && Mathf.Abs(_rb.linearVelocity.y) < 0.001f;

        if (isMoving)
        {
            float speedFactor = Mathf.Clamp01(horizSpeed / 8f);
            _bobTimer += Time.deltaTime * bobFrequency * (1f + speedFactor);
        }
        else
        {
            _bobTimer = Mathf.Lerp(_bobTimer, 0f, Time.deltaTime * bobResetSmooth);
        }

        float bx = Mathf.Sin(_bobTimer * Mathf.PI) * bobAmountX;
        float by = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * bobAmountY;
        return new Vector3(bx, by, 0f);
    }

    // ─── Position Sway ───────────────────────────────────────────────
    private void ApplyPositionSway(Vector3 bobOffset)
    {
        // 마우스 오른쪽 → 손이 왼쪽으로 (음수) → 관성 느낌
        float swayX = Mathf.Clamp(-_smoothedDelta.x * posSwayAmount, -posSwayAmount, posSwayAmount);
        float swayY = Mathf.Clamp(-_smoothedDelta.y * posSwayAmount, -posSwayAmount, posSwayAmount);

        // 원본 로직에 새로 추가된 _smoothedMovePos(이동 관성) 합산
        Vector3 targetPos = _originPos + new Vector3(swayX, swayY, 0f) + bobOffset + _smoothedMovePos;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * posSwaySmooth
        );
    }

    // ─── Rotation Sway ───────────────────────────────────────────────
    private void ApplyRotationSway()
    {
        // 마우스 오른쪽 → Z+ 기울기 (손목이 오른쪽으로 기울어짐)
        float tiltZ = Mathf.Clamp(_smoothedDelta.x * rotSwayAmount, -rotSwayAmount, rotSwayAmount);
        // 마우스 위 → 손이 약간 위로 젖혀지는 느낌
        float pitchX = Mathf.Clamp(-_smoothedDelta.y * rotSwayAmount, -rotSwayAmount, rotSwayAmount);

        // 원본 로직에 새로 추가된 _smoothedMoveRot(이동 관성) 곱셈 적용
        Quaternion targetRot = _originRot * Quaternion.Euler(pitchX, 0f, -tiltZ) * _smoothedMoveRot;

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            Time.deltaTime * rotSwaySmooth
        );
    }

    private void ApplyLandingBob()
    {
        if (_rb == null) return;

        // 임의의 Ground Check (실제 PlayerController의 isGrounded를 가져오는 것이 더 정확함)
        bool isGrounded = Physics.Raycast(_rb.transform.position, Vector3.down, 1.2f);

        if (!isGrounded)
        {
            _fallDistance += Time.deltaTime; // 떠있는 시간 기록
        }
        else if (!_wasGrounded && isGrounded) // 공중에 있다가 방금 착지함!
        {
            if (_fallDistance > 0.2f) // 살짝 뜬 건 무시, 좀 높이서 떨어졌을 때만
            {
                // 아래로 강하게 찍히는 임펄스(충격) 적용
                _smoothedMovePos += new Vector3(0f, -0.15f, 0f); // 무기가 아래로 푹 꺼짐
                _smoothedMoveRot *= Quaternion.Euler(15f, 0f, 0f); // 무기가 앞으로 쏠림
            }
            _fallDistance = 0f;
        }

        _wasGrounded = isGrounded;
    }

    // ─── Editor 유틸 ─────────────────────────────────────────────────
    [ContextMenu("Reset Sway Origin to Current Transform")]
    public void ResetOrigin()
    {
        _originPos = transform.localPosition;
        _originRot = transform.localRotation;
        Debug.Log($"[HandSway] Origin reset → pos:{_originPos}  rot:{_originRot.eulerAngles}");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        posSwayAmount = Mathf.Max(0f, posSwayAmount);
        rotSwayAmount = Mathf.Max(0f, rotSwayAmount);
        bobFrequency = Mathf.Max(0.1f, bobFrequency);
        posSwaySmooth = Mathf.Max(0.1f, posSwaySmooth);
        rotSwaySmooth = Mathf.Max(0.1f, rotSwaySmooth);
        bobResetSmooth = Mathf.Max(0.1f, bobResetSmooth);
        moveSwaySmooth = Mathf.Max(0.1f, moveSwaySmooth); // (추가됨)
        maxReferenceSpeed = Mathf.Max(0.1f, maxReferenceSpeed); // (추가됨)
    }
#endif
}