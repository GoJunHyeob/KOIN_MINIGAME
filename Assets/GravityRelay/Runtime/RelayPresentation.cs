using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRelay
{
    public sealed class RelayPresentation : MonoBehaviour
    {
        private enum ScreenState { Start, Playing, Paused, Results }
        private const string BestScoreKey = "GravityRelay.BestScore.v1";
        private ScreenState screen = ScreenState.Start;
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
        private GUIStyle buttonStyle;
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private SpriteRenderer targetMarker;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Sprite bcsdSprite;
        [SerializeField] private SpriteRenderer mascot;
        [Header("Typography")]
        [SerializeField] private Font uiFont;
        [SerializeField] private Font emphasisFont;
        [Header("Launch and landing feedback")]
        [SerializeField, Min(0.1f)] private float arrowLength = 0.9f;
        [SerializeField, Min(0.01f)] private float arrowWidth = 0.055f;
        [SerializeField, Min(0.1f)] private float feedbackDuration = 0.65f;
        private LineRenderer launchArrow;
        private LineRenderer landingPulse;
        private Color mascotBaseColor;
        private Color backgroundBaseColor;
        private RelayState previousState;
        private int feedbackScore;
        private float landedAt = -100f;
        private float diedAt = -100f;
        private float launchedAt = -100f;
        private Vector3 mascotBaseScale;
        private Quaternion mascotBaseRotation;
        private Vector2 launchVisualDirection;
        private Vector2 landingCenter;
        private float landingRadius;
        private InputActionAsset ownedActions;
        private InputAction launchAction;
        private InputAction cancelAction;
        private GUIStyle titleStyle, textStyle, scoreStyle;
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

        public void ConfigureTypography(Font regular, Font emphasis)
        {
            uiFont = regular;
            emphasisFont = emphasis;
            titleStyle = null;
        }

        private void OnEnable()
        {
            // Clone the template map so another scene's input remains independent.
            ownedActions = Instantiate(inputActions);
            launchAction = ownedActions.FindAction("Player/Attack", true);
            // Replace the template's release-time tap with a press, avoiding two commands per touch.
            bool hasTouch = false;
            for (int i = 0; i < launchAction.bindings.Count; i++)
            {
                if (launchAction.bindings[i].path == "<Touchscreen>/primaryTouch/tap")
                {
                    launchAction.ApplyBindingOverride(i, "<Touchscreen>/primaryTouch/press");
                    hasTouch = true;
                }
            }
            if (!hasTouch)
                launchAction.AddBinding("<Touchscreen>/primaryTouch/press");
            launchAction.AddBinding("<Keyboard>/space");
            launchAction.Enable();
            cancelAction = ownedActions.FindAction("UI/Cancel", true);
            cancelAction.Enable();
            if (initialized)
            {
                player.SetGameplayActive(screen == ScreenState.Playing);
                BlockTransitionInput();
            }
        }

        private void OnDisable()
        {
            if (initialized)
            {
                RecordScore();
                SaveBest();
                player.SetGameplayActive(false);
            }
            launchAction?.Disable();
            cancelAction?.Disable();
            if (ownedActions != null)
                Destroy(ownedActions);
            if (launchArrow != null) launchArrow.enabled = false;
            if (landingPulse != null) landingPulse.enabled = false;
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
            bestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
            bestText = "최고  " + bestScore.ToString("00");
            player.SetGameplayActive(false);
            initialized = true;
            BlockTransitionInput();
            mascotBaseColor = mascot.color;
            mascotBaseScale = mascot.transform.localScale;
            mascotBaseRotation = mascot.transform.localRotation;
            backgroundBaseColor = playCamera.backgroundColor;
            launchArrow = CreateLine("Tangent launch arrow", 5, false);
            landingPulse = CreateLine("Landing pulse", 64, true);
            previousState = player.State;
            feedbackScore = player.Score;
            if (bcsdSprite != null)
            {
                mascot.sprite = bcsdSprite;
                // The geometric placeholder decorations are children of the renderer.
                foreach (Transform child in mascot.transform)
                    child.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Letterbox to a fixed portrait playing field on desktop and mobile.
            const float aspect = 9f / 16f;
            float screenAspect = (float)Screen.width / Screen.height;
            playCamera.rect = screenAspect > aspect
                ? new Rect((1f - aspect / screenAspect) * 0.5f, 0f, aspect / screenAspect, 1f)
                : new Rect(0f, (1f - screenAspect / aspect) * 0.5f, 1f, screenAspect / aspect);

            RecordScore();
            if (screen == ScreenState.Playing && player.State == RelayState.Dead)
            {
                resultScore = player.Score;
                resultScoreText = resultScore.ToString("00");
                newRecord = resultScore > bestAtRunStart;
                SaveBest();
                screen = ScreenState.Results;
                BlockTransitionInput();
            }

            // GUI buttons run on release, while gameplay actions run on press. Consume the entire
            // transition gesture and require a fresh press on a later frame before allowing launch.
            if (inputBlocked)
            {
                if (Time.frameCount > blockedFrame && !AnyPrimaryInputHeld())
                    inputBlocked = false;
                return;
            }
            if (cancelAction.WasPressedThisFrame())
            {
                HandleBack();
                return;
            }
            if (screen != ScreenState.Playing)
            {
                if (ConfirmPressed())
                {
                    if (screen == ScreenState.Paused) ResumeRun();
                    else StartRun();
                }
                return;
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartRun();
                return;
            }
            if (launchAction.WasPressedThisFrame() && PointerInsideGame())
                player.RequestLaunch();
        }

        private void BlockTransitionInput()
        {
            inputBlocked = true;
            blockedFrame = Time.frameCount;
        }

        private bool AnyPrimaryInputHeld()
        {
            if (launchAction != null && launchAction.IsPressed()) return true;
            if (cancelAction != null && cancelAction.IsPressed()) return true;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
                keyboard.spaceKey.isPressed || keyboard.rKey.isPressed)) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) return true;
            }
            return false;
        }

        private static bool ConfirmPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.rKey.wasPressedThisFrame)) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private bool PointerInsideGame()
        {
            InputDevice device = launchAction.activeControl?.device;
            if (device is Mouse mouse) return playCamera.pixelRect.Contains(mouse.position.ReadValue());
            if (device is Touchscreen touchscreen)
                return new Rect(0f, 0f, Screen.width, Screen.height).Contains(touchscreen.primaryTouch.position.ReadValue());
            return true;
        }

        private void HandleBack()
        {
            if (screen == ScreenState.Playing)
            {
                RecordScore();
                SaveBest();
                player.SetGameplayActive(false);
                screen = ScreenState.Paused;
                BlockTransitionInput();
            }
            else if (screen == ScreenState.Paused) ResumeRun();
            else if (screen == ScreenState.Results) ShowTitle();
            // At the title, leave exit/navigation to the hosting app. Never force Application.Quit.
        }

        private void ResumeRun()
        {
            screen = ScreenState.Playing;
            player.SetGameplayActive(true);
            BlockTransitionInput();
        }

        private void StartRun()
        {
            RecordScore();
            SaveBest();
            // The title already has a prepared course; reuse it to avoid duplicate generation on Start.
            bool prepared = screen == ScreenState.Start && player.State == RelayState.Orbiting && player.Score == 0;
            if (!prepared) player.Restart();
            bestAtRunStart = bestScore;
            newRecord = false;
            landedAt = diedAt = launchedAt = -100f;
            previousState = player.State;
            feedbackScore = player.Score;
            displayedScore = -1;
            screen = ScreenState.Playing;
            player.SetGameplayActive(true);
            BlockTransitionInput();
            RecordScore();
        }

        private void ShowTitle()
        {
            RecordScore();
            SaveBest();
            player.SetGameplayActive(false);
            player.Restart();
            screen = ScreenState.Start;
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
            else if (screen == ScreenState.Playing) BlockTransitionInput();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!initialized) return;
            if (!focused) { RecordScore(); SaveBest(); }
            else if (screen == ScreenState.Playing) BlockTransitionInput();
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
                launchVisualDirection = player.Velocity.normalized;
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

            // Visual impulses never alter movement, input timing, or the shared time scale.
            mascot.transform.localRotation = mascotBaseRotation;
            mascot.transform.localScale = mascotBaseScale;
            float launchAge = Time.time - launchedAt;
            float landingAge = Time.time - landedAt;
            if (player.State == RelayState.Flying && launchAge < 0.1f)
            {
                float impulse = 1f - Mathf.Clamp01(launchAge / 0.1f);
                mascot.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(launchVisualDirection.y, launchVisualDirection.x) * Mathf.Rad2Deg);
                mascot.transform.localScale = Vector3.Scale(mascotBaseScale,
                    new Vector3(1f + 0.55f * impulse, 1f - 0.3f * impulse, 1f));
            }
            else if (player.State != RelayState.Dead && landingAge < 0.14f)
            {
                float impulse = Mathf.Sin(Mathf.Clamp01(landingAge / 0.14f) * Mathf.PI);
                mascot.transform.localScale = mascotBaseScale * (1f + 0.25f * impulse);
            }

            launchArrow.enabled = screen == ScreenState.Playing && player.CanLaunch && !inputBlocked;
            if (launchArrow.enabled)
            {
                Vector2 direction = player.LaunchDirection;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Vector2 start = (Vector2)player.transform.position + direction * 0.3f;
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

            landingPulse.enabled = landed && screen == ScreenState.Playing;
            if (landed)
            {
                float progress = Mathf.Clamp01((Time.time - landedAt) / feedbackDuration);
                float radius = landingRadius + progress * 0.6f;
                success.a = 1f - progress;
                landingPulse.startColor = landingPulse.endColor = success;
                for (int i = 0; i < landingPulse.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / landingPulse.positionCount;
                    landingPulse.SetPosition(i, landingCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            targetMarker.transform.position = player.TargetPlanet.transform.position;
            targetMarker.enabled = screen == ScreenState.Playing;
            float diameter = player.CaptureRadius(player.TargetPlanet) * 2f;
            Vector2 spriteSize = targetMarker.sprite.bounds.size;
            targetMarker.transform.localScale = new Vector3(diameter / spriteSize.x, diameter / spriteSize.y, 1f);
            previousState = player.State;
            feedbackScore = player.Score;
        }

        private void OnGUI()
        {
            if (!initialized) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = emphasisFont, fontSize = 28, alignment = TextAnchor.MiddleCenter };
                textStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                scoreStyle = new GUIStyle(titleStyle) { fontSize = 56 };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = emphasisFont, fontSize = 22,
                    alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
                foreach (GUIStyleState state in new[] { buttonStyle.normal, buttonStyle.hover,
                    buttonStyle.active, buttonStyle.focused })
                {
                    state.background = Texture2D.whiteTexture;
                    state.textColor = Color.white;
                }
            }
            Rect pixels = playCamera.pixelRect;
            if (pixels.width <= 0f) return;
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            Color originalBackground = GUI.backgroundColor;
            bool originalEnabled = GUI.enabled;
            Rect safe = Screen.safeArea;
            float left = Mathf.Max(pixels.xMin, safe.xMin);
            float bottom = Mathf.Max(pixels.yMin, safe.yMin);
            float right = Mathf.Min(pixels.xMax, safe.xMax);
            float top = Mathf.Min(pixels.yMax, safe.yMax);
            if (right <= left || top <= bottom) return;
            float scale = Mathf.Min((right - left) / 450f, (top - bottom) / 800f);
            float originX = left + (right - left - 450f * scale) * 0.5f;
            float originY = Screen.height - top + (top - bottom - 800f * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(originX, originY, 0f),
                Quaternion.identity, new Vector3(scale, scale, 1f));
            try
            {
                if (screen == ScreenState.Playing) DrawHud();
                else
                {
                    DrawPanel(new Rect(0, 0, 450, 800), new Color(0.035f, 0.025f, 0.09f, 0.72f));
                    GUI.enabled = originalEnabled && !inputBlocked;
                    if (screen == ScreenState.Start) DrawStartScreen();
                    else if (screen == ScreenState.Paused) DrawPauseScreen();
                    else DrawResultsScreen();
                }
            }
            finally
            {
                GUI.matrix = original;
                GUI.color = originalColor;
                GUI.backgroundColor = originalBackground;
                GUI.enabled = originalEnabled;
            }
        }

        private static void DrawPanel(Rect rect, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private bool PrimaryButton(Rect rect, string label)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.48f, 0.29f, 0.78f);
            bool clicked = GUI.Button(rect, label, buttonStyle);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private bool SecondaryButton(Rect rect, string label)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.18f, 0.12f, 0.28f);
            bool clicked = GUI.Button(rect, label, buttonStyle);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private void DrawStartScreen()
        {
            GUI.color = new Color(0.88f, 0.81f, 1f);
            GUI.Label(new Rect(25, 210, 400, 50), "GRAVITY RELAY", titleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(45, 290, 360, 100),
                "자동으로 공전합니다\n화면 어디든 톡 눌러 발사하세요\n보라색 고리 안으로 착륙하세요", textStyle);
            GUI.color = new Color(0.75f, 0.65f, 0.91f);
            GUI.Label(new Rect(25, 430, 400, 30), bestText, textStyle);
            GUI.color = Color.white;
            if (PrimaryButton(new Rect(40, 624, 370, 72), "시작하기")) StartRun();
            GUI.Label(new Rect(45, 718, 360, 28), "터치 · 클릭 · Space", textStyle);
        }

        private void DrawResultsScreen()
        {
            GUI.color = new Color(0.88f, 0.81f, 1f);
            GUI.Label(new Rect(25, 190, 400, 45), "이번 비행 완료", titleStyle);
            GUI.color = new Color(0.75f, 0.65f, 0.91f);
            GUI.Label(new Rect(40, 250, 370, 45), FailureMessage(), textStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(25, 320, 400, 26), "이번 점수", textStyle);
            GUI.Label(new Rect(25, 356, 400, 80), resultScoreText, scoreStyle);
            GUI.Label(new Rect(25, 466, 400, 30), bestText, textStyle);
            if (newRecord) GUI.Label(new Rect(25, 510, 400, 30), "새로운 최고 기록!", textStyle);
            if (PrimaryButton(new Rect(40, 610, 370, 72), "바로 다시하기")) StartRun();
            if (SecondaryButton(new Rect(95, 702, 260, 52), "시작 화면")) ShowTitle();
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

        private void DrawPauseScreen()
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(25, 260, 400, 45), "잠시 쉬어가기", titleStyle);
            GUI.Label(new Rect(25, 330, 400, 35), "다시 누르면 비행을 이어갑니다", textStyle);
            if (PrimaryButton(new Rect(40, 610, 370, 72), "계속하기")) ResumeRun();
            if (SecondaryButton(new Rect(95, 702, 260, 52), "시작 화면")) ShowTitle();
        }

        private void DrawHud()
        {
            // The next target is above the current orbit. Keep its approach clear with a small bottom HUD.
            DrawPanel(new Rect(18, 732, 414, 56), new Color(0.055f, 0.035f, 0.12f, 0.72f));
            GUI.color = Color.white;
            GUI.Label(new Rect(25, 736, 125, 46), scoreText, titleStyle);
            GUI.color = new Color(0.78f, 0.69f, 0.93f);
            GUI.Label(new Rect(275, 744, 145, 30), bestText, textStyle);
            if (Time.time - landedAt < feedbackDuration)
            {
                GUI.color = new Color(0.83f, 0.70f, 1f);
                GUI.Label(new Rect(170, 744, 90, 30), "+1", textStyle);
            }
            GUI.color = Color.white;
        }
    }
}
