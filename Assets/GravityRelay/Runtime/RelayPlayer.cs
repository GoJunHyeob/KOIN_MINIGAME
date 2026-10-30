using UnityEngine;

namespace GravityRelay
{
    public enum RelayState { Orbiting, Flying, Capturing, Dead }

    // Transform motion is authoritative; no Rigidbody2D or collision solver is needed.
    public sealed class RelayPlayer : MonoBehaviour
    {
        [SerializeField] private RelayPlanet[] planets;
        [SerializeField] private Camera playCamera;
        [SerializeField, Min(0.05f)] private float orbitClearance = 0.38f;
        [SerializeField, Min(1f)] private float orbitDegreesPerSecond = 110f;
        [SerializeField, Range(5f, 7f)] private float launchSpeed = 6f;
        [SerializeField, Min(0f)] private float gravityStrength = 18f;
        [SerializeField, Min(0.01f)] private float softening = 0.8f;
        [SerializeField, Min(0.1f)] private float maxAcceleration = 12f;
        [SerializeField, Min(0f)] private float captureMargin = 0.22f;
        [SerializeField, Min(0.01f)] private float captureDuration = 0.15f;
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
                    float captureRadius = TargetPlanet.Radius + orbitClearance + captureMargin;
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
            (planet.Radius + orbitClearance);

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
