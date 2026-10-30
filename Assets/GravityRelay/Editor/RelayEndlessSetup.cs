using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravityRelay.Editor
{
    public static class RelayEndlessSetup
    {
        [MenuItem("Gravity Relay/Apply Forward Map Defaults")]
        public static void ApplyForwardMapDefaults()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Apply saved map settings outside Play Mode.");
            RelayEndlessCourse course = UnityEngine.Object.FindFirstObjectByType<RelayEndlessCourse>();
            if (course == null)
                throw new InvalidOperationException("Configure the endless course first.");
            var settings = new SerializedObject(course);
            settings.FindProperty("poolSize").intValue = 12;
            settings.FindProperty("planetsAhead").intValue = 4;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
        }

        [MenuItem("Gravity Relay/Enable Endless Progression")]
        public static void EnableInCurrentScene()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Configure the course outside Play Mode.");
            RelayPlayer player = UnityEngine.Object.FindFirstObjectByType<RelayPlayer>();
            if (player == null)
                throw new InvalidOperationException("Open GravityRelay scene first.");
            var settings = new SerializedObject(player);
            Camera camera = (Camera)settings.FindProperty("playCamera").objectReferenceValue;
            SerializedProperty route = settings.FindProperty("planets");
            var templates = new RelayPlanet[route.arraySize];
            for (int i = 0; i < templates.Length; i++)
                templates[i] = (RelayPlanet)route.GetArrayElementAtIndex(i).objectReferenceValue;
            RelayEndlessCourse course = player.EndlessCourse;
            if (course == null)
            {
                var item = new GameObject("Endless Course");
                Undo.RegisterCreatedObjectUndo(item, "Enable Gravity Relay endless course");
                course = Undo.AddComponent<RelayEndlessCourse>(item);
            }
            Undo.RecordObject(player, "Connect endless course");
            Undo.RecordObject(course, "Configure endless course");
            course.Configure(player, camera, templates);
            player.SetEndlessCourse(course);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(course);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("Gravity Relay endless course configured. Save the scene, then Play.");
        }
    }
}
