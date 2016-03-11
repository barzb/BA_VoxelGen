using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*** Island class ***
   This script sits on the island object and has all the chunks.
   The island type is calculated with the region, the island is in.
*/
public sealed class Island : MonoBehaviour
{
    // ATTRIBUTES (show in inspector)
    [SerializeField] private Region region;
    [SerializeField] private Terrain islandType;
    [SerializeField] private Vector3 islandSize;
    [SerializeField] private Vector3 islandCenter;
    [SerializeField] private Vector3[] noiseOffset;
    [SerializeField] private bool isDone;

    // chunks
    private List<Chunk> chunks;
    private int  numChunks;
    private int  numChunksFinished;
    private bool allChunksCreated;

    // REFERENCES
    private ParticleSystem clouds;
    private ParticleSystem smoke;

    // PROPERTIES
    public Region  Zone          { get { return region; } }
    public Terrain IslandType    { get { return islandType; } }
    public Vector3 IslandSize    { get { return islandSize; } }
    public Vector3 IslandCenter  { get { return islandCenter; } }
    public Vector3[] NoiseOffset { get { return noiseOffset; } }
    public int NumChunks         { get { return numChunks; } }
    public int NumChunksFinished { get { return numChunksFinished; } }
    public bool AllChunksCreated { get { return allChunksCreated; } }

    //METHODS
    // creates an island and returns it
    public static Island CreateIsland(Vector3 pos, Region layer, Terrain type = null)
    {
        // if no type is set (i.e. custom island creation for debugging), choose one
        if (type == null) {
            // create a pseudo-random number
            int randomNumber = World.GetRandomInt(Mathf.RoundToInt(pos.x + pos.y - pos.z), 0, 1000);
            type = Terrain.ChooseTerrain(layer, randomNumber);
        }
        // create island gameobject in hierarchy (instantiate)
        GameObject wrapper = new GameObject("" + type.Label + ": " + (int)pos.x + "/" + (int)pos.y + "/" + (int)pos.z);
        // add island component
        Island island = wrapper.AddComponent<Island>();
        // initialize island
        island.Initialize(pos, layer, type);
        return island;
    }

    // island is instantiated (-> no constructor), so this initializes the island
    private void Initialize(Vector3 pos, Region _layer, Terrain _islandType)
    {
        // noise offsets for terrain sampling
        noiseOffset = new Vector3[4];
        for (int i = 0; i < noiseOffset.Length; i++) {
            // calculate a pseudo-random offset value
            noiseOffset[i] = new Vector3(
                World.GetRandomFloat(i * 20.193f + pos.z * 97.105471f, 0f, 10000f), 
                World.GetRandomFloat(i * 27.942f + pos.x * 124.01846f, 0f, 10000f),
                World.GetRandomFloat(i * 13.581f + pos.x * 114.07205f, 0f, 10000f)
            );
        }

        // create lists
        chunks = new List<Chunk>();

        isDone = false;
        allChunksCreated = false;
        region = _layer;
        islandType = _islandType;

        float minIslandSize = World.currentWorld.maxIslandSize.x;
        float maxIslandSize = World.currentWorld.maxIslandSize.y;
        // calculate a pseudo-random island size, x and z size is the same, y is the doubled chunkHeight
        float randomValue = World.GetRandomFloat(noiseOffset[0].x - noiseOffset[1].y + noiseOffset[3].z, minIslandSize, maxIslandSize);
        // round because every voxel in the island is exactly one unit big
        islandSize.x = islandSize.z = Mathf.Round(randomValue);
        islandSize.y = Chunk.Height * 2;

        // set position
        transform.position = pos;
        // other threads will access the transform position but can't because 
        // unity forbids this -> copy position to "islandCenter" attribute
        islandCenter = pos;

        // instantiate "clouds of creation" 
        clouds = Instantiate(World.currentWorld.cloudEmitterFab, IslandCenter, Quaternion.identity) as ParticleSystem;
        clouds.transform.parent = transform;

        // load island and create chunks
        LoadIsland();

        // these are just for the UI element in the World class
        numChunks = 0;
        numChunksFinished = 0;
    }
    
    // just for volcanos
    // instantiates a smoke emitter in the caldera
    private void CreateVolcanoSmoke()
    {
        smoke = World.currentWorld.smokeEmitterFab;

        if (smoke != null)
        {
            smoke = Instantiate(smoke, IslandCenter + Vector3.up * 10f, Quaternion.Euler(-90f, 0f, 0f)) as ParticleSystem;
            smoke.transform.parent = transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isDone) { 
            return;
        }

        // it's not done yet, but all chunks have been created
        if (allChunksCreated)
        {
            Debug.Log("" + gameObject.name + " is done.");
            // for UI in World class
            numChunks = numChunksFinished = chunks.Count;

            // clouds stop and the particle system will be deleted after 18sec when 
            // all particles vanished
            clouds.Stop();
            Invoke("DeleteClouds", 18f);
            
            if (islandType == Terrain.VOLCANO) {
                CreateVolcanoSmoke();
            }

            isDone = true;
        }
        // chunks are still beeing generated
        else
        {
            numChunks = 0;
            numChunksFinished = 0;

            // create a list for empty chunks
            List<Chunk> chunksToDelete = new List<Chunk>();

            // find empty chunks and add them to the deletion list
            // if we delete it in here, the foreach loop will explode
            foreach (Chunk c in chunks)
            {
                // update all chunks
                c.UpdateChunk();

                if (c.DeletionFlag) { 
                    // empty chunk -> can be deleted
                    chunksToDelete.Add(c);
                } else {
                    // not empty -> update chunkCounter for UI
                    numChunks++;
                    if (c.IsCreated) { 
                        numChunksFinished++;
                    }
                }
            }
            // delete the empty chunks we just searched
            foreach (Chunk c in chunksToDelete)
            {
                chunks.Remove(c);
                Destroy(c.gameObject);
            }

            // there are chunks with geometry and all remaining chunks are created
            if (numChunks != 0 && numChunks == numChunksFinished)
            {
                allChunksCreated = true;
            }
        }
    }

    // is invoked after the island is created
    private void DeleteClouds()
    {
        Destroy(clouds.gameObject);
        clouds = null;
    }

    // create the chunks
    private void LoadIsland()
    {
        // get chunkFab for island depending on islandType
        Chunk chunkFab = Terrain.GetChunkPrefab(islandType);

        // instantiate all chunks in the island
        for (int y = 0; y < 2; y++) // 0 = upperchunk, -1 = lowerChunk
        {
            for (float x = (-islandSize.x / 2 - Chunk.Width); x < (islandSize.x / 2 + Chunk.Width); x += Chunk.Width)
            {
                for (float z = (-islandSize.z / 2 - Chunk.Width); z < (islandSize.z / 2 + Chunk.Width); z += Chunk.Width)
                {
                    // calculate position of the chunk
                    Vector3 pos = new Vector3(x, Chunk.Height * -y, z);
                    // floor the position so all chunks are kind of like in a grid
                    pos.x = Mathf.Floor(pos.x / (float)Chunk.Width)  * Chunk.Width;
                    pos.y = Mathf.Floor(pos.y / (float)Chunk.Height) * Chunk.Height;
                    pos.z = Mathf.Floor(pos.z / (float)Chunk.Width)  * Chunk.Width;
                    
                    // objectPos to WorldPos
                    pos += IslandCenter;

                    // instantiate the chunk
                    Chunk chunk = Instantiate(chunkFab, pos, Quaternion.identity) as Chunk;

                    // initialize and add to chunkList
                    chunk.Initialize(this, (y == 0));
                    chunks.Add(chunk);
                }
            }
        }
    }

    // GIZMOS
    // always draw yellow(creating island) or green(created island)
    // wireCube around island bounds
    Color gizmoColorCreating = new Color(1.0f, 0.92f, 0.016f, 0.7f);
    Color gizmoColorCreated  = new Color(0.5f, 1.00f, 0.009f, 0.7f);
    void OnDrawGizmos()
    {
        Gizmos.color = (isDone ? gizmoColorCreated : gizmoColorCreating);
        Gizmos.DrawWireCube(IslandCenter, islandSize);
    }
}
