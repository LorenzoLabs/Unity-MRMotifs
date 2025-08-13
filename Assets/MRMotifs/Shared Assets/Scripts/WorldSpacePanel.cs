// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;

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
        [Tooltip("If true, the RectTransform will be forced to the width/height below in meters on Awake. Turn off to respect prefab size.")]
        [SerializeField] private bool enforceSizeInMeters = true;
        [SerializeField] private float widthMeters = 0.662f;
        [SerializeField] private float heightMeters = 0.442f;

        [Header("Placement")]
        [Tooltip("Distance in front of the user's camera (meters).")]
        [SerializeField] private float distanceMeters = 0.88f;
        [Tooltip("Additional distance used as the starting point for appear animation (meters).")]
        [SerializeField] private float appearFromExtraZ = 0.05f;

        [Header("Animation")]
        [SerializeField] private float animateDurationSeconds = 0.3f;
        public float AnimateDurationSeconds => animateDurationSeconds;

        [Header("Interaction")]
        [Tooltip("CanvasGroup becomes interactable/blocks raycasts when alpha >= this value")]
        [Range(0f, 1f)]
        [SerializeField] private float interactableAlphaThreshold = 1f;

        [Tooltip("If true, appear/disappear movement is computed relative to the panel's own transform instead of the camera. Useful for hand-placed panels.")]
        [SerializeField] private bool animateRelativeToSelf = false;

        [Header("Billboarding")]
        [Tooltip("Enable billboard facing the user around the Y axis only.")]
        [SerializeField] private bool billboardYawOnly = false;
        [Tooltip("Reorientation speed for billboarding (0..10).")]
        [Range(0f, 10f)]
        [SerializeField] private float billboardTurnSpeed = 2f;

        [Header("Layering")]
        [Tooltip("Layer to apply to this panel (e.g., UI_Passthrough) to exclude from post-processing.")]
        [SerializeField] private string layerName = "UI_Passthrough";

        [Header("Camera")]
        [Tooltip("Optional explicit camera to use instead of Camera.main (recommended for builds/XR rigs). Also assigned to Canvas.worldCamera for raycasting.")]
        [SerializeField] private Camera cameraOverride;

        [Header("Lifecycle")]
        [Tooltip("Automatically play appear animation on enable. Disable if another controller drives the appear sequence (e.g., AlertPanelController).")]
        [SerializeField] private bool autoPlayOnEnable = true;

        [Header("Placement Control")]
        [Tooltip("If true, this component will compute and set position/rotation in front of the camera. Turn off to preserve your authored transform.")]
        [SerializeField] private bool managePlacement = true;

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
            // Ensure a UI raycaster is present so world-space UI can receive pointer events
            var graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (!graphicRaycaster) graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            // Prefer Meta OVRRaycaster when Oculus/Meta Interaction SDK is present
            TryEnableOVRRaycaster(graphicRaycaster);
            // Optionally enable Input System TrackedDeviceRaycaster if the package exists (reflection-based)
            TryEnableTrackedDeviceRaycaster();
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
            if (enforceSizeInMeters)
            {
                rectTransform.sizeDelta = new Vector2(widthMeters, heightMeters);
            }
            // Respect authored scale unless explicitly requested elsewhere

            // Assign layer if it exists
            var layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                gameObject.layer = layerIndex;
            }

            // Camera
            mainCamera = cameraOverride != null ? cameraOverride : Camera.main;
            if (!mainCamera)
            {
                StartCoroutine(FetchMainCamera());
            }
            else
            {
                if (canvas) canvas.worldCamera = mainCamera;
            }
        }

        private IEnumerator FetchMainCamera()
        {
            while (!(mainCamera = (cameraOverride != null ? cameraOverride : Camera.main)))
            {
                yield return null;
            }
            // Re-place when camera becomes available
            if (canvas) canvas.worldCamera = mainCamera;
            if (managePlacement)
            {
                PlaceAtFinalPose();
            }
        }

        private void OnEnable()
        {
            if (!mainCamera)
            {
                mainCamera = cameraOverride != null ? cameraOverride : Camera.main;
            }

            // Start hidden; optionally animate in once camera is ready
            SetOpacity(0f);
            if (autoPlayOnEnable)
            {
                if (runningAnim != null) StopCoroutine(runningAnim);
                // Compute from the current camera forward at enable
                if (managePlacement)
                {
                    PlaceForAppearStart();
                }
                runningAnim = StartCoroutine(AnimateAppear());
            }
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

        private void TryEnableOVRRaycaster(GraphicRaycaster existingGraphicRaycaster)
        {
            // Look for a type named "OVRRaycaster" in loaded assemblies
            var ovrType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType("OVRRaycaster", false))
                .FirstOrDefault(t => t != null);

            if (ovrType == null)
            {
                // Some packages namespace it under OVR; try a namespaced lookup
                ovrType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("OVR.OVRRaycaster", false))
                    .FirstOrDefault(t => t != null);
            }

            if (ovrType != null && !GetComponent(ovrType))
            {
                // Add OVRRaycaster and disable the generic GraphicRaycaster to avoid double hits
                gameObject.AddComponent(ovrType);
                if (existingGraphicRaycaster) existingGraphicRaycaster.enabled = false;
            }
        }

        private void TryEnableTrackedDeviceRaycaster()
        {
            var tdrType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType("UnityEngine.InputSystem.UI.TrackedDeviceRaycaster", false))
                .FirstOrDefault(t => t != null);

            if (tdrType != null && !GetComponent(tdrType))
            {
                gameObject.AddComponent(tdrType);
            }
        }

        public void ConfigureSizeMeters(float width, float height)
        {
            widthMeters = width;
            heightMeters = height;
            if (rectTransform && enforceSizeInMeters)
            {
                rectTransform.sizeDelta = new Vector2(widthMeters, heightMeters);
            }
        }

        public void SetEnforceSizeInMeters(bool enforce)
        {
            enforceSizeInMeters = enforce;
            if (rectTransform && enforceSizeInMeters)
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

        public void PlaceForAppearStart()
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

        public void PlaceAtFinalPose()
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
            // Wait until a camera is ready
            yield return WaitForCamera();

            Vector3 startPos;
            Vector3 endPos;
            if (animateRelativeToSelf || !managePlacement)
            {
                endPos = transform.position;
                var forwardSelf = transform.forward; forwardSelf.y = 0f; forwardSelf.Normalize();
                startPos = endPos + forwardSelf * appearFromExtraZ;
            }
            else
            {
                var cam = mainCamera.transform;
                var forward = cam.forward; forward.y = 0f; forward.Normalize();
                startPos = cam.position + forward * (distanceMeters + appearFromExtraZ);
                endPos = cam.position + forward * distanceMeters;
            }

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
            // Wait until a camera is ready
            yield return WaitForCamera();

            Vector3 startPos;
            Vector3 endPos;
            if (animateRelativeToSelf || !managePlacement)
            {
                startPos = transform.position;
                var forwardSelf = transform.forward; forwardSelf.y = 0f; forwardSelf.Normalize();
                endPos = startPos + forwardSelf * appearFromExtraZ;
            }
            else
            {
                var cam = mainCamera.transform;
                var forward = cam.forward; forward.y = 0f; forward.Normalize();
                startPos = cam.position + forward * distanceMeters;
                endPos = cam.position + forward * (distanceMeters + appearFromExtraZ);
            }

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
                bool isInteractive = alpha >= interactableAlphaThreshold;
                canvasGroup.interactable = isInteractive;
                canvasGroup.blocksRaycasts = isInteractive;
            }
        }

        private IEnumerator WaitForCamera()
        {
            while (!mainCamera)
            {
                mainCamera = cameraOverride != null ? cameraOverride : Camera.main;
                if (!mainCamera)
                {
                    yield return null;
                }
            }

            if (canvas) canvas.worldCamera = mainCamera;
            // Place the panel relative to the now-available camera before animating
            if (autoPlayOnEnable && managePlacement)
            {
                PlaceForAppearStart();
            }
        }

        public void SetAutoPlayOnEnable(bool enabled)
        {
            autoPlayOnEnable = enabled;
        }

        public void SetManagePlacement(bool enabled)
        {
            managePlacement = enabled;
        }

        public void SetAnimateRelativeToSelf(bool enabled)
        {
            animateRelativeToSelf = enabled;
        }
    }
}


