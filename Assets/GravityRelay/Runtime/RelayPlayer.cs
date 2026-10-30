using UnityEngine;

namespace GravityRelay
{
    public enum RelayState { Orbiting, Flying, Capturing, Dead }

    // Transform motion is authoritative; no Rigidbody2D or collision solver is needed.
    public sealed class RelayPlayer : MonoBehaviour
    {
        [SerializeField] private RelayPlanet[] planets;
        [SerializeField] private Camera playCamera;
        [Header("Balance: orbit and launch")]
        [SerializeField, Min(0.05f), Tooltip("행성 표면과 캐릭터 중심 사이 여유. 가이드 링도 함께 변경됩니다.")]
        private float orbitClearance = 0.38f;
        [SerializeField, Min(1f), Tooltip("공전 각속도. 높을수록 발사 타이밍을 맞추기 어렵습니다.")]
        private float orbitDegreesPerSecond = 150f;
        [SerializeField, Range(10f, 20f), Tooltip("접선 방향 발사 속도 (units/s). 중력과 별도로 조정합니다.")]
        private float launchSpeed = 15f;
        [Header("Balance: gravity")]
        [SerializeField, Min(0f), Tooltip("목표 중력 세기. 높이면 궤적이 더 강하게 휘어집니다.")]
        private float gravityStrength = 60f;
        [SerializeField, Min(0.01f)] private float softening = 0.8f;
        [SerializeField, Min(0.1f)] private float maxAcceleration = 40f;
        [Header("Balance: landing and failure")]
        [SerializeField, Min(0f), Tooltip("공전 반경 바깥의 추가 포획 여유. 금색 링이 실제 포획 반경입니다.")]
        private float captureMargin = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("착륙 위치 보정 시간. 짧을수록 탁 잡히고, 길수록 부드럽게 붙습니다.")]
        private float captureDuration = 0.06f;
        [SerializeField, Min(0.1f)] private float flightTimeLimit = 4f;
        [SerializeField, Min(0f)] private float screenExitMargin = 0.25f;

        private Vector2 position;
        private Vector2 velocity;
        private Vector2 captureStart;
        private float angle;
        private float captureAngle;
        private float stateTime;
        private float orbitDirection = 1f;
        private bool launchQueued;
        public RelayState State { get; private set; }
        public int Score { get; private set; }
        public int CurrentIndex { get; private set; }
        public int TargetIndex => (CurrentIndex + 1) % planets.Length;
        public float FlightRemaining => Mathf.Max(0f, flightTimeLimit - stateTime);
        public string FailureReason { get; private set; }
        public Vector2 Velocity => velocity;
        public Vector2 LaunchDirection
        {
            get
            {
                Vector2 radial = ((Vector2)transform.position - CurrentPlanet.Center).normalized;
                return new Vector2(-radial.y, radial.x) * orbitDirection;
            }
        }
        public float OrbitRadius(RelayPlanet planet) => planet.Radius + orbitClearance;
        public float CaptureRadius(RelayPlanet planet) => OrbitRadius(planet) + captureMargin;
        public RelayPlanet CurrentPlanet => planets[CurrentIndex];
        public RelayPlanet TargetPlanet => planets[TargetIndex];

        public void Configure(RelayPlanet[] route, Camera camera)
        {
            planets = route;
            playCamera = camera;
        }

        private void Start()
        {
            if (planets == null || planets.Length < 2 || playCamera == null ||
                System.Array.Exists(planets, planet => planet == null))
            {
                Debug.LogError("Gravity Relay needs a camera and at least two planets.", this);
                enabled = false;
                return;
            }
            Restart();
        }

        public void Restart()
        {
            Score = 0;
            CurrentIndex = 0;
            angle = -90f * Mathf.Deg2Rad;
            orbitDirection = 1f;
            velocity = Vector2.zero;
            stateTime = 0f;
            launchQueued = false;
            FailureReason = string.Empty;
            State = RelayState.Orbiting;
            position = OrbitPosition(CurrentPlanet, angle);
            ApplyPose();
        }

        public void RequestLaunch()
        {
            if (State == RelayState.Orbiting)
                launchQueued = true;
        }

        private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

        // Also used by the editor smoke harness to exercise the real movement rules.
        public void Simulate(float deltaTime)
        {
            if (State == RelayState.Dead || deltaTime <= 0f)
                return;

            stateTime += deltaTime;
            switch (State)
            {
                case RelayState.Orbiting:
                    if (launchQueued)
                    {
                        launchQueued = false;
                        Vector2 radial = (position - CurrentPlanet.Center).normalized;
                        velocity = new Vector2(-radial.y, radial.x) * (launchSpeed * orbitDirection);
                        State = RelayState.Flying;
                        stateTime = 0f;
                    }
                    else
                    {
                        angle += orbitDegreesPerSecond * Mathf.Deg2Rad * orbitDirection * deltaTime;
                        position = OrbitPosition(CurrentPlanet, angle);
                    }
                    break;
                case RelayState.Flying:
                    Vector2 offset = TargetPlanet.Center - position;
                    float acceleration = Mathf.Min(maxAcceleration,
                        gravityStrength / (offset.sqrMagnitude + softening * softening));
                    velocity += offset.normalized * (acceleration * deltaTime);
                    Vector2 next = position + velocity * deltaTime;
                    float captureRadius = CaptureRadius(TargetPlanet);
                    // Swept segment check prevents tunnelling at high speed or low tick rates.
                    Vector2 step = next - position;
                    float t = step.sqrMagnitude > 0f
                        ? Mathf.Clamp01(Vector2.Dot(TargetPlanet.Center - position, step) / step.sqrMagnitude)
                        : 0f;
                    Vector2 closest = position + step * t;
                    if ((closest - TargetPlanet.Center).sqrMagnitude <= captureRadius * captureRadius)
                    {
                        position = closest;
                        BeginCapture();
                    }
                    else
                    {
                        position = next;
                        if (stateTime >= flightTimeLimit)
                            Die("FLIGHT TIME EXPIRED");
                        else if (OutsidePlayArea())
                            Die("OUT OF ORBIT");
                    }
                    break;
                case RelayState.Capturing:
                    float progress = Mathf.Clamp01(stateTime / captureDuration);
                    position = Vector2.Lerp(captureStart, OrbitPosition(TargetPlanet, captureAngle),
                        Mathf.SmoothStep(0f, 1f, progress));
                    if (progress >= 1f)
                    {
                        CurrentIndex = TargetIndex;
                        angle = captureAngle;
                        Score++;
                        State = RelayState.Orbiting;
                        stateTime = 0f;
                    }
                    break;
            }
            ApplyPose();
        }

        private void BeginCapture()
        {
            Vector2 radial = position - TargetPlanet.Center;
            captureAngle = Mathf.Atan2(radial.y, radial.x);
            float cross = radial.x * velocity.y - radial.y * velocity.x;
            orbitDirection = cross >= 0f ? 1f : -1f;
            captureStart = position;
            velocity = Vector2.zero;
            stateTime = 0f;
            launchQueued = false;
            State = RelayState.Capturing;
        }

        private Vector2 OrbitPosition(RelayPlanet planet, float radians) =>
            planet.Center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) *
            OrbitRadius(planet);

        private bool OutsidePlayArea()
        {
            Vector3 lower = playCamera.ViewportToWorldPoint(new Vector3(0f, 0f, 10f));
            Vector3 upper = playCamera.ViewportToWorldPoint(new Vector3(1f, 1f, 10f));
            return position.x < lower.x - screenExitMargin || position.x > upper.x + screenExitMargin ||
                   position.y < lower.y - screenExitMargin || position.y > upper.y + screenExitMargin;
        }

        private void Die(string reason)
        {
            State = RelayState.Dead;
            FailureReason = reason;
            velocity = Vector2.zero;
            launchQueued = false;
        }

        private void ApplyPose()
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            // Keep the placeholder mascot upright and readable.
        }
    }
}
