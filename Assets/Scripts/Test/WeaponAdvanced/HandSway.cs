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
/// ■ 설치
///   handPosition GameObject에 이 컴포넌트를 Add Component.
///   CharacterController는 부모 계층에서 자동 탐색합니다.
///   handPosition의 localPosition/Rotation을 씬에서 원하는 위치로 맞춘 뒤
///   Context Menu → "Reset Sway Origin" 으로 기준점을 저장하세요.
/// </summary>
public class HandSway : MonoBehaviour
{
    // ─── Position Sway ───────────────────────────────────────────────
    [Header("Position Sway")]
    [Tooltip("마우스 이동에 따른 최대 위치 흔들림 (m). 권장: 0.04~0.08")]
    public float posSwayAmount = 0.06f;

    [Tooltip("위치가 원점으로 돌아오는 속도. 클수록 빠름. 권장: 6~12")]
    public float posSwaySmooth = 8f;

    // ─── Rotation Sway ───────────────────────────────────────────────
    [Header("Rotation Sway")]
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
    private Vector3    _originPos;
    private Quaternion _originRot;

    private Vector2 _smoothedDelta;   // EMA 적용된 마우스 델타
    private float   _bobTimer;        // sinusoidal 누적 시간

    private CharacterController _cc;

    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originPos = transform.localPosition;
        _originRot = transform.localRotation;
        _cc        = GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        SmoothMouseInput();

        // bob 오프셋을 먼저 계산해 sway target에 합산
        Vector3 bobOffset = ComputeBobOffset();

        ApplyPositionSway(bobOffset);
        ApplyRotationSway();
    }

    // ─── 마우스 입력 EMA ──────────────────────────────────────────────
    // frame-independent EMA:
    //   t = 1 - (1 - alpha)^(deltaTime * 60)
    //
    //   dt = 1/60  → t = alpha          (60fps 기준치 그대로)
    //   dt = 1/30  → t > alpha          (저프레임: 더 빠르게 반응해 동일 시간 응답 유지)
    //   dt = 1/144 → t < alpha          (고프레임: 더 느리게 반응해 동일 시간 응답 유지)
    //
    // Input.GetAxisRaw: Unity 내부 smoothing 없음 → 직접 제어
    private void SmoothMouseInput()
    {
        float rawX = Input.GetAxisRaw("Mouse X");
        float rawY = Input.GetAxisRaw("Mouse Y");

        float t = 1f - Mathf.Pow(1f - inputSmoothing, Time.deltaTime * 60f);
        _smoothedDelta = Vector2.Lerp(_smoothedDelta, new Vector2(rawX, rawY), t);
    }

    // ─── Bob 오프셋 계산 ──────────────────────────────────────────────
    // 반환값을 ApplyPositionSway의 targetPos에 더합니다.
    // 이 값을 Update 끝에 transform.localPosition += 으로 붙이면
    // ApplyPositionSway의 Lerp가 다음 프레임에 원점으로 되돌려버리므로 합산이 맞습니다.
    private Vector3 ComputeBobOffset()
    {
        if (!bobEnabled || _cc == null) return Vector3.zero;

        Vector3 v          = _cc.velocity;
        float   horizSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        bool    isMoving   = horizSpeed > 0.5f && _cc.isGrounded;

        if (isMoving)
        {
            // 빠를수록 bob 주기도 빨라짐 (walkSpeed=5 기준 speedFactor≈0.625)
            float speedFactor = Mathf.Clamp01(horizSpeed / 8f);
            _bobTimer += Time.deltaTime * bobFrequency * (1f + speedFactor);
        }
        else
        {
            // 정지 시 자연스럽게 0으로 수렴 → 갑작스러운 끊김 없음
            _bobTimer = Mathf.Lerp(_bobTimer, 0f, Time.deltaTime * bobResetSmooth);
        }

        float bx = Mathf.Sin(_bobTimer * Mathf.PI)      * bobAmountX;
        float by = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * bobAmountY;
        return new Vector3(bx, by, 0f);
    }

    // ─── Position Sway ───────────────────────────────────────────────
    private void ApplyPositionSway(Vector3 bobOffset)
    {
        // 마우스 오른쪽 → 손이 왼쪽으로 (음수) → 관성 느낌
        float swayX = Mathf.Clamp(-_smoothedDelta.x * posSwayAmount,
                                  -posSwayAmount, posSwayAmount);
        float swayY = Mathf.Clamp(-_smoothedDelta.y * posSwayAmount,
                                  -posSwayAmount, posSwayAmount);

        // bob을 targetPos에 포함시켜야 Lerp에 의해 상쇄되지 않음
        Vector3 targetPos = _originPos + new Vector3(swayX, swayY, 0f) + bobOffset;

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
        float tiltZ  = Mathf.Clamp( _smoothedDelta.x * rotSwayAmount,
                                   -rotSwayAmount, rotSwayAmount);
        // 마우스 위 → 손이 약간 위로 젖혀지는 느낌
        float pitchX = Mathf.Clamp(-_smoothedDelta.y * rotSwayAmount,
                                   -rotSwayAmount, rotSwayAmount);

        Quaternion targetRot = _originRot * Quaternion.Euler(pitchX, 0f, -tiltZ);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            Time.deltaTime * rotSwaySmooth
        );
    }

    // ─── Editor 유틸 ─────────────────────────────────────────────────
    /// <summary>
    /// Inspector에서 handPosition의 localPosition/Rotation을 원하는 위치로
    /// 맞춘 뒤 이 메서드를 호출해 기준점을 갱신하세요.
    /// </summary>
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
        posSwayAmount  = Mathf.Max(0f,   posSwayAmount);
        rotSwayAmount  = Mathf.Max(0f,   rotSwayAmount);
        bobFrequency   = Mathf.Max(0.1f, bobFrequency);
        posSwaySmooth  = Mathf.Max(0.1f, posSwaySmooth);
        rotSwaySmooth  = Mathf.Max(0.1f, rotSwaySmooth);
        bobResetSmooth = Mathf.Max(0.1f, bobResetSmooth);
    }
#endif
}
