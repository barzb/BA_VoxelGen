using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimplexNoise;

/*** WorldChunk ***
   The world is a giant grid of these WorldChunks.
   When the player is in range, the Chunk is loaded
   and maybe with an island in it.
   
   The islands will be cachable in the future to make
   it more performant but that's not implemented yet!
*/
public sealed class WorldChunk
{
    // ATTRIBUTES
    private Vector3 position;
    private bool hasIsland;
    private bool loaded;
    ///private bool cached;
    private Island containingIsland;
    private WorldChunk[] neighbors;
    private Region zone;

    // CLASS ATTRIBUTES
    private static List<WorldChunk> loadedWorldChunks = new List<WorldChunk>();
    ///private static List<WorldChunk> cachedWorldChunks = new List<WorldChunk>();

    // PROPERTIES
    public static List<WorldChunk> LoadedWorldChunks { get { return loadedWorldChunks; } }
    ///public static List<WorldChunk> CachedWorldChunks { get { return cachedWorldChunks; } }
    public Vector3 Position { get { return position; } }
    public bool HasIsland { get { return hasIsland; } }
    public bool Loaded { get { return loaded; } set { loaded = value; } }
    ///public bool Cached { get { return cached; } set { cached = value; } }
    public Region Zone {  get { return zone; } }
    public Island ContainingIsland { get { return hasIsland ? containingIsland : null; } }

    // CONSTRUCTOR
    public WorldChunk(Vector3 _position)
    {
        // island can be anywhere in the worldChunk
        //NoiseOffset = World.GetRandomVec3();
        float noiseOffset = World.GetRandomFloat((_position.x + _position.y - position.z) * 192.183f, 0f, 10000f);

        // WorldChunks are like a grid, this returns the nearest grid pos
        position = WorldPosToChunkPos(_position);
        
        // noise offsets
        float nX = Mathf.Abs(position.x + noiseOffset);
        float nY = Mathf.Abs(position.y + noiseOffset);
        float nZ = Mathf.Abs(position.z + noiseOffset);
        // calculate noise value between -1 and +1
        float noise = Noise.Generate(nX, nY, nZ);

        // every y-layer in the grid has it's own zone
        zone = (Region) (Mathf.Floor(position.y / World.currentWorld.worldChunkSize.y));

        // Island = YES (45% chance)
        if (noise > 0.45f)
        {
            hasIsland = true;
            loaded = true;

            // position the island in the worldChunk
            Vector3 maxPositionOffset = new Vector3(
                World.currentWorld.maxIslandSize.y,   // x = minVal, y = maxVal
                World.currentWorld.chunkHeight * 2f,  // island height
                World.currentWorld.maxIslandSize.y    // x = minVal, y = maxVal
            );
            maxPositionOffset = (World.currentWorld.worldChunkSize - maxPositionOffset) / 2f;
            
            // calculate a pseudo-random position offset of the island in the worldChunk
            Vector3 positionOffset = new Vector3(
                World.GetRandomFloat(noise * (noise + position.z) * 907.1234f, 0f, maxPositionOffset.x), 
                World.GetRandomFloat(noise * (noise + position.z) * 482.5764f, 0f, maxPositionOffset.y), 
                World.GetRandomFloat(noise * (noise + position.x) * 159.9824f, 0f, maxPositionOffset.z)
            );

            Vector3 islandPos = positionOffset + position;

            // create island
            containingIsland = Island.CreateIsland(islandPos, zone);
            // add to islandList
            World.currentWorld.AddIsland(containingIsland);
        }
        // Island = NO
        else
        {
            hasIsland = false;
            loaded = false;
            containingIsland = null;
        }

        loadedWorldChunks.Add(this);

        /// - NOT YET IMPLEMENTED -
        ///cached = false;
    }

    
    // always load the adjacent WorldChunks if they are not already loaded
    public void LoadNeighbors()
    {
        // neighors not yet initialized
        if(neighbors == null) {
            neighbors = new WorldChunk[26];
        }

        float chunkSize = World.currentWorld.worldChunkSize.x;

        int count = 0;
        for(int i = -1; i < 2; i++)
        {
            for (int j = -1; j < 2; j++)
            {
                for (int k = -1; k < 2; k++)
                {
                    // self
                    if (i == 0 && j == 0 && k == 0) { 
                        continue;
                    }

                    // check if neighbor is set
                    if(neighbors[count] == null)
                    {
                        Vector3 neighborPos = new Vector3(
                            position.x + i * chunkSize, 
                            position.y + j * chunkSize, 
                            position.z + k * chunkSize
                        );

                        // check if WorldChunk was already created
                        neighbors[count] = FindChunk(neighborPos);
                        
                        // chunk not yet created -> create it
                        if (neighbors[count] == null
                          && (neighborPos.y <= ((int)Region.ICY * chunkSize))
                          && (neighborPos.y >= ((int)Region.LAVA * chunkSize))
                        ) {
                            neighbors[count] = new WorldChunk(neighborPos);
                        }
                    }
                    count++;
                }
            }
        }
    }

    // checks if something is inside the WorldChunk
    public bool IsObjectInChunk(Vector3 objPos)
    {
        return (WorldPosToChunkPos(objPos) == position);
    }

    // converts a position to a grid pos of a chunk
    public static Vector3 WorldPosToChunkPos(Vector3 worldPos)
    {
        Vector3 worldChunkSize = World.currentWorld.worldChunkSize;
        Vector3 chunkPos = worldPos + worldChunkSize / 2f;

        chunkPos.x = Mathf.Floor(chunkPos.x / worldChunkSize.x) * worldChunkSize.x;
        chunkPos.y = Mathf.Floor(chunkPos.y / worldChunkSize.y) * worldChunkSize.y;
        chunkPos.z = Mathf.Floor(chunkPos.z / worldChunkSize.z) * worldChunkSize.z;

        return chunkPos;
    }

    // return the chunk at position if it exists
    public static WorldChunk FindChunk(Vector3 pos)
    {
        Vector3 chunkPos = WorldPosToChunkPos(pos);

        foreach (WorldChunk chunk in loadedWorldChunks)
        {
            if (chunk.Position == chunkPos)
                return chunk;
        }

        /// - NOT YET IMPLEMENTED -
        ///foreach (WorldChunk chunk in cachedWorldChunks)
        ///{
        ///    if (chunk.Position == chunkPos)
        ///        return chunk;
        ///}

        return null;
    }
}
