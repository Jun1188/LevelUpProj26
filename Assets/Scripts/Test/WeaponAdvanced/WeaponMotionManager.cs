using UnityEngine;

public class WeaponMotionManager : MonoBehaviour
{
    private Vector3 _originPos;
    private Quaternion _originRot;
    [SerializeField]
    private IWeaponMotionModule[] _modules;

    private void Awake()
    {
        _originPos = transform.localPosition;
        _originRot = transform.localRotation;

        _modules = GetComponents<IWeaponMotionModule>();
     
    }

    private void LateUpdate()
    {
        Vector3 finalPos = _originPos;
        Quaternion finalRot = _originRot;

        // 각 모듈(컴포넌트)들이 독립적으로 계산한 오프셋을 모두 합산
        foreach (var module in _modules)
        {
            finalPos += module.PositionOffset;
            finalRot *= module.RotationOffset;
            
        }

        transform.localPosition = finalPos;
        transform.localRotation = finalRot;
    }
}