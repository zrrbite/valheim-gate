// Assets/Scripts/CircleVisualizer.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    public int segments = 64;
    public float radius = 5f;
    public float lineWidth = 0.2f;
    public Color color = new Color(1, 1, 1, 0.5f);

    private LineRenderer lr;

    void Awake()
    {
        Debug.Log("[CircleViz] Awake on " + gameObject.name);
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;            // positions are in world coords
        lr.loop = true;
        lr.positionCount = segments + 1;
        // a simple unlit color shader
        lr.material = new Material(Shader.Find("Unlit/Color"));
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = lineWidth;

        DrawCircle();
        ApplyToRenderer();
    }

    public void DrawCircle()
    {
        if (lr == null) return;
        Debug.Log($"[CircleViz] Drawing circle @ {transform.position}  r={radius}");
        float step = 2 * Mathf.PI / segments;
        for (int i = 0; i <= segments; i++)
        {
            float a = i * step;
            Vector3 offset = new Vector3(
                Mathf.Cos(a) * radius,
                0f,
                Mathf.Sin(a) * radius
            );
            // because useWorldSpace=false, this is local to the GameObject
            lr.SetPosition(i, offset);
        }
    }

    // Allow changing at runtime
    public void UpdateProperties(float newRadius, Color newColor)
    {
        radius = newRadius;
        color = newColor;
        lr.startColor = lr.endColor = color;
        DrawCircle();
        ApplyToRenderer();
    }
    private void ApplyToRenderer()
    {
        lr.startWidth = lr.endWidth = lineWidth;
        lr.startColor = lr.endColor = color;
    }
}

[RequireComponent(typeof(LineRenderer))]
public class GroundConformingRing : MonoBehaviour
{
    [Tooltip("How many points around the circle (higher = smoother)")]
    public int segments = 128;
    [Tooltip("Horizontal radius of the ring")]
    public float radius = 5f;
    [Tooltip("Thickness of the line")]
    public float lineWidth = 0.02f;
    [Tooltip("Base color (RGB) and initial alpha")]
    public Color baseColor = new Color(0, 1, 0, .3f);
    [Tooltip("How fast the alpha pulses (cycles/sec)")]
    public float pulseSpeed = 1f;
    [Tooltip("Min/max alpha for the pulse")]
    public float minAlpha = 0.2f;
    public float maxAlpha = 0.6f;

    private LineRenderer _lr;
    private Transform _target;

    /// <summary>
    /// Call once after AddComponent to tell it who to follow.
    /// </summary>
    public void Init(Transform followTarget)
    {
        _target = followTarget;
    }

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.loop = true;
        _lr.useWorldSpace = true;              // world coords
        _lr.positionCount = segments + 1;
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.startWidth = _lr.endWidth = lineWidth;
    }

    void Update()
    {
        if (_target == null) return;

        // 1) pulse alpha
        float t = (Mathf.Sin(Time.time * pulseSpeed * 2 * Mathf.PI) + 1f) * 0.5f;
        float a = Mathf.Lerp(minAlpha, maxAlpha, t);
        Color c = new Color(baseColor.r, baseColor.g, baseColor.b, a);
        _lr.startColor = _lr.endColor = c;

        // 2) sample ground under each segment
        Vector3 center = _target.position;
        for (int i = 0; i <= segments; i++)
        {
            float ang = 2 * Mathf.PI * i / segments;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));

            // cast 10m above and down to find ground
            Vector3 origin = center + dir * radius + Vector3.up * 10f;
            Vector3 pos = origin;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f))
                pos = hit.point + Vector3.up * 0.02f;

            _lr.SetPosition(i, pos);
        }
    }
}