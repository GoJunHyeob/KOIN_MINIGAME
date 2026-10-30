using UnityEditor;
using UnityEngine;

namespace GravityRelay.Editor
{
    [CustomEditor(typeof(RelayEndlessCourse))]
    public sealed class RelayEndlessCourseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var course = (RelayEndlessCourse)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("생성 범위 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("난이도는 다음 목표를 생성할 때만 적용됩니다. 발사 속도·중력은 바뀌지 않습니다. " +
                "높이/좌우 상한과 도달 가능성 검사는 모든 단계와 대체 후보에 적용됩니다.", MessageType.Info);
            DrawStage(course, "초반", 0);
            DrawStage(course, "중반", (course.OpeningLandings + course.FullDifficultyAtScore) / 2);
            DrawStage(course, "후반", course.FullDifficultyAtScore);
            if (Application.isPlaying) DrawStage(course, "현재", course.CurrentScore);
        }

        private static void DrawStage(RelayEndlessCourse course, string label, int score)
        {
            course.GetGenerationLimits(score, out float minimum, out float maximum, out float step);
            EditorGUILayout.LabelField(label + " (" + score + "점)");
            EditorGUILayout.LabelField("높이 " + minimum.ToString("0.00") + "~" + maximum.ToString("0.00") +
                " / 좌우 변화 " + course.MinimumHorizontalStep(step).ToString("0.00") + "~" + step.ToString("0.00"), EditorStyles.miniLabel);
        }
    }
}
