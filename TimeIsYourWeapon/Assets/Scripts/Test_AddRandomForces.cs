using UnityEngine;
using System.Collections;

public class Test_AddRandomForces : MonoBehaviour
{
    public float forceStrength = 10f;
    public float yForceStrength = 2f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        StartCoroutine(ApplyRandomForces());
    }

    IEnumerator ApplyRandomForces()
    {
        yield return new WaitForSeconds(5.0f);

        Vector3 randomForce = new Vector3(
            Random.Range(-forceStrength, forceStrength),
            Random.Range(-yForceStrength, yForceStrength),
            Random.Range(-forceStrength, forceStrength)
        );

        rb.AddForce(randomForce, ForceMode.Impulse);
    }
}