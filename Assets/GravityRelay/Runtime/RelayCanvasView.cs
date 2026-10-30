using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GravityRelay
{
    public enum RelayScreen { Start, Playing, Paused, Results }

    [ExecuteAlways, DefaultExecutionOrder(-1000)]
    public sealed class RelayCanvasView : MonoBehaviour
    {
        [SerializeField] private Camera playCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private RectTransform playFrame;
        [SerializeField] private RectTransform designRoot;
        [SerializeField] private RawImage[] backgroundExtensions;
        [SerializeField] private GameObject titlePanel, hudPanel, pausePanel, resultsPanel;
        [SerializeField] private Text titleBest, hudScore, hudBest, hudCombo, resultScore, resultBest, resultCombo, resultReason;
        [SerializeField] private GameObject newRecordLabel;
        [SerializeField] private Button startButton, restartButton, resumeButton, pauseButton, resultTitleButton, pauseTitleButton;
        [SerializeField] private Text[] regularLabels, emphasisLabels;
        [SerializeField] private InputSystemUIInputModule inputModule;
        public event Action StartRequested, TitleRequested, ResumeRequested, PauseRequested, LayoutChanged;
        private Rect previousArea;
        private Rect previousWindow;
        private float previousCanvasScale = -1f;
        private bool listening;
        private RelayScreen shownScreen;

        public void Configure(Camera camera) { playCamera = camera; RefreshLayout(); }

        private void OnEnable()
        {
            RefreshLayout();
            if (!Application.isPlaying) return;
            if (inputModule != null)
            {
                if (inputModule.actionsAsset == null) inputModule.AssignDefaultActions();
                inputModule.move = null;
                inputModule.submit = null;
                inputModule.cancel = null;
                inputModule.GetComponent<EventSystem>().sendNavigationEvents = false;
            }
            if (listening) return;
            startButton.onClick.AddListener(RequestStart);
            restartButton.onClick.AddListener(RequestStart);
            resumeButton.onClick.AddListener(RequestResume);
            pauseButton.onClick.AddListener(RequestPause);
            resultTitleButton.onClick.AddListener(RequestTitle);
            pauseTitleButton.onClick.AddListener(RequestTitle);
            listening = true;
        }

        private void OnDisable()
        {
            if (!listening) return;
            startButton.onClick.RemoveListener(RequestStart);
            restartButton.onClick.RemoveListener(RequestStart);
            resumeButton.onClick.RemoveListener(RequestResume);
            pauseButton.onClick.RemoveListener(RequestPause);
            resultTitleButton.onClick.RemoveListener(RequestTitle);
            pauseTitleButton.onClick.RemoveListener(RequestTitle);
            listening = false;
        }

        private void RequestStart() => StartRequested?.Invoke();
        private void RequestTitle() => TitleRequested?.Invoke();
        private void RequestResume() => ResumeRequested?.Invoke();
        private void RequestPause() => PauseRequested?.Invoke();

        private void Update() => RefreshLayout();

        private void RefreshLayout()
        {
            RelayScreenLayout.Apply(playCamera);
            if (canvas == null || playFrame == null || designRoot == null) return;
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = RelayScreenLayout.ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            Rect window = RelayScreenLayout.Window;
            Rect area = RelayScreenLayout.PlayArea;
            float canvasScale = Mathf.Max(0.001f, canvas.scaleFactor);
            bool pixelsChanged = previousArea != area || previousWindow != window;
            if (!pixelsChanged && Mathf.Approximately(previousCanvasScale, canvasScale)) return;
            previousCanvasScale = canvasScale;
            SetAnchors(playFrame, area, window);
            designRoot.anchorMin = designRoot.anchorMax = new Vector2(0.5f, 0.5f);
            designRoot.sizeDelta = RelayScreenLayout.ReferenceResolution;
            designRoot.anchoredPosition = Vector2.zero;
            // The design frame fits the camera after both screen scaling and safe-area insets.
            float designScale = area.width / (1080f * canvasScale);
            designRoot.localScale = new Vector3(designScale, designScale, 1f);
            if (backgroundExtensions != null && backgroundExtensions.Length == 4)
            {
                SetExtension(0, Rect.MinMaxRect(0, 0, area.xMin, window.height), window);
                SetExtension(1, Rect.MinMaxRect(area.xMax, 0, window.width, window.height), window);
                SetExtension(2, Rect.MinMaxRect(area.xMin, 0, area.xMax, area.yMin), window);
                SetExtension(3, Rect.MinMaxRect(area.xMin, area.yMax, area.xMax, window.height), window);
            }
            if (pixelsChanged)
            {
                previousArea = area;
                previousWindow = window;
                if (Application.isPlaying) LayoutChanged?.Invoke();
            }
        }

        private static void SetAnchors(RectTransform rect, Rect pixels, Rect window)
        {
            rect.anchorMin = new Vector2(pixels.xMin / window.width, pixels.yMin / window.height);
            rect.anchorMax = new Vector2(pixels.xMax / window.width, pixels.yMax / window.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void SetExtension(int index, Rect pixels, Rect window)
        {
            RawImage image = backgroundExtensions[index];
            if (image == null) return;
            image.enabled = pixels.width > 0.1f && pixels.height > 0.1f;
            SetAnchors(image.rectTransform, pixels, window);
            // Crop one continuous full-window artwork across the outer strips; no stretched stars.
            float imageAspect = image.texture != null ? (float)image.texture.width / image.texture.height : RelayScreenLayout.Aspect;
            float screenAspect = window.width / window.height;
            float uvWidth = Mathf.Min(1f, screenAspect / imageAspect);
            float uvHeight = Mathf.Min(1f, imageAspect / screenAspect);
            image.uvRect = new Rect((1f - uvWidth) * 0.5f + pixels.x / window.width * uvWidth,
                (1f - uvHeight) * 0.5f + pixels.y / window.height * uvHeight,
                pixels.width / window.width * uvWidth, pixels.height / window.height * uvHeight);
        }

        public void SetFonts(Font regular, Font emphasis)
        {
            if (regular != null && regularLabels != null)
                foreach (Text label in regularLabels) if (label != null) label.font = regular;
            if (emphasis != null && emphasisLabels != null)
                foreach (Text label in emphasisLabels) if (label != null) label.font = emphasis;
        }

        public void Show(RelayScreen screen)
        {
            shownScreen = screen;
            SetActive(titlePanel, screen == RelayScreen.Start);
            SetActive(hudPanel, screen == RelayScreen.Playing);
            SetActive(pausePanel, screen == RelayScreen.Paused);
            SetActive(resultsPanel, screen == RelayScreen.Results);
        }

        private static void SetActive(GameObject item, bool active)
        {
            if (item != null && item.activeSelf != active) item.SetActive(active);
        }

        public void SetControlsEnabled(bool enabled)
        {
            startButton.interactable = restartButton.interactable = resumeButton.interactable = enabled;
            pauseButton.interactable = resultTitleButton.interactable = pauseTitleButton.interactable = enabled;
        }

        public bool BlocksPointer(Vector2 position)
        {
            // Test geometry directly, before EventSystem.Update, so a press cannot launch first.
            return shownScreen != RelayScreen.Playing ||
                (pauseButton.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(pauseButton.transform as RectTransform, position, null));
        }

        public void SetValues(string score, string best, string combo, string result, string peak,
            string reason, bool record, float comboStrength, float pop)
        {
            SetText(hudScore, score); SetText(hudBest, best); SetText(hudCombo, combo);
            SetText(titleBest, best); SetText(resultBest, best); SetText(resultScore, result);
            SetText(resultCombo, peak); SetText(resultReason, reason);
            SetActive(newRecordLabel, record);
            hudScore.fontSize = score != null && score.Length > 4 ? Mathf.Max(48, 86 - (score.Length - 4) * 7) : 86;
            hudCombo.fontSize = Mathf.RoundToInt(Mathf.Lerp(48f, 65f, Mathf.Clamp01(comboStrength)) * pop);
        }

        private static void SetText(Text label, string value)
        {
            value = value ?? string.Empty;
            if (label.text != value) label.text = value;
        }
    }
}
