
using System.Collections;
using UnityEngine;

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Specialized setup for the alert panel dimensions and behaviors.
    /// </summary>
    [RequireComponent(typeof(WorldSpacePanel))]
    public class AlertPanelController : MonoBehaviour
    {
        [Header("Alert Panel Sizing (meters)")]
        [SerializeField] private float widthMeters = 0.851f;
        [SerializeField] private float heightMeters = 0.138f;

        [Header("Timing")]
        [Tooltip("Seconds to stay visible after full fade in before fading out.")]
        [SerializeField] private float visibleHoldSeconds = 0.5f;

        [Tooltip("Seconds to fade in before the hold.")]
        [SerializeField] private float fadeInSeconds = 7.0f;

        private WorldSpacePanel panel;

        private void Reset()
        {
            TryGetComponent(out panel);
        }

        private void Awake()
        {
            panel = GetComponent<WorldSpacePanel>();
            panel.ConfigureSizeMeters(widthMeters, heightMeters);
            panel.ConfigureBillboarding(true, 1.5f);
            panel.ConfigureDistance(0.88f);
        }

        private void OnEnable()
        {
            // Override the panel's appear with the slower 7s fade-in requirement for alerts.
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            // Position at final pose and then run a slow manual fade in
            var cachedDuration = 0.3f;
            // Place at final pose first
            var cam = Camera.main ? Camera.main.transform : null;
            if (cam != null)
            {
                var forward = cam.forward; forward.y = 0f; forward.Normalize();
                transform.position = cam.position + forward * 0.88f;
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            var canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeInSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInSeconds);
                canvasGroup.alpha = t;
                yield return null;
            }
            canvasGroup.alpha = 1f;

            yield return new WaitForSeconds(visibleHoldSeconds);

            // Fade out over the default 0.3s using the WorldSpacePanel animation so it also Z- retreats
            panel.PlayDisappear(() => gameObject.SetActive(false));
        }
    }
}


