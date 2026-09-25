using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoonRover.UI
{
    /// <summary>
    /// Lunar Ops HUD 的动效编排。
    /// 动效重点是“任务控制台启动”：左右信息栏从各自屏幕边缘归位，
    /// 让中央驾驶视野保持稳定；按钮只在交互时提供短促反馈。
    /// </summary>
    public sealed class AnimeJsUiMotion : MonoBehaviour
    {
        [Header("启动动效")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float panelDurationMs = 420f;
        [SerializeField] private float panelStaggerMs = 70f;
        [SerializeField] private float panelOffset = 34f;

        private bool _played;

        private void Start()
        {
            if (playOnStart) PlayBootSequence();
        }

        [ContextMenu("播放 Lunar Ops 启动动效")]
        public void PlayBootSequence()
        {
            if (_played) return;
            _played = true;

            // 左右两列分批入场，中央驾驶画面不被遮挡，也不做无意义的全屏动画。
            AnimatePanel("ModePanel", new Vector2(-panelOffset, 0f), 0f);
            AnimatePanel("StatusPanel", new Vector2(-panelOffset, 0f), panelStaggerMs);
            AnimatePanel("TelemetryPanel", new Vector2(-panelOffset, 0f), panelStaggerMs * 2f);

            AnimatePanel("MapPanel", new Vector2(panelOffset, 0f), 0f);
            AnimatePanel("CamPanel", new Vector2(panelOffset, 0f), panelStaggerMs);
            AnimatePanel("SystemStatusPanel", new Vector2(panelOffset, 0f), panelStaggerMs * 2f);
            AnimatePanel("BatteryPanel", new Vector2(panelOffset, 0f), panelStaggerMs * 3f);
            AnimatePanel("EventLogPanel", new Vector2(panelOffset, 0f), panelStaggerMs * 4f);

            AnimatePanel("CompassBar", new Vector2(0f, panelOffset * 0.6f), panelStaggerMs * 2f);

            // 运行时补挂按钮反馈，避免改动现有 DriveModeManager 的点击逻辑。
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].GetComponent<AnimeJsButtonFeedback>() == null)
                    buttons[i].gameObject.AddComponent<AnimeJsButtonFeedback>();
            }
        }

        private void AnimatePanel(string objectName, Vector2 offset, float delayMs)
        {
            Transform child = transform.Find(objectName);
            if (child == null) return;

            RectTransform rect = child as RectTransform;
            if (rect == null) return;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();

            Vector2 targetPosition = rect.anchoredPosition;
            Vector2 initialPosition = targetPosition + offset;
            rect.anchoredPosition = initialPosition;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            AnimeTween<Vector2> move = AnimeJsLite.Move(
                rect,
                targetPosition,
                panelDurationMs,
                AnimeEase.OutExpo,
                delayMs,
                false);
            move.From(initialPosition).Play();

            AnimeTween<float> fade = AnimeJsLite.Fade(
                group,
                1f,
                panelDurationMs * 0.78f,
                AnimeEase.OutQuad,
                delayMs,
                false);
            fade.From(0f);
            fade.OnComplete = _ =>
            {
                if (group == null) return;
                group.interactable = true;
                group.blocksRaycasts = true;
            };
            fade.Play();
        }
    }

    /// <summary>
    /// 只负责按钮的视觉反馈，不接管按钮原有事件：
    /// hover 轻微放大、按下收缩、松开回弹，全部使用短时 transform 动画。
    /// </summary>
    public sealed class AnimeJsButtonFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.025f;
        [SerializeField] private float hoverDurationMs = 140f;
        [SerializeField] private float pressDurationMs = 90f;

        private RectTransform _rect;
        private Vector3 _baseScale;
        private bool _pointerInside;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect != null ? _rect.localScale : Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            AnimateScale(hoverScale, hoverDurationMs, AnimeEase.OutBack);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            AnimateScale(1f, hoverDurationMs, AnimeEase.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateScale(0.965f, pressDurationMs, AnimeEase.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateScale(_pointerInside ? hoverScale : 1f, hoverDurationMs, AnimeEase.OutBack);
        }

        private void AnimateScale(float factor, float durationMs, AnimeEase ease)
        {
            if (_rect == null) return;
            Vector3 target = new Vector3(
                _baseScale.x * factor,
                _baseScale.y * factor,
                _baseScale.z * factor);

            AnimeJsLite.AnimateVector3(
                () => _rect.localScale,
                value => _rect.localScale = value,
                target,
                durationMs,
                ease);
        }

        private void OnDisable()
        {
            if (_rect != null) _rect.localScale = _baseScale;
        }
    }
}
