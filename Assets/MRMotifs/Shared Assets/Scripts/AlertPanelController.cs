
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
        private bool hasPlayedOnce = false;

        private void Reset()
        {
            TryGetComponent(out panel);
        }

        private void Awake()
        {
            panel = GetComponent<WorldSpacePanel>();
			// Respect your prefab's authored RectTransform size and scale
			panel.SetEnforceSizeInMeters(false);
			// Disable billboarding entirely per request
			panel.ConfigureBillboarding(false, 0f);
            panel.ConfigureDistance(0.88f);
            // Alert controls its own appear; prevent auto animation so we can place first, then slow fade.
            panel.SetAutoPlayOnEnable(false);
        }

        private void OnEnable()
        {
            // Only play the sequence once to prevent looping
            if (!hasPlayedOnce)
            {
                hasPlayedOnce = true;
                StartCoroutine(PlaySequence());
            }
        }

		private IEnumerator PlaySequence()
        {
            // Wait for a camera (panel will wire up its own camera in Awake)
            var canvas = GetComponent<Canvas>();
            while ((canvas && canvas.worldCamera == null) && Camera.main == null)
            {
                yield return null;
            }

			// Place at final pose using the panel helper so yaw/position match the main panel
			panel.PlaceAtFinalPose();

			var canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;

			// Fade in over assignment duration (alert requirement says 7s fade-in)
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
			panel.PlayDisappear(() => {
                gameObject.SetActive(false);
                // Reset so it can play again if manually re-enabled
                hasPlayedOnce = false;
            });
        }
    }
}


