using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private EnemyHealth health;
    [Tooltip("체력바가 따라갈 월드 위치입니다. Scene에서 이 Transform을 머리 위로 이동해 조절합니다.")]
    [SerializeField] private Transform anchor;

    internal Transform Anchor => anchor;
    internal bool CanDisplay => isActiveAndEnabled && health != null && health.isActiveAndEnabled
        && !health.IsDead && health.CurrentHp > 0 && anchor != null;

    private void Awake()
    {
        if (health != null && anchor != null) return;
        Debug.LogError("EnemyHealthBar needs its health and anchor references.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (health != null) health.HealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= HandleHealthChanged;
        EnemyHealthBarDisplay.Hide(this);
    }

    private void HandleHealthChanged(int previous, int current, int maximum)
    {
        if (current <= 0 || !CanDisplay)
        {
            EnemyHealthBarDisplay.Hide(this);
            return;
        }
        EnemyHealthBarDisplay.Show(this, previous, current, maximum);
    }
}
