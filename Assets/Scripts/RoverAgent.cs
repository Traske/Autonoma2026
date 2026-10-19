
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RoverAgent : Agent
{
    [Header("References")]
    public Transform meetingPoint;
    public Rigidbody roverRigidbody;
    public CarControl carControl;
    public Terrain terrain;

    [Header("Random Spawning")]
    public float edgeMargin = 20f;
    public float minTargetDistance = 30f;
    public int maxSpawnAttempts = 100;

    [Header("Navigation")]
    public int maxDecisions = 2000;
    public float successDistance = 3f;

    [Header("Braking")]
    public float brakingDistance = 12f;
    public float brakeStartDistance = 6f;
    public float brakeForce = 4500f;
    public float stopSpeed = 0.10f;
    public float stopRotationSpeed = 0.1f;

    private float roverHeightOffset;
    private float targetHeightOffset;

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

        if (roverRigidbody == null || carControl == null)
        {
            Debug.LogError(
                "RoverAgent needs a Rigidbody and CarControl."
            );
            return;
        }

        if (terrain != null && meetingPoint != null)
        {
            roverHeightOffset =
                transform.position.y -
                GetTerrainHeight(transform.position);

            targetHeightOffset =
                meetingPoint.position.y -
                GetTerrainHeight(meetingPoint.position);
        }

        MaxStep = maxDecisions;
    }

    public override void OnEpisodeBegin()
    {
        if (roverRigidbody == null || carControl == null)
            return;

        // Randomize the car and target at the start of each episode.
        RandomizeSpawnPositions();

        roverRigidbody.linearVelocity = Vector3.zero;
        roverRigidbody.angularVelocity = Vector3.zero;

        carControl.StopAgentInput();
        carControl.ReleaseBrakes();

        decisionCount = 0;
        stoppedDecisions = 0;

        isParking = false;
        hasParked = false;

        previousDistance = GetDistanceToTarget();

        Debug.Log(
            "New episode: random car and MeetingPoint positions."
        );
    }

    private float GetTerrainHeight(Vector3 position)
    {
        return terrain.SampleHeight(position)
            + terrain.transform.position.y;
    }

    private Vector3 GetRandomTerrainPosition(float heightOffset)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        float marginX = Mathf.Min(edgeMargin, size.x * 0.25f);
        float marginZ = Mathf.Min(edgeMargin, size.z * 0.25f);

        float x = Random.Range(
            origin.x + marginX,
            origin.x + size.x - marginX
        );

        float z = Random.Range(
            origin.z + marginZ,
            origin.z + size.z - marginZ
        );

        Vector3 position = new Vector3(x, 0f, z);
        position.y = GetTerrainHeight(position) + heightOffset;

        return position;
    }

    private void RandomizeSpawnPositions()
    {
        if (terrain == null || meetingPoint == null)
        {
            Debug.LogWarning(
                "Assign Terrain and MeetingPoint in RoverAgent."
            );
            return;
        }

        Vector3 roverSpawn = transform.position;
        Vector3 targetSpawn = meetingPoint.position;

        bool foundPositions = false;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            roverSpawn =
                GetRandomTerrainPosition(roverHeightOffset);

            targetSpawn =
                GetRandomTerrainPosition(targetHeightOffset);

            Vector2 roverXZ =
                new Vector2(roverSpawn.x, roverSpawn.z);

            Vector2 targetXZ =
                new Vector2(targetSpawn.x, targetSpawn.z);

            if (Vector2.Distance(roverXZ, targetXZ)
                >= minTargetDistance)
            {
                foundPositions = true;
                break;
            }
        }

        if (!foundPositions)
        {
            Debug.LogWarning(
                "Could not find positions far enough apart. " +
                "Check Terrain size or reduce Min Target Distance."
            );
        }

        // Random car position and direction.
        roverRigidbody.position = roverSpawn;

        roverRigidbody.rotation = Quaternion.Euler(
            0f,
            Random.Range(0f, 360f),
            0f
        );

        // Random MeetingPoint position.
        meetingPoint.position = targetSpawn;
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
            transform.InverseTransformDirection(
                direction.normalized
            );

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
        if (roverRigidbody == null || carControl == null)
            return;

        // Keep the rover parked after success.
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

        // Brake until the rover is stationary.
        if (isParking)
        {
            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            float speed =
                roverRigidbody.linearVelocity.magnitude;

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

                Debug.Log("SUCCESS! Rover parked.");
            }

            return;
        }

        float steering = Mathf.Clamp(
            actions.ContinuousActions[0],
            -1f,
            1f
        );

        float throttle = Mathf.Clamp(
            actions.ContinuousActions[1],
            0f,
            1f
        );

        // Start parking when the target is reached.
        if (distance <= successDistance)
        {
            isParking = true;
            stoppedDecisions = 0;

            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            Debug.Log("Target reached. Braking to a stop.");

            return;
        }

        // Reduce throttle gradually when approaching.
        if (distance <= brakingDistance)
        {
            float range = Mathf.Max(
                0.01f,
                brakingDistance - successDistance
            );

            float throttleScale = Mathf.Clamp01(
                (distance - successDistance) / range
            );

            throttle *= throttleScale * throttleScale;
        }

        // Apply brakes only in the closer braking zone.
        float speedNow =
            roverRigidbody.linearVelocity.magnitude;

        if (distance <= brakeStartDistance &&
            speedNow > stopSpeed)
        {
            carControl.ApplyBrakes(brakeForce);
        }
        else
        {
            carControl.ReleaseBrakes();
        }

        carControl.SetAgentInput(steering, throttle);

        float progress = previousDistance - distance;

        AddReward(
            Mathf.Clamp(progress * 0.1f, -0.1f, 0.1f)
        );

        AddReward(-0.002f);

        previousDistance = distance;

        // Failed episodes reset automatically.
        if (decisionCount >= maxDecisions)
        {
            Debug.Log("FAILED: Decision limit reached.");

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
            0f,
            Input.GetAxis("Vertical")
        );
    }
}
