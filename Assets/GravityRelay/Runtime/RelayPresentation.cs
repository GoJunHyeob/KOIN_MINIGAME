using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRelay
{
    public sealed class RelayPresentation : MonoBehaviour
    {
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private SpriteRenderer targetMarker;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Sprite bcsdSprite;
        [SerializeField] private SpriteRenderer mascot;
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
        private string targetText;
        private string statusText;
        private int displayedTarget = -1;
        private float nextTimerRefresh;
        private InputActionAsset ownedActions;
        private InputAction launchAction;
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
        }

        private void OnDisable()
        {
            launchAction?.Disable();
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

            if (launchAction.WasPressedThisFrame())
            {
                if (player.State == RelayState.Dead)
                    player.Restart();
                else
                    player.RequestLaunch();
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                player.Restart();

            if (displayedScore != player.Score)
            {
                displayedScore = player.Score;
                scoreText = player.Score.ToString("00");
            }
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
                statusText = null;
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
            Color success = new Color(0.35f, 1f, 0.65f);
            Color failure = new Color(1f, 0.28f, 0.32f);
            mascot.color = player.State == RelayState.Dead ? failure : landed ? success : mascotBaseColor;
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

            launchArrow.enabled = player.CanLaunch;
            if (launchArrow.enabled)
            {
                Vector2 direction = player.LaunchDirection;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Vector2 start = (Vector2)player.transform.position + direction * 0.3f;
                Vector2 tip = start + direction * arrowLength;
                float head = Mathf.Min(0.22f, arrowLength * 0.35f);
                launchArrow.startWidth = launchArrow.endWidth = arrowWidth;
                launchArrow.startColor = launchArrow.endColor = new Color(0.4f, 0.94f, 1f);
                launchArrow.SetPosition(0, start);
                launchArrow.SetPosition(1, tip);
                launchArrow.SetPosition(2, tip - direction * head + normal * head * 0.6f);
                launchArrow.SetPosition(3, tip);
                launchArrow.SetPosition(4, tip - direction * head - normal * head * 0.6f);
            }

            landingPulse.enabled = landed;
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
            float diameter = player.CaptureRadius(player.TargetPlanet) * 2f;
            Vector2 spriteSize = targetMarker.sprite.bounds.size;
            targetMarker.transform.localScale = new Vector3(diameter / spriteSize.x, diameter / spriteSize.y, 1f);
            if (displayedTarget != player.TargetIndex)
            {
                displayedTarget = player.TargetIndex;
                targetText = "BCSD  /  TARGET " + (displayedTarget + 1);
            }
            if (player.State == RelayState.Dead)
            {
                if (stateChanged || statusText == null)
                    statusText = player.FailureReason + "\nTAP / CLICK TO RESTART";
            }
            else if (landed)
                statusText = "LANDED!  +1";
            else if (player.State == RelayState.Flying)
            {
                if (stateChanged || Time.time >= nextTimerRefresh)
                {
                    statusText = "IN FLIGHT  " + player.FlightRemaining.ToString("0.0") + "s";
                    nextTimerRefresh = Time.time + 0.1f;
                }
            }
            else
                statusText = player.State == RelayState.Capturing ? "CAPTURE!"
                    : !player.CanLaunch ? "FRAMING NEXT ORBIT..." : "TAP / CLICK / SPACE TO LAUNCH";
            previousState = player.State;
            feedbackScore = player.Score;
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleCenter };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                scoreStyle = new GUIStyle(titleStyle) { fontSize = 52 };
            }
            Rect pixels = playCamera.pixelRect;
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            float scale = pixels.width / 450f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(pixels.x, Screen.height - pixels.yMax, 0f),
                Quaternion.identity, new Vector3(scale, scale, 1f));
            // The continuous map now passes behind the HUD; preserve score contrast.
            GUI.color = new Color(0.015f, 0.025f, 0.055f, 0.85f);
            GUI.DrawTexture(new Rect(0f, 0f, 450f, 175f), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.96f, 1f);
            GUI.Label(new Rect(0, 35, 450, 40), "GRAVITY RELAY", titleStyle);
            GUI.Label(new Rect(0, 78, 450, 65), scoreText, scoreStyle);
            GUI.Label(new Rect(0, 145, 450, 25), targetText, textStyle);
            GUI.color = player.State == RelayState.Dead ? new Color(1f, 0.4f, 0.4f)
                : Time.time - landedAt < feedbackDuration ? new Color(0.35f, 1f, 0.65f) : originalColor;
            GUI.Label(new Rect(0, 690, 450, 60), statusText, textStyle);
            GUI.color = originalColor;
            GUI.Label(new Rect(0, 755, 450, 25), "R RESTART  /  GOLD RING = LANDING ZONE", textStyle);
            Vector3 label = playCamera.WorldToScreenPoint(player.transform.position);
            GUI.Label(new Rect((label.x - pixels.x) / scale - 40,
                (pixels.yMax - label.y) / scale + 16, 80, 24), "BCSD", textStyle);
            GUI.matrix = original;
            GUI.color = originalColor;
        }
    }
}
