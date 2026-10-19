
using UnityEngine;
using Unity.MLAgents;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Hill Settings")]
    public float hillHeight = 8f;
    public float hillFrequency = 4f;
    public float valleyStrength = 0.5f;

    private TerrainData terrainData;
    private float[,] originalHeights;

    private void OnEnable()
    {
        if (Academy.IsInitialized)
            Academy.Instance.OnEnvironmentReset += GenerateTerrain;
    }

    private void Start()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        if (terrain == null)
        {
            Debug.LogError(
                "TerrainGenerator needs a Terrain reference."
            );
            return;
        }

        terrainData = terrain.terrainData;

        int resolution = terrainData.heightmapResolution;

        originalHeights = terrainData.GetHeights(
            0, 0, resolution, resolution
        );

        GenerateTerrain();
    }

    private void OnDisable()
    {
        if (Academy.IsInitialized)
            Academy.Instance.OnEnvironmentReset -= GenerateTerrain;
    }

    public void GenerateTerrain()
    {
        if (terrainData == null || originalHeights == null)
            return;

        int resolution = terrainData.heightmapResolution;

        float[,] heights = new float[resolution, resolution];

        float offsetX = Random.Range(0f, 10000f);
        float offsetZ = Random.Range(0f, 10000f);

        float heightScale = hillHeight / terrainData.size.y;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float sampleX =
                    (float)x / resolution * hillFrequency + offsetX;

                float sampleZ =
                    (float)z / resolution * hillFrequency + offsetZ;

                float largeHills = Mathf.PerlinNoise(
                    sampleX, sampleZ
                );

                float smallHills = Mathf.PerlinNoise(
                    sampleX * 2f, sampleZ * 2f
                );

                float combinedNoise =
                    largeHills * (1f - valleyStrength)
                    + smallHills * valleyStrength;

                float variation =
                    (combinedNoise - 0.5f) * 2f * heightScale;

                heights[z, x] = Mathf.Clamp01(
                    originalHeights[z, x] + variation
                );
            }
        }

        terrainData.SetHeights(0, 0, heights);

        Debug.Log("New smooth hills and valleys generated.");
    }
}
