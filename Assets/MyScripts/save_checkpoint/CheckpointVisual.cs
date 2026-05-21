using UnityEngine;

public class CheckpointVisual : MonoBehaviour
{
    public Renderer checkpointRenderer;
    public Color inactiveColor = new Color(0.1f, 0.3f, 0.35f, 0.3f);
    public Color activeColor = new Color(0f, 0.78f, 1f, 0.6f);

    private void Start()
    {
        if (checkpointRenderer != null)
            checkpointRenderer.material.color = inactiveColor;
    }

    public void Activate()
    {
        if (checkpointRenderer != null)
            checkpointRenderer.material.color = activeColor;
    }

    public void Deactivate()
    {
        if (checkpointRenderer != null)
            checkpointRenderer.material.color = inactiveColor;
    }
}