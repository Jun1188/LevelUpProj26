/// <summary>
/// 심 엔티티(Building)의 씬 표현.
/// 피격 HP/사망/타워 공격 등 실제 기능은 전부 Entities.Building(Entity 상속)으로
/// 통합되었다. 이 클래스는 PlacementBridge/FactoryBootstrap이 참조하는 타입명과
/// 프리팹의 컴포넌트 GUID를 유지하기 위한 얇은 껍데기로만 남아 있다.
/// (구 BuildingDamageable은 삭제됨 — Entities.Building.GetOrAttach 참고)
/// </summary>
public class BuildingView : Entities.Building
{
    // PlacementBridge가 배치 시 심(plain C# Building)을 연결하는 기존 계약 유지
    public Building Building
    {
        get => Sim;
        set => Sim = value;
    }
}
