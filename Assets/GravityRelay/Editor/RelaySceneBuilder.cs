using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GravityRelay.Editor
{
    public static class RelaySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GravityRelay.unity";
        private const string ArtPath = "Assets/GravityRelay/Art";

        [MenuItem("Gravity Relay/Create Prototype Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildScene();
        }

        public static void BuildAndValidate()
        {
            try
            {
                BuildScene();
                RunSmokeTests();
                Debug.Log("GRAVITY_RELAY_VALIDATION_PASSED");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("GravityRelay scene already exists; preserve it before rebuilding.");
            Directory.CreateDirectory(ArtPath);
            Sprite disc = CreateSprite("Disc", false);
            Sprite ring = CreateSprite("Ring", true);
            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, ArtPath + "/PrototypeSprite.mat");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.aspect = 9f / 16f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.09f);

            var route = new RelayPlanet[3];
            Vector2[] centers = { new Vector2(-1, -3.2f), new Vector2(1, 0), new Vector2(-0.75f, 3.2f) };
            Color[] colors = { new Color(0.18f, 0.68f, 0.77f), new Color(0.51f, 0.38f, 0.83f), new Color(0.91f, 0.43f, 0.38f) };
            var planetRoot = new GameObject("Planets (route 1 > 2 > 3 > 1)");
            for (int i = 0; i < route.Length; i++)
            {
                var planetObject = new GameObject("Planet " + (i + 1), typeof(RelayPlanet));
                planetObject.transform.SetParent(planetRoot.transform);
                planetObject.transform.position = centers[i];
                route[i] = planetObject.GetComponent<RelayPlanet>();
                MakeSprite("Surface", planetObject.transform, disc, material, colors[i], Vector2.zero, 1.6f, 0);
                MakeSprite("Orbit guide", planetObject.transform, ring, material,
                    new Color(0.5f, 0.8f, 1f, 0.22f), Vector2.zero, 2.36f, -1);
                MakeSprite("Crater", planetObject.transform, disc, material,
                    colors[i] * 0.75f, new Vector2(-0.2f, 0.15f), 0.38f, 1);
            }
            SpriteRenderer marker = MakeSprite("Next target halo", null, ring, material,
                new Color(1f, 0.82f, 0.3f), centers[1], 1.84f, 2);
            var playerObject = new GameObject("BCSD Player", typeof(RelayPlayer));
            RelayPlayer player = playerObject.GetComponent<RelayPlayer>();
            player.Configure(route, camera);
            playerObject.transform.position = centers[0] + Vector2.down * 1.18f;
            SpriteRenderer body = MakeSprite("BCSD Placeholder (replace via Presentation)", playerObject.transform,
                disc, material, new Color(1f, 0.89f, 0.57f), Vector2.zero, 0.42f, 10);
            // Decorations are normalized to the body's local scale.
            MakeSprite("Eye L", body.transform, disc, material, new Color(0.05f, 0.09f, 0.16f),
                new Vector2(-0.16f, 0.1f), 0.14f, 11);
            MakeSprite("Eye R", body.transform, disc, material, new Color(0.05f, 0.09f, 0.16f),
                new Vector2(0.16f, 0.1f), 0.14f, 11);
            var hud = new GameObject("Relay Input and HUD", typeof(RelayPresentation));
            hud.GetComponent<RelayPresentation>().Configure(player, camera, marker, body,
                AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Keep the project's existing startup/build settings intact.
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ScenePath);
        }

        private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite,
            Material material, Color color, Vector2 position, float diameter, int order)
        {
            var item = new GameObject(name, typeof(SpriteRenderer));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = Vector3.one * diameter;
            SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite CreateSprite(string name, bool hollow)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f);
                float alpha = Mathf.Clamp01(size * 0.5f - 1f - distance);
                if (hollow)
                    alpha *= Mathf.Clamp01(distance - (size * 0.5f - 4f));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            string path = ArtPath + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Gravity Relay/Validate Movement (outside Play Mode)")]
        public static void RunSmokeTests()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run this validation outside Play Mode.");
            RelayPlayer player = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
            if (player == null)
                throw new InvalidOperationException("Open GravityRelay scene first.");
            Vector3 originalPosition = player.transform.position;
            var waits = new List<int>();
            bool sawCapture = false;
            try
            {
                for (int leg = 0; leg < 6; leg++)
                {
                    bool landed = false;
                    for (int wait = 0; wait < 170 && !landed; wait++)
                    {
                        player.Restart();
                        foreach (int previous in waits)
                            Fly(player, previous, ref sawCapture);
                        int expectedScore = player.Score + 1;
                        Fly(player, wait, ref sawCapture);
                        if (player.State == RelayState.Orbiting && player.Score == expectedScore)
                        {
                            waits.Add(wait);
                            landed = true;
                            Debug.Log("RELAY_TEST landing " + expectedScore + ", wait ticks=" + wait);
                        }
                    }
                    Require(landed, "No reachable launch window on leg " + leg);
                }
                Require(sawCapture && player.Score == 6 && player.CurrentIndex == 0,
                    "Two complete loops must score six captures and return to planet one.");
                player.RequestLaunch();
                player.Restart();
                player.Simulate(0.02f);
                Require(player.State == RelayState.Orbiting && player.Score == 0,
                    "Restart must clear score, velocity and pending launch.");

                var settings = new SerializedObject(player);
                SerializedProperty timeLimit = settings.FindProperty("flightTimeLimit");
                float originalLimit = timeLimit.floatValue;
                try
                {
                    timeLimit.floatValue = 0.01f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    player.RequestLaunch();
                    player.Simulate(0.02f);
                    player.Simulate(0.02f);
                    Require(player.State == RelayState.Dead && player.FailureReason == "FLIGHT TIME EXPIRED",
                        "Flight limit must kill the player.");
                }
                finally
                {
                    timeLimit.floatValue = originalLimit;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                player.Restart();
                player.RequestLaunch();
                player.Simulate(0.02f);
                player.Simulate(2f);
                Require(player.State == RelayState.Dead && player.FailureReason == "OUT OF ORBIT",
                    "Leaving the camera bounds must kill the player before the flight timeout.");
                player.Restart();
                Require(player.Score == 0 && player.State == RelayState.Orbiting, "Restart after death failed.");
                Debug.Log("RELAY_TEST PASSED: six landings, capture state, score, timeout, screen exit, restart.");
            }
            finally
            {
                player.Restart();
                player.transform.position = originalPosition;
            }
        }

        private static void Fly(RelayPlayer player, int wait, ref bool sawCapture)
        {
            for (int tick = 0; tick < wait; tick++)
                player.Simulate(0.02f);
            player.RequestLaunch();
            player.Simulate(0.02f);
            for (int tick = 0; tick < 230; tick++)
            {
                player.Simulate(0.02f);
                sawCapture |= player.State == RelayState.Capturing;
                if (player.State == RelayState.Dead || player.State == RelayState.Orbiting)
                    return;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
