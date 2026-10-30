using System.Collections.Generic;
using UnityEngine;

namespace GravityRelay
{
    public sealed class RelayEndlessCourse : MonoBehaviour
    {
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private RelayPlanet[] planetTemplates;
        [Header("Generation")]
        [SerializeField] private int seed = 2718;
        [SerializeField] private Vector2 startPosition = new Vector2(-1f, -3.2f);
        [SerializeField, Range(8, 12)] private int poolSize = 8;
        [SerializeField, Min(2.8f), Tooltip("전체 생성 높이 차의 하한입니다.")]
        private float minVerticalGap = 3.1f;
        [SerializeField, Min(2.8f), Tooltip("난이도가 올라가도 넘지 않는 높이 차 상한입니다.")]
        private float maxVerticalGap = 3.7f;
        [SerializeField, Range(0f, 2f), Tooltip("모든 단계에서 유지되는 절대 좌우 생성 범위입니다.")]
        private float horizontalLimit = 1.6f;
        [SerializeField, Min(0f)] private float separationMargin = 0.15f;
        [SerializeField, Range(1, 20)] private int candidateAttempts = 10;
        [Header("Progressive difficulty: placement only")]
        [SerializeField] private bool progressiveDifficulty = true;
        [SerializeField, Min(0), Tooltip("이 점수까지 초반의 쉬운 생성 범위를 유지합니다.")]
        private int easyLandings = 4;
        [SerializeField, Min(1), Tooltip("이 점수부터는 생성 난이도를 더 높이지 않습니다.")]
        private int fullDifficultyScore = 30;
        [SerializeField, Min(2.8f)] private float earlyMinVerticalGap = 3.15f;
        [SerializeField, Min(2.8f)] private float earlyMaxVerticalGap = 3.3f;
        [SerializeField, Min(2.8f)] private float lateMinVerticalGap = 3.4f;
        [SerializeField, Min(0f), Tooltip("초반 목표와 현재 행성 사이 x 차이의 최대값입니다.")]
        private float earlyHorizontalStep = 0.45f;
        [SerializeField, Min(0f), Tooltip("후반 x 차이의 최대값. 절대 좌우 범위도 함께 적용됩니다.")]
        private float lateHorizontalStep = 2.8f;
        [Header("Camera and recycling")]
        [SerializeField, Min(0f)] private float cameraLookAhead = 3.2f;
        [SerializeField, Min(1f)] private float cameraSharpness = 12f;
        [SerializeField, Min(0f)] private float retireMargin = 1f;

        private readonly List<RelayPlanet> pool = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> active = new List<RelayPlanet>(12);
        private System.Random random;
        private RelayPlanet current;
        private RelayPlanet next;
        private float generationMinGap;
        private float generationMaxGap;
        private float generationMaxStep;
        public RelayPlanet CurrentPlanet => current;
        public RelayPlanet TargetPlanet => next != null ? next : current;
        public int ActiveCount => active.Count;
        public int PoolCount => pool.Count;
        public IReadOnlyList<RelayPlanet> ActivePlanets => active;
        public bool ReadyForLaunch => next != null &&
            (playCamera.transform.position - FramedCameraPosition()).sqrMagnitude <= 0.0004f;
        public Rect FramedBounds => BoundsAt(FramedCameraPosition());
        public int OpeningLandings => Mathf.Max(0, easyLandings);
        public int FullDifficultyAtScore => Mathf.Max(OpeningLandings + 1, fullDifficultyScore);
        public int CurrentScore => player != null ? player.Score : 0;
        public float AbsoluteHorizontalLimit => Mathf.Clamp(horizontalLimit, 0f, 2f);
        public float HardMinimumGap => Mathf.Max(2.8f, minVerticalGap);
        public float HardMaximumGap => Mathf.Max(HardMinimumGap, maxVerticalGap);

        public float DifficultyProgressAt(int score) => Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(OpeningLandings, FullDifficultyAtScore, Mathf.Max(0, score)));

        public void GetGenerationLimits(int score, out float minimumGap, out float maximumGap, out float maximumStep)
        {
            if (!progressiveDifficulty)
            {
                minimumGap = HardMinimumGap;
                maximumGap = HardMaximumGap;
                maximumStep = AbsoluteHorizontalLimit * 2f;
                return;
            }
            float progress = DifficultyProgressAt(score);
            float earlyMin = Mathf.Clamp(earlyMinVerticalGap, HardMinimumGap, HardMaximumGap);
            float earlyMax = Mathf.Clamp(earlyMaxVerticalGap, earlyMin, HardMaximumGap);
            float lateMin = Mathf.Clamp(lateMinVerticalGap, earlyMin, HardMaximumGap);
            float earlyStep = Mathf.Clamp(earlyHorizontalStep, 0f, AbsoluteHorizontalLimit * 2f);
            float lateStep = Mathf.Clamp(lateHorizontalStep, earlyStep, AbsoluteHorizontalLimit * 2f);
            minimumGap = Mathf.Lerp(earlyMin, lateMin, progress);
            maximumGap = Mathf.Lerp(earlyMax, HardMaximumGap, progress);
            maximumStep = Mathf.Lerp(earlyStep, lateStep, progress);
        }

        public void Configure(RelayPlayer relayPlayer, Camera camera, RelayPlanet[] templates)
        {
            player = relayPlayer;
            playCamera = camera;
            planetTemplates = templates;
        }

        public bool ResetCourse()
        {
            if (planetTemplates == null || planetTemplates.Length < 2 || player == null || playCamera == null)
                return false;
            if (pool.Count == 0)
            {
                foreach (RelayPlanet planet in planetTemplates)
                    pool.Add(planet);
                while (pool.Count < Mathf.Clamp(poolSize, 8, 12))
                {
                    RelayPlanet copy = Instantiate(planetTemplates[pool.Count % planetTemplates.Length],
                        planetTemplates[0].transform.parent);
                    copy.name = "Pooled Relay Planet " + pool.Count;
                    pool.Add(copy);
                }
            }
            random = new System.Random(seed);
            foreach (RelayPlanet planet in pool)
                planet.gameObject.SetActive(false);
            active.Clear();
            current = pool[0];
            current.transform.position = startPosition;
            current.gameObject.SetActive(true);
            active.Add(current);
            next = null;
            playCamera.transform.position = FramedCameraPosition();
            return PrepareNext();
        }

        public bool Advance()
        {
            current = next;
            next = null;
            RetirePastPlanets();
            return PrepareNext();
        }

        public void Tick(float dt)
        {
            if (player.State == RelayState.Orbiting)
            {
                Vector3 destination = FramedCameraPosition();
                Vector3 point = Vector3.Lerp(playCamera.transform.position, destination,
                    1f - Mathf.Exp(-cameraSharpness * dt));
                if ((point - destination).sqrMagnitude <= 0.0004f)
                    point = destination;
                playCamera.transform.position = point;
            }
            RetirePastPlanets();
        }

        private Vector3 FramedCameraPosition() => new Vector3(0f,
            current != null ? current.Center.y + cameraLookAhead : startPosition.y + cameraLookAhead,
            playCamera.transform.position.z);

        private Rect BoundsAt(Vector3 point)
        {
            float height = playCamera.orthographicSize;
            float width = height * (9f / 16f);
            return Rect.MinMaxRect(point.x - width, point.y - height, point.x + width, point.y + height);
        }

        private void RetirePastPlanets()
        {
            float bottom = BoundsAt(playCamera.transform.position).yMin - retireMargin;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RelayPlanet planet = active[i];
                if (planet != current && planet != next &&
                    planet.Center.y + player.CaptureRadius(planet) < bottom)
                {
                    planet.gameObject.SetActive(false);
                    active.RemoveAt(i);
                }
            }
        }

        private bool PrepareNext()
        {
            // Snapshot only at generation/landing. Flight parameters and existing targets never change.
            GetGenerationLimits(player.Score, out generationMinGap, out generationMaxGap, out generationMaxStep);
            RelayPlanet available = null;
            foreach (RelayPlanet planet in pool)
            {
                if (!planet.gameObject.activeSelf)
                {
                    available = planet;
                    break;
                }
            }
            if (available == null)
                return false;

            // A small predictable first step teaches the tangent shot before wider placements appear.
            Vector2 firstStep = new Vector2(Mathf.Clamp(current.Center.x + Mathf.Min(0.35f, generationMaxStep),
                -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit),
                current.Center.y + (generationMinGap + generationMaxGap) * 0.5f);
            if (player.Score == 0 && Accept(firstStep, available))
                return true;
            for (int attempt = 0; attempt < candidateAttempts; attempt++)
            {
                float deltaX = Mathf.Lerp(-generationMaxStep, generationMaxStep, (float)random.NextDouble());
                float x = Mathf.Clamp(current.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                float gap = Mathf.Lerp(generationMinGap, generationMaxGap, (float)random.NextDouble());
                if (Accept(new Vector2(x, current.Center.y + gap), available))
                    return true;
            }
            // Bounded deterministic fallback, still tested with the real movement model.
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float deltaX = attempt < 3 ? 0f : attempt < 6 ? -generationMaxStep : generationMaxStep;
                float x = Mathf.Clamp(current.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                float gap = Mathf.Lerp(generationMinGap, generationMaxGap, (attempt % 3) * 0.5f);
                if (Accept(new Vector2(x, current.Center.y + gap), available))
                    return true;
            }
            return false;
        }

        private bool Accept(Vector2 center, RelayPlanet candidate)
        {
            float gap = center.y - current.Center.y;
            const float tolerance = 0.0001f;
            if (gap < generationMinGap - tolerance || gap > generationMaxGap + tolerance ||
                Mathf.Abs(center.x) > AbsoluteHorizontalLimit + tolerance ||
                Mathf.Abs(center.x - current.Center.x) > generationMaxStep + tolerance)
                return false;
            foreach (RelayPlanet planet in active)
            {
                float distance = player.CaptureRadius(planet) + player.CaptureRadius(candidate) + separationMargin;
                if ((center - planet.Center).sqrMagnitude < distance * distance)
                    return false;
            }
            if (!player.TryFindLaunchWait(current, center, candidate.Radius, FramedBounds, out _))
                return false;
            candidate.transform.position = center;
            candidate.gameObject.SetActive(true);
            candidate.RefreshVisuals();
            active.Add(candidate);
            next = candidate;
            return true;
        }
    }
}
