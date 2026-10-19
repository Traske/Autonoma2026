
using UnityEngine;

public class CarControl : MonoBehaviour
{
    [Header("Driving")]
    public float enginePower = 2000f;
    public float turnSpeed = 25f;
    public float turnSmoothness = 5f;

    [Header("Vehicle References")]
    public Transform[] wheels;
    public Transform[] wheelMeshes;
    public Transform centerOfMass;
    public GameObject steeringWheel;

    [Header("Agent Control")]
    public bool useAgentInput = false;

    private float agentSteering;
    private float agentThrottle;

    private Rigidbody rb;
    private float currentTurnAngle;

    public void SetAgentInput(float steering, float throttle)
    {
        agentSteering = Mathf.Clamp(steering, -1f, 1f);
        agentThrottle = Mathf.Clamp(throttle, -1f, 1f);
    }

    public void StopAgentInput()
    {
        agentSteering = 0f;
        agentThrottle = 0f;
    }

    public void ApplyBrakes(float brakeForce)
    {
        if (wheels == null)
            return;

        foreach (Transform wheelTransform in wheels)
        {
            if (wheelTransform == null)
                continue;

            WheelCollider wheel =
                wheelTransform.GetComponent<WheelCollider>();

            if (wheel == null)
                continue;

            wheel.motorTorque = 0f;
            wheel.brakeTorque = Mathf.Max(0f, brakeForce);
        }
    }

    public void ReleaseBrakes()
    {
        if (wheels == null)
            return;

        foreach (Transform wheelTransform in wheels)
        {
            if (wheelTransform == null)
                continue;

            WheelCollider wheel =
                wheelTransform.GetComponent<WheelCollider>();

            if (wheel != null)
                wheel.brakeTorque = 0f;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null && centerOfMass != null)
            rb.centerOfMass = centerOfMass.localPosition;
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        float steering = 0f;
        float throttle = 0f;

        // Use either AI input or manual keyboard input.
        if (useAgentInput)
        {
            steering = agentSteering;
            throttle = agentThrottle;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow))
                steering = -1f;
            else if (Input.GetKey(KeyCode.RightArrow))
                steering = 1f;

            if (Input.GetKey(KeyCode.UpArrow))
                throttle = 1f;
            else if (Input.GetKey(KeyCode.DownArrow))
                throttle = -1f;
        }

        float targetAngle = steering * turnSpeed;

        currentTurnAngle = Mathf.Lerp(
            currentTurnAngle,
            targetAngle,
            Time.fixedDeltaTime * turnSmoothness
        );

        if (steeringWheel != null)
        {
            steeringWheel.transform.localEulerAngles =
                new Vector3(
                    -64f,
                    0f,
                    currentTurnAngle * 3f
                );
        }

        if (wheels == null)
            return;

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null)
                continue;

            WheelCollider wheel =
                wheels[i].GetComponent<WheelCollider>();

            if (wheel == null)
                continue;

            // Front wheels steer.
            wheel.steerAngle =
                i < 2 ? currentTurnAngle : 0f;

            // Braking overrides motor torque.
            if (wheel.brakeTorque > 0f)
                wheel.motorTorque = 0f;
            else
                wheel.motorTorque = throttle * enginePower;

            // Update the visible wheel mesh.
            if (wheelMeshes != null &&
                i < wheelMeshes.Length &&
                wheelMeshes[i] != null)
            {
                wheel.GetWorldPose(
                    out Vector3 position,
                    out Quaternion rotation
                );

                wheelMeshes[i].position = position;
                wheelMeshes[i].rotation = rotation;
            }
        }
    }
}
