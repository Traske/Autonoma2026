
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

    [Header("Obstacle Randomization")]
    public RandomizeExistingObstacles obstacleRandomizer;

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

    // Hämtar komponenter och förbereder agenten.
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

        MaxStep = 0;
    }

    // Startar en ny episod med random positioner.
    public override void OnEpisodeBegin()
    {
        if (roverRigidbody == null || carControl == null)
            return;

        roverRigidbody.linearVelocity = Vector3.zero;
        roverRigidbody.angularVelocity = Vector3.zero;

        carControl.StopAgentInput();
        carControl.ReleaseBrakes();

        if (obstacleRandomizer != null)
        {
            obstacleRandomizer.RandomizeObstacles();
        }

        RandomizeSpawnPositions();

        roverRigidbody.linearVelocity = Vector3.zero;
        roverRigidbody.angularVelocity = Vector3.zero;

        decisionCount = 0;
        stoppedDecisions = 0;

        isParking = false;
        hasParked = false;

        previousDistance = GetDistanceToTarget();

        Debug.Log(
            "New episode: rover, MeetingPoint, and existing obstacles randomized."
        );
    }

    // Hämtar terrängens höjd vid en viss position.
    private float GetTerrainHeight(Vector3 position)
    {
        return terrain.SampleHeight(position)
            + terrain.transform.position.y;
    }

    // Random position på terrängen.
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

    // Random startpositioner för rovern och målet.
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

        roverRigidbody.position = roverSpawn;

        roverRigidbody.rotation = Quaternion.Euler(
            0f,
            Random.Range(0f, 360f),
            0f
        );

        meetingPoint.position = targetSpawn;
    }

    // Beräknar avståndet mellan rovern och målet.
    private float GetDistanceToTarget()
    {
        Vector3 roverPosition = transform.position;
        Vector3 targetPosition = meetingPoint.position;

        roverPosition.y = 0f;
        targetPosition.y = 0f;

        return Vector3.Distance(roverPosition, targetPosition);
    }

    // Samlar information som agenten använder för beslut.
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

    // Styr rovern, hanterar belöningar och bromsar vid målet.
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (roverRigidbody == null || carControl == null)
            return;

        // Håller rovern stilla efter parkering.
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

        // Bromsar tills rovern står stilla.
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

        // Börjar parkera när målet nås.
        if (distance <= successDistance)
        {
            isParking = true;
            stoppedDecisions = 0;

            carControl.StopAgentInput();
            carControl.ApplyBrakes(brakeForce);

            Debug.Log("Target reached. Braking to a stop.");

            return;
        }

        // Minskar gasen nära målet.
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

    }

    // Gör det möjligt att styra rovern manuellt.
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
