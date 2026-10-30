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
        [SerializeField, Range(12, 16)] private int poolSize = 12;
        [SerializeField, Range(4, 6), Tooltip("항상 앞쪽에 미리 배치할 행성 수입니다.")]
        private int planetsAhead = 4;
        [SerializeField, Min(2.8f), Tooltip("전체 생성 높이 차의 하한입니다.")]
        private float minVerticalGap = 4.2f;
        [SerializeField, Min(2.8f), Tooltip("난이도가 올라가도 넘지 않는 높이 차 상한입니다.")]
        private float maxVerticalGap = 5.4f;
        [SerializeField, Range(0f, 2.5f), Tooltip("모든 단계에서 유지되는 절대 좌우 생성 범위입니다.")]
        private float horizontalLimit = 2.2f;
        [SerializeField, Min(0f), Tooltip("수직으로 붙는 배치를 피하기 위한 최소 좌우 이동폭입니다.")]
        private float minimumHorizontalStep = 1.2f;
        [SerializeField, Min(0f)] private float separationMargin = 0.15f;
        [SerializeField, Range(1, 20)] private int candidateAttempts = 10;
        [Header("Progressive difficulty: placement only")]
        [SerializeField] private bool progressiveDifficulty = true;
        [SerializeField, Min(0), Tooltip("이 점수까지 초반의 쉬운 생성 범위를 유지합니다.")]
        private int easyLandings = 4;
        [SerializeField, Min(1), Tooltip("이 점수부터는 생성 난이도를 더 높이지 않습니다.")]
        private int fullDifficultyScore = 30;
        [SerializeField, Min(2.8f)] private float earlyMinVerticalGap = 4.2f;
        [SerializeField, Min(2.8f)] private float earlyMaxVerticalGap = 4.6f;
        [SerializeField, Min(2.8f)] private float lateMinVerticalGap = 4.8f;
        [SerializeField, Min(0f), Tooltip("초반 목표와 현재 행성 사이 x 차이의 최대값입니다.")]
        private float earlyHorizontalStep = 2.6f;
        [SerializeField, Min(0f), Tooltip("후반 x 차이의 최대값. 절대 좌우 범위도 함께 적용됩니다.")]
        private float lateHorizontalStep = 4.2f;
        [Header("Camera and recycling")]
        [SerializeField, Min(0f)] private float cameraLookAhead = 3.2f;
        [SerializeField, Min(1f)] private float cameraSharpness = 12f;
        [SerializeField, Min(0f)] private float retireMargin = 1f;

        private readonly List<RelayPlanet> pool = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> active = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> upcoming = new List<RelayPlanet>(6);
        private System.Random random;
        private RelayPlanet current;
        private RelayPlanet next;
        private float generationMinGap;
        private float generationMaxGap;
        private float generationMaxStep;
        public RelayPlanet CurrentPlanet => current;
        public RelayPlanet TargetPlanet => next != null ? next : current;
        public int ConfiguredAheadCount => Mathf.Clamp(planetsAhead, 4, 6);
        public bool ReadyForLaunch => next != null &&
            (playCamera.transform.position - FramedCameraPosition()).sqrMagnitude <= 0.0004f;
        public Rect FramedBounds => BoundsAt(FramedCameraPosition());
        public int OpeningLandings => Mathf.Max(0, easyLandings);
        public int FullDifficultyAtScore => Mathf.Max(OpeningLandings + 1, fullDifficultyScore);
        public int CurrentScore => player != null ? player.Score : 0;
        public float AbsoluteHorizontalLimit => Mathf.Clamp(horizontalLimit, 0f, 2.5f);
        public float MinimumHorizontalStep(float maximumStep) => Mathf.Clamp(minimumHorizontalStep, 0f, maximumStep);
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
                while (pool.Count < Mathf.Clamp(poolSize, 12, 16))
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
            upcoming.Clear();
            current = pool[0];
            current.transform.position = startPosition;
            current.gameObject.SetActive(true);
            active.Add(current);
            next = null;
            playCamera.transform.position = FramedCameraPosition();
            return FillHorizon();
        }

        public bool Advance()
        {
            if (upcoming.Count == 0)
                return false;
            current = upcoming[0];
            upcoming.RemoveAt(0);
            next = upcoming.Count > 0 ? upcoming[0] : null;
            RetirePastPlanets();
            // Recheck the actual arrival phase without moving a planet that was already visible.
            if (next != null && !player.TryFindLaunchWait(current, next.Center, next.Radius, FramedBounds, out _))
                return false;
            return FillHorizon();
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

        private Vector3 FramedCameraPosition() => CameraPositionFor(current);

        private Vector3 CameraPositionFor(RelayPlanet source) => new Vector3(0f,
            source != null ? source.Center.y + cameraLookAhead : startPosition.y + cameraLookAhead,
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
                if (planet != current && !upcoming.Contains(planet) &&
                    planet.Center.y + player.CaptureRadius(planet) < bottom)
                {
                    planet.gameObject.SetActive(false);
                    active.RemoveAt(i);
                }
            }
        }

        private bool FillHorizon()
        {
            while (upcoming.Count < ConfiguredAheadCount)
            {
                RelayPlanet source = upcoming.Count == 0 ? current : upcoming[upcoming.Count - 1];
                int futureScore = player.Score + upcoming.Count;
                if (!PrepareNext(source, futureScore))
                    return false;
            }
            next = upcoming[0];
            return true;
        }

        private bool PrepareNext(RelayPlanet source, int futureScore)
        {
            // Difficulty belongs to the score when this future leg will be played, not the generation frame.
            GetGenerationLimits(futureScore, out generationMinGap, out generationMaxGap, out generationMaxStep);
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

            // The opening jump already has a visible lateral offset; later jumps vary across the map.
            Vector2 firstStep = new Vector2(Mathf.Clamp(source.Center.x + (source.Center.x >= 0f ? -1f : 1f) *
                Mathf.Clamp(1.6f, MinimumHorizontalStep(generationMaxStep), generationMaxStep),
                -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit),
                source.Center.y + (generationMinGap + generationMaxGap) * 0.5f);
            if (futureScore == 0 && Accept(firstStep, available, source))
                return true;
            for (int attempt = 0; attempt < candidateAttempts; attempt++)
            {
                float inward = source.Center.x >= 0f ? -1f : 1f;
                float direction = random.NextDouble() < 0.75 ? inward : -inward;
                float deltaX = direction * Mathf.Lerp(MinimumHorizontalStep(generationMaxStep),
                    generationMaxStep, (float)random.NextDouble());
                float x = Mathf.Clamp(source.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                float gap = Mathf.Lerp(generationMinGap, generationMaxGap, (float)random.NextDouble());
                if (Accept(new Vector2(x, source.Center.y + gap), available, source))
                    return true;
            }
            // Bounded deterministic fallback, still tested with the real movement model.
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float inward = source.Center.x >= 0f ? -1f : 1f;
                float direction = attempt % 3 == 1 ? -inward : inward;
                float deltaX = direction * Mathf.Lerp(MinimumHorizontalStep(generationMaxStep),
                    generationMaxStep, (attempt / 3) * 0.5f);
                float x = Mathf.Clamp(source.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                float gap = Mathf.Lerp(generationMinGap, generationMaxGap, (attempt % 3) * 0.5f);
                if (Accept(new Vector2(x, source.Center.y + gap), available, source))
                    return true;
            }
            return false;
        }

        private bool Accept(Vector2 center, RelayPlanet candidate, RelayPlanet source)
        {
            float gap = center.y - source.Center.y;
            const float tolerance = 0.0001f;
            if (gap < generationMinGap - tolerance || gap > generationMaxGap + tolerance ||
                Mathf.Abs(center.x) > AbsoluteHorizontalLimit + tolerance ||
                Mathf.Abs(center.x - source.Center.x) > generationMaxStep + tolerance ||
                Mathf.Abs(center.x - source.Center.x) < MinimumHorizontalStep(generationMaxStep) - tolerance)
                return false;
            foreach (RelayPlanet planet in active)
            {
                float distance = player.CaptureRadius(planet) + player.CaptureRadius(candidate) + separationMargin;
                if ((center - planet.Center).sqrMagnitude < distance * distance)
                    return false;
            }
            Rect futureBounds = BoundsAt(CameraPositionFor(source));
            if (!player.CanReachFromEitherOrbit(source, center, candidate.Radius, futureBounds))
                return false;
            candidate.transform.position = center;
            candidate.gameObject.SetActive(true);
            candidate.RefreshVisuals();
            active.Add(candidate);
            upcoming.Add(candidate);
            next = upcoming[0];
            return true;
        }
    }
}
