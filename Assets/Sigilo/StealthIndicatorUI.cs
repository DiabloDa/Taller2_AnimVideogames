using UnityEngine;
using UnityEngine.UI;

namespace Clases.Clase_2.Scripts
{
    /// <summary>
    /// Simple visual feedback for stealth state.
    /// Assign an indicator GameObject (e.g., an Image/Text under a Canvas) and it will be enabled only while in stealth.
    /// </summary>
    public class StealthIndicatorUI : MonoBehaviour
    {
        [SerializeField] private Character character;
        [Tooltip("If assigned, this GameObject will be shown/hidden based on Character.IsStealth. If null, this component will toggle its own UI visibility.")]
        [SerializeField] private GameObject indicator;

        [Header("Self-visibility mode (when Indicator is null or is this object)")]
        [Tooltip("When toggling self, use a CanvasGroup for show/hide (recommended).")]
        [SerializeField] private bool useCanvasGroupWhenSelf = true;

        [Tooltip("If true and a CanvasGroup is missing in self-visibility mode, one will be added.")]
        [SerializeField] private bool autoAddCanvasGroup = true;

        private bool _last;
        private CanvasGroup _selfCanvasGroup;
        private Graphic _selfGraphic;

        private void Awake()
        {
            if (character == null)
                character = FindFirstObjectByType<Character>();

            // If indicator isn't set, prefer toggling this object's UI visibility.
            if (indicator == null)
                indicator = gameObject;

            // Cache self components for self-visibility mode.
            if (indicator == gameObject)
            {
                _selfCanvasGroup = GetComponent<CanvasGroup>();
                if (_selfCanvasGroup == null && useCanvasGroupWhenSelf && autoAddCanvasGroup)
                    _selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();

                _selfGraphic = GetComponent<Graphic>();

#if UNITY_EDITOR
                if (_selfCanvasGroup == null && _selfGraphic == null)
                    Debug.LogWarning("[StealthIndicatorUI] Attach this to a UI element (Text/Image) or assign an indicator GameObject.", this);
#endif
            }
        }

        private void OnEnable()
        {
            ForceRefresh();
        }

        private void Update()
        {
            if (character == null || indicator == null) return;

            bool current = character.IsStealth;
            if (current == _last) return;

            ApplyVisible(current);
            _last = current;
        }

        public void ForceRefresh()
        {
            if (character == null || indicator == null) return;
            _last = !character.IsStealth; // force flip
            ApplyVisible(character.IsStealth);
            _last = character.IsStealth;
        }

        private void ApplyVisible(bool visible)
        {
            if (indicator == null) return;

            // If toggling a different object, simple active toggle is fine.
            if (indicator != gameObject)
            {
                indicator.SetActive(visible);
                return;
            }

            // Self-visibility mode: avoid disabling our own GameObject.
            if (useCanvasGroupWhenSelf && _selfCanvasGroup != null)
            {
                _selfCanvasGroup.alpha = visible ? 1f : 0f;
                _selfCanvasGroup.blocksRaycasts = visible;
                _selfCanvasGroup.interactable = visible;
                return;
            }

            if (_selfGraphic != null)
                _selfGraphic.enabled = visible;
        }
    }
}
