using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GravityRelay.Editor
{
    // Opt-in integration check. Never runs merely from opening/importing the project.
    [InitializeOnLoad]
    public static class RelayPlayValidation
    {
        private const string ActiveKey = "GravityRelay.PlayValidation";
        private static int stage;
        private static double started;
        private static double stepStarted;
        private static Mouse mouse;
        private static Touchscreen touch;
        private static Keyboard keyboard;
        private static bool failed;

        static RelayPlayValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += CheckLog;
        }

        public static void RunBatch()
        {
            stage = 0;
            failed = false;
            EditorSceneManager.OpenScene(RelaySceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void CheckLog(string condition, string trace, LogType type)
        {
            if (SessionState.GetBool(ActiveKey, false) &&
                (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                failed = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
                return;
            try
            {
                RelayPlayer player = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
                if (player == null)
                    throw new InvalidOperationException("Player missing in Play Mode.");
                double now = EditorApplication.timeSinceStartup;
                if (stage == 0)
                {
                    // A hidden batch editor has no focused Game view. Isolate that test-only setting.
                    InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    Application.runInBackground = true;
                    started = stepStarted = now;
                    mouse = InputSystem.AddDevice<Mouse>();
                    touch = InputSystem.AddDevice<Touchscreen>();
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    stage++;
                }
                if (now - started > 20)
                    throw new TimeoutException("Play Mode input smoke test timed out at stage " + stage);

                switch (stage)
                {
                    case 1:
                        if (now - stepStarted < 0.6)
                            return;
                        Require(player.State == RelayState.Orbiting, "Scene must start orbiting.");
                        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                        stepStarted = now;
                        stage++;
                        break;
                    case 2:
                        if (now - stepStarted < 0.15)
                            return;
                        if (player.State == RelayState.Orbiting && now - stepStarted < 2)
                            return;
                        Require(player.State != RelayState.Orbiting, "Mouse input did not launch.");
                        InputSystem.QueueStateEvent(mouse, new MouseState());
                        var settings = new SerializedObject(player);
                        settings.FindProperty("flightTimeLimit").floatValue = 0.5f;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        Debug.Log("RELAY_PLAY mouse launch passed.");
                        stage++;
                        break;
                    case 3:
                        if (player.State != RelayState.Dead)
                            return;
                        InputSystem.QueueStateEvent(touch, new TouchState
                        {
                            touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began,
                            position = new Vector2(320, 240), pressure = 1f
                        });
                        stepStarted = now;
                        stage++;
                        break;
                    case 4:
                        if (now - stepStarted < 0.15)
                            return;
                        Require(player.State == RelayState.Orbiting && player.Score == 0,
                            "Touch must restart from Dead.");
                        InputSystem.QueueStateEvent(touch, new TouchState
                        {
                            touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended,
                            position = new Vector2(320, 240)
                        });
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                        Debug.Log("RELAY_PLAY touch restart passed.");
                        stepStarted = now;
                        stage++;
                        break;
                    case 5:
                        if (now - stepStarted < 0.15)
                            return;
                        Require(player.State == RelayState.Flying, "Space input did not launch.");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                        stepStarted = now;
                        stage++;
                        break;
                    case 6:
                        if (now - stepStarted < 0.15)
                            return;
                        Require(player.State == RelayState.Orbiting && player.Score == 0,
                            "R must restart during flight.");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Require(!failed, "Runtime Console reported errors.");
                        Debug.Log("GRAVITY_RELAY_PLAY_PASSED: startup, mouse launch, touch restart, space launch, R restart; no runtime errors.");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(ActiveKey, false);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (touch != null) InputSystem.RemoveDevice(touch);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
