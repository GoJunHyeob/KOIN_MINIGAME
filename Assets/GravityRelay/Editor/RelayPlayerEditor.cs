using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravityRelay.Editor
{
    [CustomEditor(typeof(RelayPlayer))]
    public sealed class RelayPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("빠른 밸런스 비교", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("한 번에 한 값씩 조정해 비교하세요. Play 중 변경은 종료 시 되돌아갑니다. " +
                "프리셋은 속도·중력·포획 여유·공전 속도 네 값만 바꿉니다.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("쉬움")) ApplyPreset(6f, 22f, 0.35f, 95f);
            if (GUILayout.Button("기본")) ApplyPreset(6f, 18f, 0.22f, 110f);
            if (GUILayout.Button("정밀")) ApplyPreset(7f, 14f, 0.1f, 120f);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset(float speed, float gravity, float margin, float orbitSpeed)
        {
            serializedObject.Update();
            serializedObject.FindProperty("launchSpeed").floatValue = speed;
            serializedObject.FindProperty("gravityStrength").floatValue = gravity;
            serializedObject.FindProperty("captureMargin").floatValue = margin;
            serializedObject.FindProperty("orbitDegreesPerSecond").floatValue = orbitSpeed;
            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }
    }

    public static class RelaySceneSetup
    {
        [MenuItem("Gravity Relay/Refresh Balance Visual References")]
        public static void WireVisualsInCurrentScene()
        {
            RelayPlayer player = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
            if (player == null)
                throw new InvalidOperationException("Open GravityRelay scene first.");
            foreach (RelayPlanet planet in UnityEngine.Object.FindObjectsByType<RelayPlanet>(FindObjectsSortMode.None))
            {
                SpriteRenderer surface = null;
                SpriteRenderer guide = null;
                // Names are used only for this explicit authoring/migration step, never at runtime.
                foreach (SpriteRenderer renderer in planet.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (renderer.gameObject.name == "Surface") surface = renderer;
                    if (renderer.gameObject.name == "Orbit guide") guide = renderer;
                }
                if (surface == null || guide == null)
                    throw new InvalidOperationException("Missing surface or orbit guide on " + planet.name);
                planet.ConfigureVisuals(surface, guide, player);
                EditorUtility.SetDirty(planet);
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        public static void UpgradeAndExit()
        {
            try
            {
                EditorSceneManager.OpenScene(RelaySceneBuilder.ScenePath);
                WireVisualsInCurrentScene();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("GRAVITY_RELAY_BALANCE_COMPILE_AND_SETUP_PASSED");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
