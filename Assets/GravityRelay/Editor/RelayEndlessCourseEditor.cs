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
            EditorGUILayout.LabelField("연속 발사 성공 구간과 배치", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("횟수는 다음 이동을 성공했을 때의 횟수입니다. 첫 3회는 쉬운 배치를 유지합니다. " +
                "구간은 한 바퀴 안의 연속 입력 시간입니다. 배치가 없으면 Fallback Launch Window까지 완화합니다. 완만한 대각 흐름과 빠른 재출발을 우선합니다. 이미 보이는 행성은 옮기지 않습니다.", MessageType.Info);
            DrawStage(course, "튜토리얼 · 1회", 0);
            DrawStage(course, "초급 · 2~4회", 1);
            DrawStage(course, "보통 · 5~8회", 4);
            DrawStage(course, "어려움 · 9~15회", 8);
            DrawStage(course, "고급 · 16~25회", 15);
            DrawStage(course, "무한 · 26회 이상", 25);
            if (Application.isPlaying) DrawStage(course, "현재", course.CurrentScore);
        }

        private static void DrawStage(RelayEndlessCourse course, string label, int score)
        {
            course.GetGenerationLimits(score, out float minimum, out float maximum, out float step);
            EditorGUILayout.LabelField(label + " / " + course.MinimumWindowAtScore(score).ToString("0.00") + "초 이상");
            EditorGUILayout.LabelField("높이 " + minimum.ToString("0.00") + "~" + maximum.ToString("0.00") +
                " / 좌우 변화 " + course.MinimumHorizontalStep(step).ToString("0.00") + "~" + step.ToString("0.00"), EditorStyles.miniLabel);
        }
    }
}
