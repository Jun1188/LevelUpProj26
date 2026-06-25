using UnityEngine;

public class FactoryBootstrap : MonoBehaviour
{
    void Awake()
    {
        var systems = new GameObject("FactorySystems");
        systems.AddComponent<GridRegistry>();
        systems.AddComponent<BuildingGraph>();
        systems.AddComponent<SimulationSystem>();
        systems.AddComponent<BeltSegmentManager>();
        DontDestroyOnLoad(systems);
    }
}