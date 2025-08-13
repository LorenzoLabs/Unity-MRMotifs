using UnityEngine;
#if NAUGHTY_ATTRIBUTES
using NaughtyAttributes;
#endif

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Helper actions you can hook to UI Button onClick to close the current
    /// panel with a fade (and optional Z- retreat), and optionally show another panel.
    /// Attach this to the same GameObject that has the WorldSpacePanel (or any child).
    /// </summary>
    public class PanelButtonActions : MonoBehaviour
    {
        [Tooltip("Panel to control. If not set, will search in parents.")]
        [SerializeField] private WorldSpacePanel targetPanel;
        
        [Header("Test Targets (Inspector)")]
        [Tooltip("Assign a panel to be shown after closing the current one for testing.")]
        [SerializeField] private GameObject nextPanelForTest;
        
        [Tooltip("Assign a panel to show directly for testing.")]
        [SerializeField] private GameObject panelToShowForTest;

        private void Awake()
        {
            if (!targetPanel)
            {
                targetPanel = GetComponentInParent<WorldSpacePanel>();
            }
        }

        /// <summary>
        /// Fade out the panel using WorldSpacePanel's disappear animation, then deactivate it.
        /// Hook this to a Button's onClick to close the panel with a fade.
        /// </summary>
        public void ClosePanel()
        {
            if (!targetPanel) return;
            targetPanel.PlayDisappear(() =>
            {
                if (targetPanel) targetPanel.gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// Fade out the current panel and, when complete, deactivate it and activate the provided next panel.
        /// The next panel will play its own appear animation on enable.
        /// </summary>
        /// <param name="nextPanel">The GameObject root of the next panel to show.</param>
        public void CloseAndShow(GameObject nextPanel)
        {
            if (!targetPanel) return;
            targetPanel.PlayDisappear(() =>
            {
                if (targetPanel) targetPanel.gameObject.SetActive(false);
                if (nextPanel) nextPanel.SetActive(true);
            });
        }

        /// <summary>
        /// Simply activates the provided panel (useful for Alert panels that self-fade via AlertPanelController).
        /// </summary>
        /// <param name="panelToShow">Panel to activate.</param>
        public void ShowPanel(GameObject panelToShow)
        {
            if (panelToShow) panelToShow.SetActive(true);
        }

        // Inspector test helpers (ContextMenu works without any dependencies; NaughtyAttributes buttons if available)
        [ContextMenu("Test/Close Panel")]
#if NAUGHTY_ATTRIBUTES
        [Button("Close Panel (Test)")]
#endif
        public void Test_ClosePanel()
        {
            ClosePanel();
        }

        [ContextMenu("Test/Close And Show Next")]
#if NAUGHTY_ATTRIBUTES
        [Button("Close And Show Next (Test)")]
#endif
        public void Test_CloseAndShow()
        {
            CloseAndShow(nextPanelForTest);
        }

        [ContextMenu("Test/Show Panel")]
#if NAUGHTY_ATTRIBUTES
        [Button("Show Panel (Test)")]
#endif
        public void Test_ShowPanel()
        {
            ShowPanel(panelToShowForTest);
        }
    }
}


