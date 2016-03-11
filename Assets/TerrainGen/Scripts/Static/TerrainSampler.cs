using UnityEngine;
using System.Collections;
using SimplexNoise;

/*** Terrain Sampler ***
   provides methods for the terrain to 
   procedurally generate the voxels. 
*/
public static class TerrainSampler
{
    // ISLAND FORM
    public static float SampleIsland(Vector3 pos, Vector3 islandCenter, Vector3 islandSize)
    {
        float maxLength = islandSize.x;
        Vector3 localPos = pos - islandCenter;

        float length = Mathf.Min(Vector3.Magnitude(localPos), maxLength);

        return Mathf.Clamp01((maxLength / 2f - length) / maxLength);
    }

    // MOUNTAINS
    public static float SampleMountains(Vector3 pos, Vector3[] offsets, float heightScale = 1f)
    {
        float noiseX = Mathf.Abs((pos.x + offsets[0].x) * 0.007f);
        float noiseY = Mathf.Abs((pos.y + offsets[0].y) * 0.007f / heightScale);
        float noiseZ = Mathf.Abs((pos.z + offsets[0].z) * 0.007f);
        float value1 = (Noise.Generate(noiseX, noiseY, noiseZ) + 1f) / 2f;

        noiseX = Mathf.Abs((pos.x + offsets[1].x) * 0.0065f);
        noiseY = Mathf.Abs((pos.y + offsets[1].y) * 0.0065f / heightScale);
        noiseZ = Mathf.Abs((pos.z + offsets[1].z) * 0.0065f);
        float value2 = Noise.Generate(noiseX, noiseY, noiseZ);

        return Mathf.Clamp01(value1 * (value1 + value2) * 0.5f * heightScale);
    }

    // HILLS
    public static float SampleHills(Vector3 pos, Vector3[] offsets)
    {
        float noiseX = Mathf.Abs((pos.x + offsets[2].x) * 0.025f);
        float noiseY = Mathf.Abs((pos.y + offsets[2].y) * 0.025f);
        float noiseZ = Mathf.Abs((pos.z + offsets[2].z) * 0.025f);
        return Mathf.Max(0f, Noise.Generate(noiseX, noiseY, noiseZ)) * 10f;
    }

    // CAVES
    public static float SampleCaves(Vector3 pos, Vector3[] offsets)
    {
        float noiseX = Mathf.Abs((pos.x + offsets[3].x) * 0.02f);
        float noiseY = Mathf.Abs((pos.y + offsets[3].y) * 0.02f);
        float noiseZ = Mathf.Abs((pos.z + offsets[3].z) * 0.02f);
        float value1 = Mathf.Clamp01(Noise.Generate(noiseX, noiseY, noiseZ));

        noiseX = Mathf.Abs((pos.x + offsets[1].x) * 0.012f);
        noiseY = Mathf.Abs((pos.y + offsets[1].y) * 0.012f);
        noiseZ = Mathf.Abs((pos.z + offsets[1].z) * 0.012f);
        float value2 = Mathf.Clamp01(Noise.Generate(noiseX, noiseY, noiseZ));

        float value = (value1 > 0.6f ? value1 * 50f : value1 * 10f) + (value2 * 10f);
        return Mathf.Max(0f, value);
    }

    // VOLCANO
    public static float SampleVolcano(Vector3 pos, Vector3[] offsets, Vector3 islandCenter, Vector3 islandSize)
    {
        // width/length of the volcano chamber
        float maxLength = islandSize.y / 2f;
        // local position of the voxel
        Vector3 localPos = pos - islandCenter;
        localPos *= 1.5f;
        // stretch the volcano in height
        localPos.y *= 0.5f;
        float length = Vector3.SqrMagnitude(localPos);
        
        float noise = Mathf.Clamp01(Noise.Generate(Mathf.Abs(pos.x * 0.035f), Mathf.Abs(pos.y * 0.035f), Mathf.Abs(pos.z * 0.035f)));

        // square the maxlength because the length was squared, too (sqrMagnitude)
        maxLength *= maxLength;
        
        float value = Mathf.Max(0f, (maxLength - length) / maxLength);
        
        // combine value with noise
        return Mathf.Clamp01(value + (value * noise * 0.5f));
    }

    // CALDERA for a volcano
    public static float SampleCaldera(Vector3 pos, Vector3 islandCenter, Vector3 islandSize)
    {
        // size of the caldera
        float maxLength = islandSize.y / 2f;
        // set the center of the island to the upper center
        islandCenter.y += maxLength;
        // local position of the voxel
        Vector3 localPos = pos - islandCenter;
        // stretch the caldera in height
        localPos.y *= 0.26f;
        // calculate the distance of the voxel to the upper center of the island
        float length = Vector3.Magnitude(localPos);

        // generate a noise for the caldera
        float noise = Mathf.Clamp01(Noise.Generate(Mathf.Abs(pos.x * 0.07f), Mathf.Abs(pos.y * 0.07f), Mathf.Abs(pos.z * 0.07f)));
        
        // this creates a "blob" in the upper center of the island that will later be substracted from the volcano
        float value = Mathf.Max(0f, 10f * (maxLength / 3f - length) / maxLength);
        // add noise (but not outside the caldera region)
        return 1.2f * value + (value > 0f ? noise : 0f);
    }

    // SWAMP
    public static float SampleSwamp(Vector3 pos, Vector3[] offsets, Vector3 islandCenter, Vector3 islandSize)
    {
        float noiseX = Mathf.Abs((pos.x + offsets[3].x) * 0.05f);
        float noiseY = Mathf.Abs((pos.y + offsets[3].y) * 0.09f);
        float noiseZ = Mathf.Abs((pos.z + offsets[3].z) * 0.05f);

        float noise = Mathf.Clamp01(Noise.Generate(noiseX, noiseY, noiseZ));
        
        float y = Mathf.Max(1f, (pos.y - islandCenter.y)) * 5f;

        float value = Mathf.Clamp(y / islandSize.y, 0f, 1f);

        return value * noise * 1.2f;
    }
}