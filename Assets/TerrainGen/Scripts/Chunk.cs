using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimplexNoise;

/*** Chunk Class ***
   This is attached to the chunk gameObject and does all the mainThread
   calculation for the chunk creation. Everything that happens in the 
   working threads is calculated in the chunkCreator class.
*/
[RequireComponent (typeof(MeshRenderer))]
[RequireComponent (typeof(MeshFilter))]
[RequireComponent (typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    // ATTRIBUTES
    private Vector3 position;
    private bool created;
    private bool deletionFlag;
    private bool upperChunk;
    private ChunkCreator chunkCreator;
	private Mesh mesh;
    
    // REFERENCES
    private Island island;
	private MeshCollider meshCollider;
	private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    
    // PROPERTIES
    public static int Width  { get { return World.currentWorld.chunkWidth; } }
    public static int Height { get { return World.currentWorld.chunkHeight; } }
    public Island Island     { get { return island; } }
    public Vector3 Position  { get { return position; } }
    public Mesh Mesh         { get { return mesh; } }
    public bool IsCreated    { get { return created; } }
    public bool DeletionFlag { get { return deletionFlag; } }
    public bool IsUpperChunk { get { return upperChunk; } }

    // METHODS

    // this is like an constructor but the chunks are beeing instantiated
    // so this initializes the attributes in the chunk
    public void Initialize(Island _island, bool _upperChunk)
    {
        island = _island;
        transform.parent = island.transform;
        position = transform.position;
        upperChunk = _upperChunk;

        deletionFlag = false;
        created = false;

        // set component references
        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // this creates the job that will generate the chunk mesh
        chunkCreator = new ChunkCreator(this);

        // add the job to the ThreadManager's jobList
        ThreadManager.AddJob(chunkCreator);
    }

    // only needs to be called when mesh is beeing calculated (in seperate threads)
    public void UpdateChunk()
    {
        // check if job is done
        if (!created && chunkCreator.IsDone)
        {
            created = true;

            if (chunkCreator.ContainsBlocks) {
                // apply mesh if it isn't empty
                ApplyMesh();
                // generate trees (or not, depending on terrain type)
                Vegetation.GenerateTrees(this, island.IslandType.TreeCount);
            } else {
                // set deletion flag if mesh is empty 
                // (chunk will be deleted in island update)
                deletionFlag = true;
            }
        }
    }

    // set mesh data to components
    private void ApplyMesh()
    {
        // grab mesh data from job
        //++++++map = chunkCreator.map;

        // create mesh from mesh data
        mesh = new Mesh();
        chunkCreator.MeshData.SetMeshData(mesh);
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
        
        // space eventually cleaned by garbage collector
        chunkCreator = null;
    }
}






