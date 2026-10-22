using UnityEngine;

public class Lamp : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light lampLight;

    [Header("Brightness steps — index 0 is OFF")]
    [SerializeField] private float[] intensitySteps = { 0f, 0.6f, 1.5f, 3f };
    [SerializeField] private int startStep = 1;

    private int currentStep;

    private void Awake()
    {
        currentStep = Mathf.Clamp(startStep, 0, intensitySteps.Length - 1);
        Apply();
    }

    public void Toggle()
    {
        currentStep = (currentStep == 0) ? 1 : 0;
        Apply();
    }

    public void CycleBrightness()
    {
        currentStep = (currentStep + 1) % intensitySteps.Length;
        Apply();
    }

    public void Increase() => SetStep(currentStep + 1);
    public void Decrease() => SetStep(currentStep - 1);

    private void SetStep(int step)
    {
        currentStep = Mathf.Clamp(step, 0, intensitySteps.Length - 1);
        Apply();
    }

    private void Apply()
    {
        if (lampLight == null) return;

        float intensity = intensitySteps[currentStep];
        lampLight.intensity = intensity;
        lampLight.enabled = intensity > 0.0001f; 
    }
}