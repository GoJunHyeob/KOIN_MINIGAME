using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRelay
{
    public sealed class RelayPresentation : MonoBehaviour
    {
        private const string BestScoreKey = "GravityRelay.BestScore.v1";
        private RelayScreen screen = RelayScreen.Start;
        private bool initialized;
        private bool inputBlocked = true;
        private int blockedFrame;
        private int bestScore;
        private int bestAtRunStart;
        private int resultScore;
        private bool newRecord;
        private bool bestDirty;
        private string bestText;
        private string resultScoreText;
        [SerializeField] private RelayCanvasView canvasView;
        private bool canvasBound;
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private SpriteRenderer targetMarker;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Sprite bcsdSprite;
        [SerializeField] private Sprite feedbackParticleSprite;
        [SerializeField] private SpriteRenderer mascot;
        [Header("Replace placeholder assets here")]
        [SerializeField] private AudioClip launchSound;
        [SerializeField] private AudioClip landingSound;
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private Sprite launchExpression;
        [SerializeField] private Sprite landingExpression;
        [SerializeField] private Sprite comboExpression;
        [SerializeField] private Sprite failureExpression;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.45f;
        [SerializeField, Range(5, 30)] private int comboFeedbackCap = 15;
        private RelayFeedback feedback;
        private string comboText;
        private string resultComboText;
        private int displayedCombo = -1;
        [Header("Space background")]
        [SerializeField] private Sprite spaceBackground;
        [SerializeField, Range(0f, 1f)] private float backgroundBrightness = 0.9f;
        [SerializeField, Range(0f, 1f)] private float backgroundParallax = 0.12f;
        [SerializeField] private RelaySpaceBackground spaceBackdrop;
        [Header("Typography")]
        [SerializeField] private Font uiFont;
        [SerializeField] private Font emphasisFont;
        [Header("Launch and landing feedback")]
        [SerializeField, Range(0.5f, 1f)] private float characterDisplayScale = 0.88f;
        [SerializeField, Min(0.1f)] private float arrowLength = 0.9f;
        [SerializeField, Min(0.01f)] private float arrowWidth = 0.055f;
        [SerializeField, Min(0.1f)] private float feedbackDuration = 0.65f;
        private LineRenderer launchArrow;
        private LineRenderer landingPulse;
        private LineRenderer arrivalPulse;
        private Color mascotBaseColor;
        private Color backgroundBaseColor;
        private RelayState previousState;
        private int feedbackScore;
        private float landedAt = -100f;
        private float diedAt = -100f;
        private float launchedAt = -100f;
        private Vector3 mascotBaseScale;
        private Quaternion mascotBaseRotation;
        private Vector2 landingCenter;
        private float landingRadius;
        private InputActionAsset ownedActions;
        private InputAction launchAction;
        private InputAction cancelAction;
        private string scoreText;
        private int displayedScore = -1;

        public void Configure(RelayPlayer relayPlayer, Camera camera, SpriteRenderer marker,
            SpriteRenderer character, InputActionAsset actions)
        {
            player = relayPlayer;
            playCamera = camera;
            targetMarker = marker;
            mascot = character;
            inputActions = actions;
        }

        public void ConfigureCharacter(Sprite character, Sprite particleDot)
        {
            bcsdSprite = character;
            feedbackParticleSprite = particleDot;
        }

        public void ConfigureTypography(Font regular, Font emphasis)
        {
            uiFont = regular;
            emphasisFont = emphasis;
            if (canvasView != null) canvasView.SetFonts(regular, emphasis);
        }

        public void ConfigureCanvas(RelayCanvasView view)
        {
            UnbindCanvas();
            canvasView = view;
            canvasView.Configure(playCamera);
            if (Application.isPlaying) BindCanvas();
        }

        private void BindCanvas()
        {
            if (canvasView == null || canvasBound) return;
            canvasView.StartRequested += StartRun;
            canvasView.TitleRequested += ShowTitle;
            canvasView.ResumeRequested += ResumeRun;
            canvasView.PauseRequested += HandleBack;
            canvasView.LayoutChanged += BlockTransitionInput;
            canvasBound = true;
        }

        private void UnbindCanvas()
        {
            if (canvasView == null || !canvasBound) return;
            canvasView.StartRequested -= StartRun;
            canvasView.TitleRequested -= ShowTitle;
            canvasView.ResumeRequested -= ResumeRun;
            canvasView.PauseRequested -= HandleBack;
            canvasView.LayoutChanged -= BlockTransitionInput;
            canvasBound = false;
        }

        private void OnEnable()
        {
            BindCanvas();
            // Clone the template map so another scene's input remains independent.
            ownedActions = Instantiate(inputActions);
            launchAction = ownedActions.FindAction("Player/Attack", true);
            // Keep the existing action clone, but accept only pointer presses for this game UI.
            for (int i = 0; i < launchAction.bindings.Count; i++)
                launchAction.ApplyBindingOverride(i, string.Empty);
            launchAction.AddBinding("<Mouse>/leftButton");
            launchAction.AddBinding("<Touchscreen>/primaryTouch/press");
            launchAction.Enable();
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android system Back is delivered as Escape; desktop keyboard shortcuts stay disabled.
            cancelAction = ownedActions.FindAction("UI/Cancel", true);
            for (int i = 0; i < cancelAction.bindings.Count; i++)
                cancelAction.ApplyBindingOverride(i, string.Empty);
            cancelAction.AddBinding("<Keyboard>/escape");
            cancelAction.Enable();
#endif
            if (initialized)
            {
                player.SetGameplayActive(screen == RelayScreen.Playing);
                BlockTransitionInput();
            }
        }

        private void OnDisable()
        {
            UnbindCanvas();
            if (initialized)
            {
                RecordScore();
                SaveBest();
                player.SetGameplayActive(false);
            }
            if (feedback != null) { feedback.SetPlaying(false); feedback.ResetFeedback(); }
            launchAction?.Disable();
            cancelAction?.Disable();
            if (ownedActions != null)
                Destroy(ownedActions);
            if (spaceBackdrop != null) spaceBackdrop.SetFlash(Color.white, 0f);
            if (launchArrow != null) launchArrow.enabled = false;
            if (landingPulse != null) landingPulse.enabled = false;
            if (arrivalPulse != null) arrivalPulse.enabled = false;
            if (playCamera != null && launchArrow != null)
                playCamera.backgroundColor = backgroundBaseColor;
            if (mascot != null && launchArrow != null)
            {
                mascot.color = mascotBaseColor;
                mascot.transform.localScale = mascotBaseScale;
                mascot.transform.localRotation = mascotBaseRotation;
            }
        }

        private void Start()
        {
            RelayScreenLayout.Apply(playCamera);
            if (canvasView == null)
            {
                GameObject prefab = Resources.Load<GameObject>("GravityRelayCanvas");
                if (prefab == null)
                {
                    Debug.LogError("Gravity Relay Canvas prefab is missing.", this);
                    enabled = false;
                    return;
                }
                canvasView = Instantiate(prefab).GetComponent<RelayCanvasView>();
            }
            canvasView.Configure(playCamera);
            canvasView.SetFonts(uiFont, emphasisFont);
            BindCanvas();
            bestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
            bestText = "최고  " + bestScore.ToString("00");
            player.SetGameplayActive(false);
            initialized = true;
            BlockTransitionInput();
            Sprite particleDot = feedbackParticleSprite != null ? feedbackParticleSprite : mascot.sprite;
            mascotBaseColor = mascot.color;
            mascotBaseScale = mascot.transform.localScale * characterDisplayScale;
            mascot.transform.localScale = mascotBaseScale;
            mascotBaseRotation = mascot.transform.localRotation;
            backgroundBaseColor = playCamera.backgroundColor;
            Sprite background = spaceBackground != null ? spaceBackground : Resources.Load<Sprite>("GravityRelaySpace");
            if (background != null)
            {
                if (spaceBackdrop == null) spaceBackdrop = gameObject.AddComponent<RelaySpaceBackground>();
                spaceBackdrop.Configure(playCamera, background, mascot.sharedMaterial,
                    backgroundBrightness, backgroundParallax);
            }
            launchArrow = CreateLine("Tangent launch arrow", 5, false);
            landingPulse = CreateLine("Landing pulse", 64, true);
            arrivalPulse = CreateLine("Orbit arrival pulse", 64, true);
            previousState = player.State;
            feedbackScore = player.Score;
            if (bcsdSprite != null)
            {
                mascot.sprite = bcsdSprite;
                mascotBaseColor = Color.white;
                mascot.color = Color.white;
                // The geometric placeholder decorations are children of the renderer.
                foreach (Transform child in mascot.transform)
                    child.gameObject.SetActive(false);
            }
            feedback = gameObject.AddComponent<RelayFeedback>();
            feedback.Configure(player, mascot, targetMarker, particleDot, bcsdSprite == null,
                launchSound != null ? launchSound : Resources.Load<AudioClip>("GravityRelaySfx/PlaceholderLaunch"),
                landingSound != null ? landingSound : Resources.Load<AudioClip>("GravityRelaySfx/PlaceholderLanding"),
                failureSound != null ? failureSound : Resources.Load<AudioClip>("GravityRelaySfx/PlaceholderFailure"),
                effectsVolume, comboFeedbackCap,
                new[] { launchExpression, landingExpression, comboExpression, failureExpression });
        }

        private void Update()
        {
            RelayScreenLayout.Apply(playCamera);

            RecordScore();
            if (screen == RelayScreen.Playing && player.State == RelayState.Dead)
            {
                resultScore = player.Score;
                resultScoreText = resultScore.ToString("00");
                resultComboText = "최대 콤보  " + player.PeakCombo;
                newRecord = resultScore > bestAtRunStart;
                SaveBest();
                screen = RelayScreen.Results;
                BlockTransitionInput();
            }

            // GUI buttons run on release, while gameplay actions run on press. Consume the entire
            // transition gesture and require a fresh press on a later frame before allowing launch.
            if (inputBlocked)
            {
                if (Time.frameCount > blockedFrame && !AnyPrimaryInputHeld())
                {
                    inputBlocked = false;
                    if (canvasView != null) canvasView.SetControlsEnabled(true);
                }
                return;
            }
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                HandleBack();
                return;
            }
            if (screen != RelayScreen.Playing) return;
            if (launchAction.WasPressedThisFrame() && PointerInsideGame())
                player.RequestLaunch();
        }

        private void BlockTransitionInput()
        {
            inputBlocked = true;
            blockedFrame = Time.frameCount;
            if (canvasView != null)
            {
                canvasView.Show(screen);
                canvasView.SetControlsEnabled(false);
            }
        }

        private bool AnyPrimaryInputHeld()
        {
            if (launchAction != null && launchAction.IsPressed()) return true;
            if (cancelAction != null && cancelAction.IsPressed()) return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) return true;
            }
            return false;
        }

        private bool PointerInsideGame()
        {
            InputDevice device = launchAction.activeControl?.device;
            if (device is Mouse mouse) return RelayScreenLayout.PlayArea.Contains(mouse.position.ReadValue()) &&
                (canvasView == null || !canvasView.BlocksPointer(mouse.position.ReadValue()));
            if (device is Touchscreen touchscreen)
                return RelayScreenLayout.PlayArea.Contains(touchscreen.primaryTouch.position.ReadValue()) &&
                    (canvasView == null || !canvasView.BlocksPointer(touchscreen.primaryTouch.position.ReadValue()));
            return true;
        }

        private void HandleBack()
        {
            if (screen == RelayScreen.Playing)
            {
                RecordScore();
                SaveBest();
                player.SetGameplayActive(false);
                screen = RelayScreen.Paused;
                if (feedback != null) feedback.ResetFeedback();
                BlockTransitionInput();
            }
            else if (screen == RelayScreen.Paused) ResumeRun();
            else if (screen == RelayScreen.Results) ShowTitle();
            // At the title, leave exit/navigation to the hosting app. Never force Application.Quit.
        }

        private void ResumeRun()
        {
            screen = RelayScreen.Playing;
            player.SetGameplayActive(true);
            BlockTransitionInput();
        }

        private void StartRun()
        {
            if (feedback != null) feedback.ResetFeedback();
            RecordScore();
            SaveBest();
            // The title already has a prepared course; reuse it to avoid duplicate generation on Start.
            bool prepared = screen == RelayScreen.Start && player.State == RelayState.Orbiting && player.Score == 0;
            if (!prepared) player.Restart();
            bestAtRunStart = bestScore;
            newRecord = false;
            landedAt = diedAt = launchedAt = -100f;
            previousState = player.State;
            feedbackScore = player.Score;
            displayedScore = -1;
            screen = RelayScreen.Playing;
            player.SetGameplayActive(true);
            BlockTransitionInput();
            RecordScore();
        }

        private void ShowTitle()
        {
            if (feedback != null) feedback.ResetFeedback();
            RecordScore();
            SaveBest();
            player.SetGameplayActive(false);
            player.Restart();
            screen = RelayScreen.Start;
            landedAt = diedAt = launchedAt = -100f;
            previousState = player.State;
            feedbackScore = 0;
            displayedScore = -1;
            BlockTransitionInput();
            RecordScore();
        }

        private void RecordScore()
        {
            if (player == null) return;
            if (displayedScore != player.Score)
            {
                displayedScore = player.Score;
                scoreText = player.Score.ToString("00");
            }
            if (displayedCombo != player.Combo)
            {
                displayedCombo = player.Combo;
                comboText = "콤보  " + player.Combo;
            }
            if (player.Score > bestScore)
            {
                bestScore = player.Score;
                bestText = "최고  " + bestScore.ToString("00");
                PlayerPrefs.SetInt(BestScoreKey, bestScore);
                bestDirty = true;
            }
        }

        private void SaveBest()
        {
            if (!bestDirty) return;
            PlayerPrefs.Save();
            bestDirty = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!initialized) return;
            if (paused) { RecordScore(); SaveBest(); }
            else if (screen == RelayScreen.Playing) BlockTransitionInput();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!initialized) return;
            if (!focused) { RecordScore(); SaveBest(); }
            else if (screen == RelayScreen.Playing) BlockTransitionInput();
        }

        private void OnApplicationQuit()
        {
            if (!initialized) return;
            RecordScore();
            SaveBest();
        }

        private LineRenderer CreateLine(string name, int points, bool loop)
        {
            var item = new GameObject(name, typeof(LineRenderer));
            item.transform.SetParent(transform, false);
            LineRenderer line = item.GetComponent<LineRenderer>();
            line.sharedMaterial = mascot.sharedMaterial;
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.sortingOrder = 20;
            line.numCapVertices = 3;
            line.startWidth = line.endWidth = arrowWidth;
            line.enabled = false;
            return line;
        }

        private void LateUpdate()
        {
            if (launchArrow == null)
                return;

            if (feedback != null) feedback.SetPlaying(screen == RelayScreen.Playing);
            bool stateChanged = previousState != player.State;
            bool restarted = player.Score < feedbackScore ||
                (previousState == RelayState.Dead && player.State == RelayState.Orbiting);
            if (restarted)
            {
                landedAt = diedAt = launchedAt = -100f;
            }
            if (stateChanged && player.State == RelayState.Flying)
            {
                launchedAt = Time.time;
            }
            if (player.Score > feedbackScore)
            {
                landedAt = Time.time;
                landingCenter = player.CurrentPlanet.Center;
                landingRadius = player.OrbitRadius(player.CurrentPlanet);
            }
            if (stateChanged && player.State == RelayState.Dead)
                diedAt = Time.time;

            bool landed = Time.time - landedAt < feedbackDuration;
            Color success = new Color(0.72f, 0.53f, 1f);
            Color failure = new Color(0.95f, 0.38f, 0.65f);
            mascot.color = player.State == RelayState.Dead ? Color.Lerp(mascotBaseColor, failure, 0.45f) : mascotBaseColor;
            float flash = player.State == RelayState.Dead
                ? 0.35f * Mathf.Clamp01(1f - (Time.time - diedAt) / feedbackDuration) : 0f;
            playCamera.backgroundColor = Color.Lerp(backgroundBaseColor, failure, flash);
            if (spaceBackdrop != null) spaceBackdrop.SetFlash(failure, flash);

            // Visual impulses never alter movement, input timing, or the shared time scale.
            mascot.transform.localRotation = mascotBaseRotation;
            mascot.transform.localScale = mascotBaseScale;
            float launchAge = Time.time - launchedAt;
            float landingAge = Time.time - landedAt;
            if (player.State == RelayState.Flying && launchAge < 0.1f)
            {
                float impulse = 1f - Mathf.Clamp01(launchAge / 0.1f);
                mascot.transform.localScale = Vector3.Scale(mascotBaseScale,
                    new Vector3(1f + 0.55f * impulse, 1f - 0.3f * impulse, 1f));
            }
            else if (player.State != RelayState.Dead && landingAge < 0.14f)
            {
                float impulse = Mathf.Sin(Mathf.Clamp01(landingAge / 0.14f) * Mathf.PI);
                mascot.transform.localScale = mascotBaseScale * (1f + 0.25f * impulse);
            }

            launchArrow.enabled = screen == RelayScreen.Playing && player.CanLaunch && !inputBlocked;
            if (launchArrow.enabled)
            {
                Vector2 direction = player.LaunchDirection;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Vector2 extents = mascot.bounds.extents;
                float characterEdge = Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y;
                Vector2 start = (Vector2)player.transform.position + direction * (characterEdge + 0.08f);
                Vector2 tip = start + direction * arrowLength;
                float head = Mathf.Min(0.22f, arrowLength * 0.35f);
                launchArrow.startWidth = launchArrow.endWidth = arrowWidth;
                launchArrow.startColor = launchArrow.endColor = new Color(0.78f, 0.66f, 1f);
                launchArrow.SetPosition(0, start);
                launchArrow.SetPosition(1, tip);
                launchArrow.SetPosition(2, tip - direction * head + normal * head * 0.6f);
                launchArrow.SetPosition(3, tip);
                launchArrow.SetPosition(4, tip - direction * head - normal * head * 0.6f);
            }

            landingPulse.enabled = landed && screen == RelayScreen.Playing;
            arrivalPulse.enabled = landed && screen == RelayScreen.Playing;
            if (landed)
            {
                float progress = Mathf.Clamp01((Time.time - landedAt) / feedbackDuration);
                float radius = landingRadius + progress * 0.6f;
                landingPulse.startWidth = landingPulse.endWidth = arrowWidth * (1f + Mathf.Min(player.Combo, comboFeedbackCap) * 0.035f);
                success.a = 1f - progress;
                landingPulse.startColor = landingPulse.endColor = success;
                for (int i = 0; i < landingPulse.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / landingPulse.positionCount;
                    landingPulse.SetPosition(i, landingCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
                float arrivalRadius = landingRadius * Mathf.Lerp(1.3f, 0.98f, Mathf.SmoothStep(0f, 1f, progress));
                Color arrival = new Color(0.95f, 0.87f, 1f, 0.78f * (1f - progress));
                arrivalPulse.startWidth = arrivalPulse.endWidth = arrowWidth * 0.85f;
                arrivalPulse.startColor = arrivalPulse.endColor = arrival;
                for (int i = 0; i < arrivalPulse.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / arrivalPulse.positionCount;
                    arrivalPulse.SetPosition(i, landingCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * arrivalRadius);
                }
            }

            targetMarker.transform.position = player.TargetPlanet.transform.position;
            targetMarker.enabled = screen == RelayScreen.Playing;
            float diameter = player.CaptureRadius(player.TargetPlanet) * 2f;
            Vector2 spriteSize = targetMarker.sprite.bounds.size;
            targetMarker.transform.localScale = new Vector3(diameter / spriteSize.x, diameter / spriteSize.y, 1f);
            if (canvasView != null)
            {
                canvasView.Show(screen);
                canvasView.SetControlsEnabled(!inputBlocked);
                float pop = 1f + Mathf.Clamp01(1f - (Time.time - landedAt) / 0.18f) * 0.2f;
                canvasView.SetValues(scoreText, bestText, comboText, resultScoreText, resultComboText,
                    FailureMessage(), newRecord, player.Combo / (float)Mathf.Max(1, comboFeedbackCap), pop);
            }
            previousState = player.State;
            feedbackScore = player.Score;
        }

        private string FailureMessage()
        {
            switch (player.FailureReason)
            {
                case "FLIGHT TIME EXPIRED": return "다음 행성까지 시간이 부족했어요";
                case "OUT OF ORBIT": return "우주 바깥으로 벗어났어요";
                case "NO REACHABLE ROUTE": return "행성 경로를 준비하지 못했어요";
                default: return "다시 한 번 이어가 볼까요?";
            }
        }

    }
}
