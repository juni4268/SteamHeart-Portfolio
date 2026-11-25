using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawns one self-finishing spark per authored point when the body death motion starts.</summary>
[DisallowMultipleComponent]
public sealed class Boss1DeathSparkEmitter : MonoBehaviour
{
    [Tooltip("Animator로 한 번 재생한 뒤 스스로 제거되는 스파크 Prefab입니다.")]
    [SerializeField] private CombatAnimationEffect sparkPrefab;
    [Tooltip("사망 모션 시작 시 각 지점에서 한 번씩 동시에 생성합니다. 스파크는 지점의 움직임을 따라갑니다.")]
    [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();

    private readonly HashSet<Transform> spawnedPoints = new HashSet<Transform>();

    public void Play()
    {
        if (!isActiveAndEnabled || sparkPrefab == null) return;

        foreach (Transform point in spawnPoints)
        {
            if (point == null || !point.gameObject.activeInHierarchy || !spawnedPoints.Add(point)) continue;
            Instantiate(sparkPrefab, point, false);
        }
    }
}
