
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RoverAgent : Agent
{
    public Transform meetingPoint;
    public Rigidbody roverRigidbody;
    public CarControl carControl;

    public int maxDecisions = 2000;
    public float successDistance = 3f;

    public float brakingDistance = 8f;
    public float brakeForce = 3000f;
    public float stopSpeed = 0.15f;
    public float stopRotationSpeed = 0.1f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistance;
    private int decisionCount;
    private int stoppedDecisions;

    private bool isParking;
    private bool hasParked;

    public override void Initialize()
    {
        if (roverRigidbody == null)
            roverRigidbody = GetComponent<Rigidbody>();

        if (carControl == null)
            carControl = GetComponent<CarControl>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        MaxStep = maxDecisions;
    }

    public override void OnEpisodeBegin()
    {
        // Reset only when a new episode actually begins.
        roverRigidbody.position = startPosition;
        roverRigidbody.rotation = startRotation;
        roverRigidbody.linearVelocity = Vector3.zero;
        roverRigidbody.angularVelocity = Vector3.zero;

        carControl.StopAgentInput();
        carControl.ReleaseBrakes();

        decisionCount = 0;
        stoppedDecisions = 0;
        isParking = false;
        hasParked = false;

        previousDistance = GetDistanceToTarget();

        Debug.Log("New episode started.");
    }

    private float GetDistanceToTarget()
    {
        Vector3 roverPosition = transform.position;
        Vector3 targetPosition = meetingPoint.position;

        roverPosition.y = 0f;
        targetPosition.y = 0f;

        return Vector3.Distance(roverPosition, targetPosition);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 direction =
            meetingPoint.position - transform.position;

        direction.y = 0f;

        Vector3 localDirection =
            transform.InverseTransformDirection(direction.normalized);

        Vector3 localVelocity =
            transform.InverseTransformDirection(
                roverRigidbody.linearVelocity
            );

        sensor.AddObservation(localDirection.x);
        sensor.AddObservation(localDirection.z);
        sensor.AddObservation(GetDistanceToTarget() / 150f);
        sensor.AddObservation(localVelocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Keep the rover parked after successful arrival.
        if (hasParked)
        {
            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            roverRigidbody.linearVelocity = Vector3.zero;
            roverRigidbody.angularVelocity = Vector3.zero;

            return;
        }

        decisionCount++;

        float distance = GetDistanceToTarget();

        // Brake and confirm that the rover has fully stopped.
        if (isParking)
        {
            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            float speed = roverRigidbody.linearVelocity.magnitude;
            float rotationSpeed =
                roverRigidbody.angularVelocity.magnitude;

            if (speed <= stopSpeed &&
                rotationSpeed <= stopRotationSpeed)
            {
                stoppedDecisions++;
            }
            else
            {
                stoppedDecisions = 0;
            }

            if (stoppedDecisions >= 5)
            {
                AddReward(2f);

                hasParked = true;

                carControl.StopAgentInput();
                carControl.ApplyBrakes(brakeForce);

                roverRigidbody.linearVelocity = Vector3.zero;
                roverRigidbody.angularVelocity = Vector3.zero;

                Debug.Log(
                    "SUCCESS! Rover parked inside MeetingPoint. " +
                    "Waiting for the next episode."
                );
            }

            return;
        }

        // Read the trained model's driving actions.
        float steering = Mathf.Clamp(
            actions.ContinuousActions[0], -1f, 1f
        );

        float throttle = Mathf.Clamp(
            actions.ContinuousActions[1], 0f, 1f
        );

        // Begin braking when the rover enters the target area.
        if (distance <= successDistance)
        {
            isParking = true;
            stoppedDecisions = 0;

            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            Debug.Log("MeetingPoint reached. Braking to a stop.");

            return;
        }

        // Reduce throttle while approaching the MeetingPoint.
        if (distance <= brakingDistance)
        {
            float range = Mathf.Max(
                0.01f, brakingDistance - successDistance
            );

            float throttleScale = Mathf.Clamp01(
                (distance - successDistance) / range
            );

            throttle *= throttleScale;
            carControl.ApplyBrakes(brakeForce);
        }
        else
        {
            carControl.ReleaseBrakes();
        }

        carControl.SetAgentInput(steering, throttle);

        // Reward progress toward the target.
        float progress = previousDistance - distance;

        AddReward(Mathf.Clamp(progress * 0.1f, -0.1f, 0.1f));
        AddReward(-0.002f);

        previousDistance = distance;

        // End unsuccessful episodes only.
        if (decisionCount >= maxDecisions)
        {
            Debug.Log(
                $"FAILED: Decision limit reached. " +
                $"Distance remaining: {distance:F2} m"
            );

            carControl.StopAgentInput();
            carControl.ReleaseBrakes();

            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;

        actions[0] = Input.GetAxis("Horizontal");
        actions[1] = Mathf.Max(
            0f, Input.GetAxis("Vertical")
        );
    }
}
