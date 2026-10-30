using System;
using UnityEditor;
using UnityEngine;

namespace GravityRelay.Editor
{
    public static class RelayEndlessValidation
    {
        [MenuItem("Gravity Relay/Validate Endless Progression")]
        public static void RunSmokeTests()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run outside Play Mode.");
            RelayPlayer source = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
            if (source == null || source.EndlessCourse == null)
                throw new InvalidOperationException("Open the configured endless scene first.");
            var root = new GameObject("Temporary endless validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var sourceSettings = new SerializedObject(source);
                var cameraObject = new GameObject("Validation camera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = new Vector3(0, 0, -10);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = ((Camera)sourceSettings.FindProperty("playCamera").objectReferenceValue).orthographicSize;
                camera.aspect = 9f / 16f;
                camera.enabled = false;
                SerializedProperty route = sourceSettings.FindProperty("planets");
                var templates = new RelayPlanet[route.arraySize];
                for (int i = 0; i < templates.Length; i++)
                    templates[i] = UnityEngine.Object.Instantiate((RelayPlanet)route.GetArrayElementAtIndex(i).objectReferenceValue,
                        root.transform);
                RelayPlayer player = UnityEngine.Object.Instantiate(source, root.transform);
                RelayEndlessCourse course = UnityEngine.Object.Instantiate(source.EndlessCourse, root.transform);
                player.Configure(templates, camera);
                player.SetEndlessCourse(course);
                course.Configure(player, camera, templates);
                player.Restart();
                Require(player.CanLaunch, "First route must be ready.");
                Vector3 initialCamera = camera.transform.position;
                Vector2 initialTarget = player.TargetPlanet.Center;
                int initialPoolCount = course.PoolCount;
                int previewCount = course.ConfiguredAheadCount;
                Require(course.AheadCount == previewCount, "Initial forward map was not filled.");
                var initialMap = new Vector2[previewCount];
                for (int i = 0; i < previewCount; i++) initialMap[i] = course.UpcomingPlanets[i].Center;
                var physics = new SerializedObject(player);
                string[] protectedFields = { "launchSpeed", "gravityStrength", "maxAcceleration",
                    "orbitDegreesPerSecond", "captureMargin", "captureDuration" };
                var originalPhysics = new float[protectedFields.Length];
                for (int i = 0; i < protectedFields.Length; i++)
                    originalPhysics[i] = physics.FindProperty(protectedFields[i]).floatValue;
                ValidateDifficultyCurve(course);
                float dt = Time.fixedDeltaTime;
                int maxActive = 0;
                for (int leg = 0; leg < 100; leg++)
                {
                    int guard = 0;
                    while (!player.CanLaunch && player.State != RelayState.Dead && guard++ < 300)
                        player.Simulate(dt);
                    Require(player.CanLaunch, "Camera never settled on leg " + leg);
                    Require(course.AheadCount == previewCount, "Forward map shrank.");
                    ValidateUpcomingRanges(player, course);
                    Require(player.TargetPlanet.Center.y > player.CurrentPlanet.Center.y, "Target must be above player.");
                    course.GetGenerationLimits(player.Score, out float minimumGap, out float maximumGap, out float maximumStep);
                    Vector2 separation = player.TargetPlanet.Center - player.CurrentPlanet.Center;
                    Require(separation.y >= minimumGap - 0.001f && separation.y <= maximumGap + 0.001f,
                        "Generated height violates the score's difficulty range.");
                    Require(Mathf.Abs(separation.x) <= maximumStep + 0.001f &&
                        Mathf.Abs(player.TargetPlanet.Center.x) <= course.AbsoluteHorizontalLimit + 0.001f,
                        "Generated lateral step violates the score's difficulty range.");
                    Require(player.TryFindLaunchWait(player.CurrentPlanet, player.TargetPlanet.Center,
                        player.TargetPlanet.Radius, course.FramedBounds, out int wait), "Unreachable target on leg " + leg);
                    for (int tick = 0; tick < wait; tick++) player.Simulate(dt);
                    int previousScore = player.Score;
                    float targetHeight = player.TargetPlanet.Center.y;
                    var oldFuture = new RelayPlanet[previewCount];
                    var oldPositions = new Vector2[previewCount];
                    for (int i = 0; i < previewCount; i++)
                    {
                        oldFuture[i] = course.UpcomingPlanets[i];
                        oldPositions[i] = oldFuture[i].Center;
                    }
                    player.RequestLaunch();
                    player.Simulate(dt);
                    Require(player.State == RelayState.Flying, "Launch rejected after framing.");
                    Vector3 lockedCamera = camera.transform.position;
                    guard = 0;
                    while (player.State != RelayState.Orbiting && player.State != RelayState.Dead && guard++ < 500)
                    {
                        player.RequestLaunch(); // Flight/capture presses must not queue another launch.
                        player.Simulate(dt);
                        Require(camera.transform.position == lockedCamera, "Camera moved during flight/capture.");
                    }
                    Require(player.State == RelayState.Orbiting && player.Score == previousScore + 1,
                        "Landing failed on leg " + leg + ": " + player.FailureReason);
                    Require(Mathf.Abs(player.CurrentPlanet.Center.y - targetHeight) < 0.001f, "Landed on wrong planet.");
                    Require(player.TargetIndex == player.Score + 1, "Target label wrapped instead of increasing.");
                    for (int i = 0; i < previewCount - 1; i++)
                        Require(course.UpcomingPlanets[i] == oldFuture[i + 1] &&
                            course.UpcomingPlanets[i].Center == oldPositions[i + 1],
                            "A previously visible future planet moved or was replaced.");
                    RelayPlanet newTail = course.UpcomingPlanets[previewCount - 1];
                    Require(newTail.Center.y - newTail.Radius > lockedCamera.y + camera.orthographicSize,
                        "A new planet popped into the visible flight frame.");
                    maxActive = Mathf.Max(maxActive, course.ActiveCount);
                    Require(course.PoolCount == initialPoolCount && course.ActiveCount <= initialPoolCount,
                        "Pool grew beyond its budget.");
                    var active = course.ActivePlanets;
                    for (int a = 0; a < active.Count; a++)
                    for (int b = a + 1; b < active.Count; b++)
                        Require(Vector2.Distance(active[a].Center, active[b].Center) > active[a].Radius + active[b].Radius,
                            "Visible planets overlap.");
                }
                Require(player.Score == 100 && camera.transform.position.y > initialCamera.y + 200f,
                    "Course did not continue upward.");
                physics.Update();
                for (int i = 0; i < protectedFields.Length; i++)
                    Require(physics.FindProperty(protectedFields[i]).floatValue == originalPhysics[i],
                        "Progression altered player physics: " + protectedFields[i]);
                player.Restart();
                Require(player.Score == 0 && player.CanLaunch && course.ActiveCount == previewCount + 1 && course.PoolCount == initialPoolCount,
                    "Restart failed to reset/reuse the pool.");
                Require(camera.transform.position == initialCamera && player.TargetPlanet.Center == initialTarget,
                    "Restart failed to restore camera and deterministic first target.");
                for (int i = 0; i < previewCount; i++)
                    Require(course.UpcomingPlanets[i].Center == initialMap[i], "Restart changed the prebuilt map.");
                player.RequestLaunch();
                player.Restart();
                player.Simulate(dt);
                Require(player.State == RelayState.Orbiting, "Restart retained a pending launch.");
                player.RequestLaunch();
                player.Simulate(dt);
                for (int tick = 0; tick < 500 && player.State != RelayState.Dead && player.Score == 0; tick++)
                    player.Simulate(dt);
                Require(player.State == RelayState.Dead && player.FailureReason == "OUT OF ORBIT",
                    "A missed shot must still fail at the frozen screen boundary.");
                player.Restart();
                var settings = new SerializedObject(player);
                settings.FindProperty("flightTimeLimit").floatValue = 0.01f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                player.RequestLaunch();
                player.Simulate(dt);
                player.Simulate(dt);
                Require(player.State == RelayState.Dead && player.FailureReason == "FLIGHT TIME EXPIRED",
                    "Flight timeout failed.");
                Debug.Log("RELAY_ENDLESS_PASSED: 100 landings, upward targets, frozen flight camera, no overlaps, " +
                    "pool=" + initialPoolCount + ", peak active=" + maxActive +
                    ", difficulty ranges/caps, unchanged physics, restart, screen exit and timeout.");
                Debug.Log("RELAY_HORIZON_PASSED: ahead=" + previewCount +
                    ", stable visible planets, offscreen tail generation, future-score difficulty and full map restart.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateUpcomingRanges(RelayPlayer player, RelayEndlessCourse course)
        {
            for (int i = 0; i < course.AheadCount; i++)
            {
                RelayPlanet from = i == 0 ? player.CurrentPlanet : course.UpcomingPlanets[i - 1];
                RelayPlanet to = course.UpcomingPlanets[i];
                course.GetGenerationLimits(player.Score + i, out float min, out float max, out float step);
                Vector2 separation = to.Center - from.Center;
                Require(separation.y >= min - 0.001f && separation.y <= max + 0.001f &&
                    Mathf.Abs(separation.x) <= step + 0.001f &&
                    Mathf.Abs(to.Center.x) <= course.AbsoluteHorizontalLimit + 0.001f,
                    "A prebuilt leg uses the wrong future-score difficulty.");
            }
        }

        private static void ValidateDifficultyCurve(RelayEndlessCourse course)
        {
            course.GetGenerationLimits(0, out float firstMin, out float firstMax, out float firstStep);
            float lastMin = firstMin;
            float lastMax = firstMax;
            float lastStep = firstStep;
            int finalScore = course.FullDifficultyAtScore;
            for (int score = 0; score <= finalScore + 10; score++)
            {
                course.GetGenerationLimits(score, out float min, out float max, out float step);
                Require(min >= lastMin && max >= lastMax && step >= lastStep, "Difficulty envelope decreased.");
                Require(min >= course.HardMinimumGap && min <= max && max <= course.HardMaximumGap &&
                    step >= 0f && step <= course.AbsoluteHorizontalLimit * 2f, "Difficulty escaped hard caps.");
                if (score <= course.OpeningLandings)
                    Require(min == firstMin && max == firstMax && step == firstStep, "Opening is not stable.");
                if (score > finalScore)
                    Require(min == lastMin && max == lastMax && step == lastStep, "Late difficulty did not plateau.");
                lastMin = min;
                lastMax = max;
                lastStep = step;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
