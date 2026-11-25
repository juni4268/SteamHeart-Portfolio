using UnityEngine;

// The scene AudioSource supplies the clip and mixer route; GameMusicPlayer owns playback.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class SceneMusic : MonoBehaviour
{
    [Tooltip("사망 시 이 씬의 BGM을 페이드아웃할 보스입니다. 일반 맵은 비워둡니다.")]
    [SerializeField] private BossHealth fadeOutOnDeath;

    public BossHealth FadeOutOnDeath => fadeOutOnDeath;
}
