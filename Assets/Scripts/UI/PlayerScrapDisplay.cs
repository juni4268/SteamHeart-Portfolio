using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class PlayerScrapDisplay : MonoBehaviour
{
    private Text label;

    private void Awake() => label = GetComponent<Text>();

    private void OnEnable()
    {
        PlayerPermanentProgress.ScrapChanged += UpdateScrap;
        UpdateScrap(PlayerPermanentProgress.Scrap);
    }

    private void OnDisable() => PlayerPermanentProgress.ScrapChanged -= UpdateScrap;

    private void UpdateScrap(int scrap) => label.text = $"{scrap:N0}";
}
