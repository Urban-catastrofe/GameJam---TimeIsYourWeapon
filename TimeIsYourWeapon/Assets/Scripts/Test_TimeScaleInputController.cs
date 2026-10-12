using System;
using UnityEngine;

public class Test_TimeScaleInputController : MonoBehaviour
{
    public TimeScaleManager timeScaleManager;

    public float[] timeScaleValues = {0.0f, .25f, 0.5f, 1.0f, 1.25f};
    private float currentValue = 1.0f;

    void OnAttack()
    {
        // get next value
        var currentIndex = Array.IndexOf(timeScaleValues, currentValue);
        var nextIndex = (currentIndex + 1) % timeScaleValues.Length;

        currentValue = timeScaleValues[nextIndex];
        
        timeScaleManager.ChangeTimescale(currentValue);
    }
}
