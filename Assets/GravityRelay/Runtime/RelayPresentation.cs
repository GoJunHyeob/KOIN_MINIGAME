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
        }

        private void Start()
        {
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

            targetMarker.transform.position = player.TargetPlanet.transform.position;
            targetMarker.transform.localScale = Vector3.one * (player.TargetPlanet.Radius * 2f + 0.24f);
            if (displayedScore != player.Score)
            {
                displayedScore = player.Score;
                scoreText = player.Score.ToString("00");
            }
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
            GUI.color = new Color(0.86f, 0.96f, 1f);
            GUI.Label(new Rect(0, 35, 450, 40), "GRAVITY RELAY", titleStyle);
            GUI.Label(new Rect(0, 78, 450, 65), scoreText, scoreStyle);
            GUI.Label(new Rect(0, 145, 450, 25), "BCSD  /  TARGET " + (player.TargetIndex + 1), textStyle);
            string hint = player.State == RelayState.Dead
                ? player.FailureReason + "\nTAP TO RESTART"
                : player.State == RelayState.Flying
                    ? "IN FLIGHT  " + player.FlightRemaining.ToString("0.0") + "s"
                    : player.State == RelayState.Capturing ? "CAPTURE!" : "TAP / CLICK / SPACE TO LAUNCH";
            GUI.Label(new Rect(0, 690, 450, 60), hint, textStyle);
            GUI.Label(new Rect(0, 755, 450, 25), "R  RESTART    /    FOLLOW THE GOLD HALO", textStyle);
            Vector3 label = playCamera.WorldToScreenPoint(player.transform.position);
            GUI.Label(new Rect((label.x - pixels.x) / scale - 40,
                (pixels.yMax - label.y) / scale + 16, 80, 24), "BCSD", textStyle);
            GUI.matrix = original;
            GUI.color = originalColor;
        }
    }
}
