using UnityEngine;

public class Lamp : MonoBehaviour
{
    [SerializeField] private Light lampLight;
    [SerializeField] private float maxIntensity = 10f;

    [Header("Intensidad (0 = apagada, 1 = máxima)")]
    [Range(0f, 1f)] [SerializeField] private float intensity01 = 0f;

    private float lastOn = 0f;

    public float Normalized => intensity01;

    private void Awake() => SetNormalized(intensity01);


    private void OnValidate() => SetNormalized(intensity01);

    public void SetNormalized(float t)
    {
        intensity01 = Mathf.Clamp01(t);
        if (intensity01 > 0.001f) lastOn = intensity01;
        if (lampLight == null) return;

        lampLight.intensity = intensity01 * maxIntensity;
        lampLight.enabled = intensity01 > 0.001f;
    }

    public void Toggle() => SetNormalized(intensity01 > 0.001f ? 0f : lastOn);

    [ContextMenu("Test Toggle")]
    private void TestToggle() => Toggle();
}
