// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Simple controller to sequence an initial "connection" panel that the user
    /// can dismiss with a Continue button, followed by a subsequent panel (e.g. alert).
    /// </summary>
    public class PanelSequenceController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldSpacePanel connectionPanel;
        [SerializeField] private GameObject nextPanelRoot; // e.g. alert panel root

        public void Continue()
        {
            if (connectionPanel == null)
            {
                if (nextPanelRoot) nextPanelRoot.SetActive(true);
                return;
            }

            connectionPanel.PlayDisappear(() =>
            {
                if (nextPanelRoot)
                {
                    nextPanelRoot.SetActive(true);
                }
            });
        }
    }
}


