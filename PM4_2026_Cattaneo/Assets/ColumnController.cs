using Ludocore;
using UnityEngine;

/// <summary>
/// Reads nearest detection from a `Sensor` (e.g. `ProximitySensor`) or from a `SensorResponse`
/// and drives the HDR emissive color intensity of the assigned `Renderer`'s material (URP).
/// - Supports distance→intensity curve
/// - Optional smoothing
/// - Can use `SensorResponse` events to avoid polling the `Sensor`
/// </summary>
public class ColumnController : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Sensor to read nearest detection from (ProximitySensor or any Ludocore.Sensor). " +
             "If `sensorResponse` is assigned it takes precedence and this sensor will not be polled.")]
    [SerializeField] private Sensor sensor;

    [Tooltip("Optional: listen to a SensorResponse to receive nearest position events instead of polling Sensor.")]
    [SerializeField] private SensorResponse sensorResponse;

    [Header("Target")]
    [Tooltip("Renderer whose material emission will be driven (will be instanced at runtime)")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Emission")]
    [Tooltip("Base HDR color (set in inspector as HDR)")]
    [SerializeField] private Color baseEmissiveColor = Color.white;
    [Tooltip("Shader property name for emission. URP/Standard typically uses _EmissionColor")]
    [SerializeField] private string emissionProperty = "_EmissionColor";

    [Header("Distance -> Intensity")]
    [Tooltip("Distance at which intensity reaches maximum")]
    [SerializeField, Min(0f)] private float minDistance = 0.5f;
    [Tooltip("Distance at which intensity reaches minimum (no effect)")]
    [SerializeField, Min(0f)] private float maxDistance = 5f;
    [Tooltip("Intensity when at or closer than MinDistance")]
    [SerializeField] private float maxIntensity = 5f;
    [Tooltip("Intensity when at or farther than MaxDistance")]
    [SerializeField] private float minIntensity = 0f;

    [Header("Curve & Smoothing")]
    [Tooltip("Remap the normalized distance-to-1 (near) value with this curve (input 0..1).")]
    [SerializeField] private AnimationCurve distanceToMultiplier = default;
    [Tooltip("Smooth time for intensity changes (seconds). 0 = no smoothing")]
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;

    private Material _materialInstance;
    private float _currentIntensity;
    private float _intensityVelocity;
    private bool _hasSignal;
    private float _eventDistance = float.MaxValue;

    private void Reset()
    {
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        if (distanceToMultiplier == null || distanceToMultiplier.length == 0)
        {
            // default linear falloff: 1 at near, 0 at far
            distanceToMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }
    }

    private void OnEnable()
    {
        if (sensorResponse != null)
        {
            sensorResponse.OnWhileDetected += HandleWhileDetected;
            sensorResponse.OnAllLost += HandleAllLost;
        }
    }

    private void OnDisable()
    {
        if (sensorResponse != null)
        {
            sensorResponse.OnWhileDetected -= HandleWhileDetected;
            sensorResponse.OnAllLost -= HandleAllLost;
        }
    }

    private void Start()
    {
        if (targetRenderer)
        {
            // Instance the material so we don't modify shared asset
            _materialInstance = targetRenderer.material;
            _materialInstance.EnableKeyword("_EMISSION");
        }
    }

    private void Update()
    {
        if (_materialInstance == null) return;

        // Determine distance source:
        // sensorResponse (events) takes precedence when assigned; otherwise poll sensor.
        float desiredIntensity = minIntensity;
        bool hasDetection = false;
        float distance = maxDistance;

        if (sensorResponse != null)
        {
            hasDetection = _hasSignal;
            distance = _eventDistance;
        }
        else if (sensor != null && sensor.TryGetNearest(out var nearest))
        {
            hasDetection = true;
            distance = nearest.Distance;
        }

        if (hasDetection)
        {
            // Map distance->t where t=1 at minDistance (near) and 0 at maxDistance (far)
            float t = Mathf.InverseLerp(maxDistance, minDistance, distance);
            t = Mathf.Clamp01(t);
            float multiplier = distanceToMultiplier.Evaluate(t);
            desiredIntensity = Mathf.Lerp(minIntensity, maxIntensity, multiplier);
        }
        else
        {
            desiredIntensity = minIntensity;
        }

        // Smooth intensity if requested
        if (smoothTime > 0f)
            _currentIntensity = Mathf.SmoothDamp(_currentIntensity, desiredIntensity, ref _intensityVelocity, smoothTime);
        else
            _currentIntensity = desiredIntensity;

        Color final = baseEmissiveColor * _currentIntensity;
        _materialInstance.SetColor(emissionProperty, final);
    }

    private void HandleWhileDetected(Vector3 nearestPosition)
    {
        _eventDistance = Vector3.Distance(transform.position, nearestPosition);
        _hasSignal = true;
    }

    private void HandleAllLost()
    {
        _hasSignal = false;
        _eventDistance = float.MaxValue;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (_materialInstance)
            DestroyImmediate(_materialInstance);
#else
        if (_materialInstance)
            Destroy(_materialInstance);
#endif
    }
}
        