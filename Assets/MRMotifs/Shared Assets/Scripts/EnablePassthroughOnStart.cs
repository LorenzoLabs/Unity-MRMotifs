// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
#if META_CORE_SDK_DEFINED
using Meta.XR.Environment;
#endif

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Ensures the scene starts in passthrough mode on Meta Quest.
    /// Works with MR Motifs samples that include passthrough management.
    /// </summary>
    public class EnablePassthroughOnStart : MonoBehaviour
    {
        private void Start()
        {
#if META_CORE_SDK_DEFINED
            // If using Meta SDK Environment features, explicitly enable passthrough.
            PassthroughUtility.TryEnablePassthrough();
#else
            // Fallback: try enabling the OVRManager passthrough flag if present
            var ovrManager = FindObjectOfType<OVRManager>();
            if (ovrManager != null)
            {
                ovrManager.isInsightPassthroughEnabled = true;
            }
#endif
        }
    }
}


