       [RequireComponent(typeof(LineRenderer))]
    public class CircleVisualizer : MonoBehaviour
    {
        public int segments = 64;
        public float radius = 5f;
        public Color color = new Color(1, 1, 1, 0.5f);
        public float lineWidth = 0.1f;

        private LineRenderer lr;

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = false;      // draw in local space
            lr.loop = true;
            lr.positionCount = segments + 1;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            DrawCircle();
        }

        public void DrawCircle()
        {
            float angleStep = 2 * Mathf.PI / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * angleStep;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                lr.SetPosition(i, new Vector3(x, 0.01f, z));
            }
        }

        // if you need to change radius at runtime:
        public void UpdateRadius(float r)
        {
            radius = r;
            DrawCircle();
        }
    }
   
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Tooltip("Number of segments around the circle")]
    public int segments = 64;

    [Tooltip("Radius of the circle in world units")]
    public float radius = 5f;

    [Tooltip("Line width of the circle")]
    public float lineWidth = 0.1f;

    [Tooltip("Color (with alpha) of the circle")]
    public Color color = new Color(1, 1, 1, 0.5f);

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.loop          = true;
        lr.useWorldSpace = false;
        lr.positionCount = segments + 1;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth    = lr.endWidth = lineWidth;
        lr.startColor    = lr.endColor = color;

        DrawCircle();
    }

    public void DrawCircle()
    {
        float step = 2 * Mathf.PI / segments;
        for (int i = 0; i <= segments; i++)
        {
            float a = i * step;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.01f, Mathf.Sin(a) * radius));
        }
    }

    /// <summary>
    /// If you want to change radius/color at runtime, call UpdateProperties.
    /// </summary>
    public void UpdateProperties(float newRadius, Color newColor)
    {
        radius = newRadius;
        color  = newColor;
        lr.startColor = lr.endColor = color;
        DrawCircle();
    }
}