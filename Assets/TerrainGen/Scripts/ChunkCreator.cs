using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimplexNoise;

/*** Chunk Creator ***
   This class derives the abstract IThreadedJob class and now
   qualifies to be executed in working threads.
   How to use a job class:
   1) set input data, in this case just use the constructor
   2) add the job to the ThreadManager joblist
   3) the ThreadFunction() will be executed eventually
   4) check for the isDone property from the main thread and 
      access the result data if the job is finished.
   NOTE: if the result data is accessed before the job is
   finished, something could break!
*/
public class ChunkCreator : IThreadedJob
{
    // INPUT DATA
    private Chunk chunk;

    // OUTPUT DATA
    public MeshData MeshData { get { return meshData; } }

    // priority of the job is the distance of the chunk to the player
    public override float Priority {
        get {
            return Mathf.Min(100000f, Vector3.Distance(
                World.currentWorld.PlayerTransform.position,
                chunk.transform.position));
        }
    }

    private float[,,] map;       // voxel array
    private Vector3[,,] normals; // normal array
	private GridCell[,,] cells;  // grid cells for MarchingCubes
    private MeshData meshData;   // will be applied to mesh later
    private bool containsBlocks; // determines if the chunk is empty or not
    public bool ContainsBlocks { get { return containsBlocks; } }


    // CONSTRUCTOR
    public ChunkCreator(Chunk _chunk)
    {
        chunk = _chunk;
        containsBlocks = false;
    }
    
    // -- METHODS --

    // Do the threaded task. DON'T use the Unity API here
    protected override void ThreadFunction()
    {
        if(chunk == null)
        {
            Debug.LogError("Chunk Reference not set in ChunkCreator");
            return;
        }
         
        // calculate the voxel array       
        CalculateMap();
		        
        // check if chunk is empty or not
        if (containsBlocks)
        {
            // level the voxels with sourrounding ones to smooth the terrain
            // and calculate the normal array
            SmoothVoxelsAndCalculateNormals();

            // convert the voxel and normal array to the GridCell structure (see STRUCTS.cs)
            CreateGridCells();

            // create lists for the geometry data
            List<Vector3> vertexList = new List<Vector3>();
            List<Vector3> normalList = new List<Vector3>();
            List<int>     indices    = new List<int>();
            List<Triangle> tris = new List<Triangle>();

            // polygonize every GridCell (2x2x2 grid of voxel + normal data)
            int vCount = 0;     // vertex counter
            foreach (GridCell c in cells)
            {
                // let the MarchingCubes algorithm create a surface from the
                // GridCell -> convert a GridCell to a triangleList
                MarchingCubes.Polygonise(c, World.currentWorld.isoLevel, tris);
            }

            // exctract the vertices from the triangleList and calculate indices
            foreach (Triangle t in tris)
            {
                for (int i = 0; i < 3; i++)
                {
                    vertexList.Add(t.p[i]);
                    normalList.Add(t.n[i]);
                    indices.Add(vCount++);
                }
            }

            // set the meshData (result value) or mark as empty if there was no geometry generated
            if (tris.Count == 0) {
                containsBlocks = false;
            } else {
                meshData = new MeshData(vertexList.ToArray(), normalList.ToArray(), indices.ToArray());
            }
        }
    }

    // calculate the voxels in the voxel array
    private void CalculateMap()
    {
        // get chunk bounds
        int width  = Chunk.Width;
        int height = Chunk.Height;
        // create array, take width+1 and height+1 
        // (i.e. |0|1|2|3| <- we need a voxel at each | so it is +1)
        map = new float[width + 1, height + 1, width + 1];
        
        // create the voxels
        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z <= width; z++)
                {
                    // calculate position
                    Vector3 pos = new Vector3(x, y, z);
                    // object space to world space
                    pos += chunk.Position;

                    // calculate voxel
                    map[x, y, z] = CalculateVoxel(pos, chunk.IsUpperChunk);

                    // if the voxel isn't "empty", the whole chunk isn't
                    if (!containsBlocks && map[x, y, z] != 0) { 
                        containsBlocks = true;
                    }
                }
            }
        }
    }
    
    // This averages a voxel with all its neighbours (like a gaussian blur)
    // and calculate the normals. It's done in one method because we have to  
    // extend the voxel array for both, so we don't need to do it twice.
    private void SmoothVoxelsAndCalculateNormals()
    {
        // bounds
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int l = map.GetLength(2);

        // offsets to find the neighbour voxel
        int[,] offset = new int[,]
        {
            {1,-1,0}, {1,-1,1}, {0,-1,1}, {-1,-1,1}, {-1,-1,0}, {-1,-1,-1}, {0,-1,-1}, {1,-1,-1}, {0,-1,0},
            {1, 0,0}, {1, 0,1}, {0,0, 1}, {-1,0, 1}, {-1,0, 0}, {-1,0, -1}, {0,0, -1}, {1,0, -1}, {0,0, 0},
            {1, 1,0}, {1, 1,1}, {0,1, 1}, {-1,1, 1}, {-1,1, 0}, {-1,1, -1}, {0,1, -1}, {1,1, -1}, {0,1, 0}
        };

        // extended voxel map (+1 to each side in each direction)
        float[,,] smoothedVoxels = new float[w+2, h+2, l+2];

        for (int x = -1; x < w+1 ; x++)
        {
            for (int y = -1; y < h+1 ; y++)
            {
                for (int z = -1; z < l+1 ; z++)
                {
                    float value = 0f;
                    // add all 27 values of the area around the voxel
                    for (int i = 0; i < 27; i++) {
                        value += GetValue(x + offset[i, 0], y + offset[i, 1], z + offset[i, 2]);
                    }
                    // divide by 27 to get the average 
                    smoothedVoxels[x + 1, y + 1, z + 1] = (value / 27f);
                }
            }
        }

        // create the normal array
        normals = new Vector3[w, h, l];

        // rewrite the smoothed voxels to the original voxel array 
        // and calculate normals
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int z = 0; z < l; z++)
                {
                    // --- Calculate Normals ---
                    float dx = smoothedVoxels[x + 2, y + 1, z + 1] - smoothedVoxels[x + 0, y + 1, z + 1];
                    float dy = smoothedVoxels[x + 1, y + 2, z + 1] - smoothedVoxels[x + 1, y + 0, z + 1];
                    float dz = smoothedVoxels[x + 1, y + 1, z + 2] - smoothedVoxels[x + 1, y + 1, z + 0];

                    // normalize and invert the normals (or else they point in the wrong direction)
                    normals[x, y, z] = (new Vector3(dx, dy, dz)).normalized * -1;

                    // --- apply the smoothed voxels ---
                    map[x,y,z] = smoothedVoxels[x+1,y+1,z+1];
                }
            }
        }
    }
    
    // convert the voxel and normal array to the GridCell format
    private void CreateGridCells()
	{
        // create GridCell array (1 cell uses 2 voxelss in each direction -> length-1)
		cells = new GridCell[map.GetLength(0)-1, map.GetLength(1)-1, map.GetLength(2)-1];

        // offsets for the voxels for one GridCell
        int[,] vertexOffset = new int[,]
        {
            {0,0,0}, {0,0,1}, {1,0,1}, {1,0,0},
            {0,1,0}, {0,1,1}, {1,1,1}, {1,1,0}
        };

        // convert to GridCells
        for (int x = 0; x < cells.GetLength(0); x++)
        {
            for (int y = 0; y < cells.GetLength(1); y++)
            {
                for (int z = 0; z < cells.GetLength(2); z++)
                {
                    // each GridCell has 8 voxel -> 8 positions, 8 normals, 8 values
                    Vector3[] voxPos = new Vector3[8];
                    Vector3[] voxNormal = new Vector3[8];
                    float[] voxVal = new float[8];

                    for (int i = 0; i < 8; i++)
                    {
                        voxVal[i] = Mathf.Max(0, map[x + vertexOffset[i, 0], y + vertexOffset[i, 1], z + vertexOffset[i, 2]]);
                        voxPos[i] = new Vector3(x + vertexOffset[i, 0], y + vertexOffset[i, 1], z + vertexOffset[i, 2]);
                        voxNormal[i] = normals[x + vertexOffset[i, 0], y + vertexOffset[i, 1], z + vertexOffset[i, 2]];
                    }
                    // create GridCell
                    cells[x, y, z] = new GridCell(voxPos, voxNormal, voxVal);
                }
            }
        }
    }
        
    // returns the value of the voxel at a position
    // if the voxel is out of bounds -> calculate it!
    private float GetValue(int x, int y, int z)
    {
        //  bounds
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int l = map.GetLength(2);

        // get chunk position
        Vector3 cp = chunk.Position;

        // try to find the value in the existing voxel array or calculate it
        float val;
        if (x < 0 || x >= w || z < 0 || z >= l || y < 0 || y >= h)
        {
            bool upperChunk = (y < 0 ? false : (y >= h ? true : chunk.IsUpperChunk));
            val = CalculateVoxel(new Vector3(cp.x + x, cp.y + y, cp.z + z), upperChunk);
        } else {
            val = map[x, y, z];
        }

        return val;
    }

    // sample the terrain and calculate the voxel value
    // pos = global position of voxel in world
    private float CalculateVoxel(Vector3 pos, bool isUpperChunk)
    {
        Island island = chunk.Island;
        // get the type of the terrain
        // some terrain attributes are different, depending on terrain type
        Terrain terrain = island.IslandType;

        // height of the island (from center)
        float height = island.IslandSize.y/2f;

        // local position of the voxel in island coordinates
        float localYPos = (pos.y - island.IslandCenter.y);


        // Island Sampler
        float islandValue = TerrainSampler.SampleIsland(pos, island.IslandCenter, island.IslandSize);
        // save some calculation time if this value get's zero because the voxel will be air anyway
        if (islandValue <= 0f) return 0f;
        
        // this is the return value; density of the voxel
        float value = 0f;
        // lifts the terrain
        float groundHeight  = terrain.GroundHeight;
        // determines how high the mountains will be
        float mountainScale = terrain.Height;
        
        // -- VOLCANO --
        if (terrain == Terrain.VOLCANO)
        {
            // sample volcano
            float volcanoValue = TerrainSampler.SampleVolcano(
                pos, island.NoiseOffset, 
                island.IslandCenter, 
                island.IslandSize
             );

            // add volcano form
            value += volcanoValue;
        }

        // mountain sampler
        float mountainValue = TerrainSampler.SampleMountains(pos, island.NoiseOffset, mountainScale);
        
        // add mountains
        value = Mathf.Clamp01(mountainValue + value + groundHeight);

        // stretch volume to island height
        value = Mathf.Clamp(value * height * mountainScale * 2f, 0, height * mountainScale * 2f);
        
        // add hill value, but only if there will likely be other terrain
        // -> don't create hills in the air
        if (value > 0.1f)
        {
            // Hill Sampler
            float hillValue = TerrainSampler.SampleHills(pos, island.NoiseOffset);
            // add hills
            value += value * hillValue / (height * 0.2f);
        }

        // create caldera for volcanos
        if (terrain == Terrain.VOLCANO)
        {
            // caldera sampler
            float calderaValue = TerrainSampler.SampleCaldera(
                pos, 
                island.IslandCenter, 
                island.IslandSize
            );

            // substract caldera; scale with height because value is scaled too
            value -= (calderaValue * height * mountainScale);
        }


        // this gives the island the typical island form
        value *= islandValue;
        

        // --- TERRAIN WITH CAVES ---
        if (terrain.HasCaves)
        {
            // cave sampler
            float caveValue = TerrainSampler.SampleCaves(pos, island.NoiseOffset);
            // cut out the caves
            value -= caveValue;
        }

        // --- SWAMP ---
        if (terrain == Terrain.SWAMP) {
            // swamp sampler
            float swampValue = TerrainSampler.SampleSwamp(
                pos, 
                island.NoiseOffset, 
                island.IslandCenter, 
                island.IslandSize
            );

            // subtract swamp sampler
            value -= (swampValue * height);
        } 

        // be sure the terrain doesn't try to be higher than max Height
        value = Mathf.Clamp(value, 0f, height*0.95f);

        // creates less terrain, the higher we go
        // if we are on the upper island half
        if (isUpperChunk)
        {
            if (value > localYPos) { 
                return value;
            }
        }
        // creates less terrain, the lower we go
        // if we are on the lower island half
        else
        {
            if (-value < localYPos) { 
                return value;
            }
        }

        // no terrain -> air
        return 0f;
    }
}

