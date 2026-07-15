using UnityEngine;

public class WeaponKickback : MonoBehaviour, IWeaponMotionModule
{
    [Tooltip("반동이 적용되는 튕김 속도 (클수록 딱딱함)")]
    public float snapSpeed = 25f;
    [Tooltip("원래 자리로 돌아오는 복구 속도")]
    public float returnSpeed = 15f;

    private Vector3 _targetPos, _targetRot;

    public Vector3 PositionOffset { get; private set; }
    public Quaternion RotationOffset { get; private set; } = Quaternion.identity;

    // ★ 수정됨: WeaponBase에서 무기 고유의 반동값을 전달받음
    public void Fire(float zAmount, Vector3 rotAmount, bool isAiming)
    {
        float mult = isAiming ? 0.5f : 1f;

        // WeaponBase에서 넘겨준 무기별 고유 반동값 적용
        _targetPos += new Vector3(0, 0, -zAmount) * mult;
        _targetRot += new Vector3(-rotAmount.x, Random.Range(-rotAmount.y, rotAmount.y), 0) * mult;
    }

    private void Update()
    {
        _targetPos = Vector3.Lerp(_targetPos, Vector3.zero, Time.deltaTime * returnSpeed);
        _targetRot = Vector3.Lerp(_targetRot, Vector3.zero, Time.deltaTime * returnSpeed);

        PositionOffset = Vector3.Slerp(PositionOffset, _targetPos, Time.deltaTime * snapSpeed);
        RotationOffset = Quaternion.Slerp(RotationOffset, Quaternion.Euler(_targetRot), Time.deltaTime * snapSpeed);
    }
}