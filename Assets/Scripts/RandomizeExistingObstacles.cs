
using UnityEngine;

public class RandomizeExistingObstacles : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;

    [Header("Existing Obstacles")]
    public Transform[] rocks = new Transform[2];
    public Transform[] buildings = new Transform[5];

    [Header("Placement")]
    public float edgeMargin = 10f;
    public float minDistanceBetweenObstacles = 5f;
    public float safeDistanceFromMeetingPoint = 12f;
    public int maxAttempts = 50;

    private Vector3[] originalScales;

    // Random nya positioner för de existerande hindren.
    public void RandomizeObstacles()
    {
        if (terrain == null)
        {
            Debug.LogError("Assign the Terrain first.");
            return;
        }

        originalScales = new Vector3[rocks.Length + buildings.Length];

        for (int i = 0; i < rocks.Length; i++)
            if (rocks[i] != null)
                originalScales[i] = rocks[i].localScale;

        for (int i = 0; i < buildings.Length; i++)
            if (buildings[i] != null)
                originalScales[rocks.Length + i] = buildings[i].localScale;

        Transform meetingPoint = GameObject.Find("MeetingPoint")?.transform;

        Vector3[] placedPositions =
            new Vector3[rocks.Length + buildings.Length];

        int placedCount = 0;

        for (int i = 0; i < placedPositions.Length; i++)
        {
            Transform obstacle = i < rocks.Length
                ? rocks[i]
                : buildings[i - rocks.Length];

            if (obstacle == null)
            {
                Debug.LogWarning("An obstacle reference is missing.");
                continue;
            }

            bool placed = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 position = GetRandomTerrainPosition();

                if (meetingPoint != null &&
                    HorizontalDistance(position, meetingPoint.position)
                    < safeDistanceFromMeetingPoint)
                    continue;

                bool tooClose = false;

                for (int j = 0; j < placedCount; j++)
                {
                    if (HorizontalDistance(position, placedPositions[j])
                        < minDistanceBetweenObstacles)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                obstacle.position = position;
                placedPositions[placedCount] = position;
                placedCount++;
                placed = true;
                break;
            }

            if (!placed)
                Debug.LogWarning(
                    "Could not find a suitable position for " + obstacle.name
                );
        }

        Debug.Log("Randomized existing obstacles. No copies created.");
    }

    // Hämtar en random position på terrängen.
    private Vector3 GetRandomTerrainPosition()
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        float marginX = Mathf.Min(edgeMargin, size.x * 0.25f);
        float marginZ = Mathf.Min(edgeMargin, size.z * 0.25f);

        Vector3 position = new Vector3(
            Random.Range(origin.x + marginX, origin.x + size.x - marginX),
            0f,
            Random.Range(origin.z + marginZ, origin.z + size.z - marginZ)
        );

        position.y = terrain.SampleHeight(position) + origin.y;
        return position;
    }

    // Beräknar avståndet mellan två positioner
    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
