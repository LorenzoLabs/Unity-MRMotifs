// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Generic controller for a world-space UI panel that:
    /// - Positions itself at a fixed distance in front of the HMD at eye level
    /// - Optionally billboards around the Y-axis only
    /// - Plays linear fade/move animations on appear/disappear
    /// - Enforces canvas sizing in meters and removes any CanvasScaler
    /// </summary>
    public class WorldSpacePanel : MonoBehaviour
    {
        [Header("Sizing (meters)")]
        [SerializeField] private float widthMeters = 0.662f;
        [SerializeField] private float heightMeters = 0.442f;

        [Header("Placement")]
        [Tooltip("Distance in front of the user's camera (meters).")]
        [SerializeField] private float distanceMeters = 0.88f;
        [Tooltip("Additional distance used as the starting point for appear animation (meters).")]
        [SerializeField] private float appearFromExtraZ = 0.05f;

        [Header("Animation")]
        [SerializeField] private float animateDurationSeconds = 0.3f;

        [Header("Billboarding")]
        [Tooltip("Enable billboard facing the user around the Y axis only.")]
        [SerializeField] private bool billboardYawOnly = false;
        [Tooltip("Reorientation speed for billboarding (0..10).")]
        [Range(0f, 10f)]
        [SerializeField] private float billboardTurnSpeed = 2f;

        [Header("Layering")]
        [Tooltip("Layer to apply to this panel (e.g., UI_Passthrough) to exclude from post-processing.")]
        [SerializeField] private string layerName = "UI_Passthrough";

        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Camera mainCamera;
        private Coroutine runningAnim;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();

            if (!canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Enforce World Space canvas with explicit size in meters and no scaler
            if (!canvas)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler)
            {
                Destroy(scaler);
            }

            // Size in meters (Unity units). Fetch RectTransform AFTER ensuring Canvas,
            // because adding a Canvas converts Transform to RectTransform.
            rectTransform = GetComponent<RectTransform>();
            if (!rectTransform)
            {
                rectTransform = transform as RectTransform;
            }
            rectTransform.sizeDelta = new Vector2(widthMeters, heightMeters);
            rectTransform.localScale = Vector3.one; // 1 unit == 1 meter

            // Assign layer if it exists
            var layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                gameObject.layer = layerIndex;
            }

            // Camera
            mainCamera = Camera.main;
            if (!mainCamera)
            {
                StartCoroutine(FetchMainCamera());
            }
        }

        private IEnumerator FetchMainCamera()
        {
            while (!(mainCamera = Camera.main))
            {
                yield return null;
            }
            // Re-place when camera becomes available
            PlaceAtFinalPose();
        }

        private void OnEnable()
        {
            if (!mainCamera)
            {
                mainCamera = Camera.main;
            }

            // Start hidden and slightly farther away, then animate in
            PlaceForAppearStart();
            SetOpacity(0f);
            PlayAppear();
        }

        private void Update()
        {
            if (billboardYawOnly && mainCamera)
            {
                var toCam = mainCamera.transform.position - transform.position;
                toCam.y = 0f; // yaw only
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    var targetRot = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        Mathf.Clamp01(Time.deltaTime * billboardTurnSpeed));
                }
            }
        }

        public void ConfigureSizeMeters(float width, float height)
        {
            widthMeters = width;
            heightMeters = height;
            if (rectTransform)
            {
                rectTransform.sizeDelta = new Vector2(widthMeters, heightMeters);
            }
        }

        public void ConfigureBillboarding(bool enabled, float turnSpeed = 2f)
        {
            billboardYawOnly = enabled;
            billboardTurnSpeed = turnSpeed;
        }

        public void ConfigureDistance(float distance)
        {
            distanceMeters = distance;
        }

        public void PlayAppear()
        {
            if (runningAnim != null) StopCoroutine(runningAnim);
            runningAnim = StartCoroutine(AnimateAppear());
        }

        public void PlayDisappear(System.Action onComplete = null)
        {
            if (runningAnim != null) StopCoroutine(runningAnim);
            runningAnim = StartCoroutine(AnimateDisappear(onComplete));
        }

        private void PlaceForAppearStart()
        {
            if (!mainCamera) return;
            var cam = mainCamera.transform;
            var forward = cam.forward;
            forward.y = 0f; // eye-level alignment
            forward.Normalize();

            var targetPos = cam.position + forward * (distanceMeters + appearFromExtraZ);
            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void PlaceAtFinalPose()
        {
            if (!mainCamera) return;
            var cam = mainCamera.transform;
            var forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();
            var targetPos = cam.position + forward * distanceMeters;
            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private IEnumerator AnimateAppear()
        {
            if (!mainCamera)
            {
                PlaceForAppearStart();
            }

            var cam = mainCamera.transform;
            var forward = cam.forward; forward.y = 0f; forward.Normalize();

            var startPos = cam.position + forward * (distanceMeters + appearFromExtraZ);
            var endPos = cam.position + forward * distanceMeters;

            float elapsed = 0f;
            while (elapsed < animateDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animateDurationSeconds);
                // Linear interpolation
                transform.position = Vector3.Lerp(startPos, endPos, t);
                SetOpacity(t);
                yield return null;
            }
            transform.position = endPos;
            SetOpacity(1f);
            runningAnim = null;
        }

        private IEnumerator AnimateDisappear(System.Action onComplete)
        {
            if (!mainCamera)
            {
                yield break;
            }

            var cam = mainCamera.transform;
            var forward = cam.forward; forward.y = 0f; forward.Normalize();

            var startPos = cam.position + forward * distanceMeters;
            var endPos = cam.position + forward * (distanceMeters + appearFromExtraZ);

            float elapsed = 0f;
            while (elapsed < animateDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animateDurationSeconds);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                SetOpacity(1f - t);
                yield return null;
            }
            transform.position = endPos;
            SetOpacity(0f);
            runningAnim = null;
            onComplete?.Invoke();
        }

        private void SetOpacity(float alpha)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = alpha;
                canvasGroup.interactable = alpha >= 1f;
                canvasGroup.blocksRaycasts = alpha > 0.99f;
            }
        }
    }
}


