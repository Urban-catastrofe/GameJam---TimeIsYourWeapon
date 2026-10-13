using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Test_TimeScaleInputController : MonoBehaviour
{
    public TimeManager timeManager;

    public float[] timeScaleValues = {0.0f, .25f, 0.5f, 1.0f, 1.25f};
    private float currentValue = 1.0f;

    void OnAttack()
    {
        // get next value
        var currentIndex = Array.IndexOf(timeScaleValues, currentValue);
        var nextIndex = (currentIndex + 1) % timeScaleValues.Length;

        currentValue = timeScaleValues[nextIndex];
        
        timeManager.ChangeTimeScale(currentValue);
    }

    void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();

        if (input.y > 0.5f)
        {
            timeManager.AddSeconds(UnityEngine.Random.Range(1, 6));
        }
        else if (input.y < -0.5f)
        {
            timeManager.RemoveSeconds(UnityEngine.Random.Range(1, 6));
        }
    }
}
