using UnityEngine;

public interface IWeaponMotionModule
{
    Vector3 PositionOffset { get; }
    Quaternion RotationOffset { get; }
}