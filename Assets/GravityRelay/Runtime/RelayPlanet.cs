using UnityEngine;

namespace GravityRelay
{
    public sealed class RelayPlanet : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 0.8f;
        public float Radius => radius;
        public Vector2 Center => transform.position;
    }
}
