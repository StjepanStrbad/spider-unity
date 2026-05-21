using UnityEngine;

public class DiscoLight : MonoBehaviour
{
    public float colorSpeed = 5f;
    public float quitIntensity = 5f;
    public float normalIntensity = 2f;

    private Light discoLight;
    private bool isQuitMode = false;

    void Start()
    {
        discoLight = GetComponent<Light>();
        discoLight.intensity = normalIntensity;
    }

    void Update()
    {
        if (isQuitMode)
        {
            discoLight.color = Color.red;
            discoLight.intensity = normalIntensity + Mathf.Sin(Time.time * 10f) * 2f;
        }
        else
        {
            float hue = Mathf.Repeat(Time.time * colorSpeed, 1f);
            discoLight.color = Color.HSVToRGB(hue, 1f, 1f);
            discoLight.intensity = normalIntensity;
        }
    }

    public void SetQuitMode(bool value)
    {
        isQuitMode = value;
    }
}