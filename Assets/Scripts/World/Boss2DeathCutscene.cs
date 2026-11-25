using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossHealth), typeof(Boss2Controller), typeof(VideoCutscene))]
public sealed class Boss2DeathCutscene : MonoBehaviour
{
    [SerializeField] private Boss2DeathSequence deathSequence;
    [Tooltip("DeadCore가 플레이어에게 닿아 사라진 뒤 동영상까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float postCoreDelay = 2f;
    private BossHealth health;
    private Boss2Controller boss;
    private VideoCutscene cutscene;

    private void Awake()
    {
        health = GetComponent<BossHealth>();
        boss = GetComponent<Boss2Controller>();
        cutscene = GetComponent<VideoCutscene>();
    }

    private void OnEnable() => health.Died += OnDied;
    private void OnDisable() => health.Died -= OnDied;

    private void OnDied() => StartCoroutine(WaitForPatterns());

    private IEnumerator WaitForPatterns()
    {
        while (boss.Executions.Count > 0) yield return null;
        if (deathSequence == null)
            throw new System.InvalidOperationException("Assign the Boss2 death sequence before playing its cutscene.");
        yield return deathSequence.Play();
        float elapsed = 0f;
        while (elapsed < postCoreDelay)
        {
            if (!HitStopManager.IsActive && Time.timeScale > 0f) elapsed += Time.deltaTime;
            yield return null;
        }
        cutscene.Play();
    }
}
