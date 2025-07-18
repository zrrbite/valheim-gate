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