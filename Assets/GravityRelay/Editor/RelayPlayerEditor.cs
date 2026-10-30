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
                "프리셋은 발사·중력·가속도 상한·포획 여유·공전 속도·착륙 보정 시간을 바꿉니다.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("쉬움")) ApplyPreset(12f, 55f, 0.42f, 125f);
            if (GUILayout.Button("기본: 빵·탁")) ApplyPreset(15f, 60f, 0.3f, 150f);
            if (GUILayout.Button("정밀")) ApplyPreset(18f, 70f, 0.18f, 170f);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset(float speed, float gravity, float margin, float orbitSpeed)
        {
            serializedObject.Update();
            serializedObject.FindProperty("launchSpeed").floatValue = speed;
            serializedObject.FindProperty("gravityStrength").floatValue = gravity;
            serializedObject.FindProperty("captureMargin").floatValue = margin;
            serializedObject.FindProperty("orbitDegreesPerSecond").floatValue = orbitSpeed;
            serializedObject.FindProperty("maxAcceleration").floatValue = 40f;
            serializedObject.FindProperty("captureDuration").floatValue = 0.06f;
            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }
    }

    public static class RelaySceneSetup
    {
        [MenuItem("Gravity Relay/Apply Punchy Baseline")]
        public static void ApplyPunchyBaseline()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Apply the saved baseline outside Play Mode.");
            RelayPlayer player = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
            if (player == null)
                throw new InvalidOperationException("Open GravityRelay scene first.");
            var settings = new SerializedObject(player);
            settings.FindProperty("launchSpeed").floatValue = 15f;
            settings.FindProperty("gravityStrength").floatValue = 60f;
            settings.FindProperty("maxAcceleration").floatValue = 40f;
            settings.FindProperty("captureMargin").floatValue = 0.3f;
            settings.FindProperty("orbitDegreesPerSecond").floatValue = 150f;
            settings.FindProperty("captureDuration").floatValue = 0.06f;
            settings.ApplyModifiedProperties();
            WireVisualsInCurrentScene();
            SceneView.RepaintAll();
        }

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
                Transform crater = null;
                // Names are used only for this explicit authoring/migration step, never at runtime.
                foreach (SpriteRenderer renderer in planet.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (renderer.gameObject.name == "Surface") surface = renderer;
                    if (renderer.gameObject.name == "Orbit guide") guide = renderer;
                    if (renderer.gameObject.name == "Crater") crater = renderer.transform;
                }
                if (surface == null || guide == null)
                    throw new InvalidOperationException("Missing surface or orbit guide on " + planet.name);
                planet.ConfigureVisuals(surface, guide, player, crater);
                EditorUtility.SetDirty(planet);
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

    }
}
