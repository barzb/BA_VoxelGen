using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*** World class *** 
   This is the main class for all the procedurally generated stuff.
   It handles the ThreadManager and all the islands and chunks. 
*/
[RequireComponent (typeof(Vegetation))]
public sealed class World : MonoBehaviour
{
    // --------- WORLD ATTRIBUTES ---------
    // only instance of World to be accessed
	public static World currentWorld;

    public int seed = 0;
    public Vector3 worldChunkSize;
    public Vector2 maxIslandSize;
    public float isoLevel = 5f;

    // worldchunk at player position
    private WorldChunk currentWorldChunk;
    
    // --------- ISLAND ATTRIBUTES ---------
    // parent game object for islands
    private Transform islandWrapper;
    // list of all loaded islands
    private List<Island> islands;

    // chunk type prefabs
    // accessed from Terrain.GetChunkPrefab(..)
    public Chunk greenChunk;
    public Chunk swampChunk;
    public Chunk sandChunk;
    public Chunk snowChunk;
    public Chunk lavaChunk;

    // particle system prefabs
    public ParticleSystem cloudEmitterFab;
    public ParticleSystem smokeEmitterFab;
    
    // chunk size
    public int chunkWidth = 40;
	public int chunkHeight = 40;

    // --------- INFO/UI ATTRIBUTES ---------
    public Text infoText;
    // time since game started
    private float buildTime = 0f;

    // --------- THREAD ATTRIBUTES ---------
    private static ThreadManager threadManager;
    // number of threads allowed
    public uint maxNumThreads = 4;

    // ---------- PLAYER ----------
    // player transform reference
    private Transform playerTransform;

    // ---------- PROPERTIES ----------
    // can be accessed from everywhere so we don't need to use GameObject.Find everytime
    public Transform PlayerTransform { get { return playerTransform; } }


    // Awake is called before Start() of this and other MonoBehaviour classes
    void Awake ()
    {
        // check if worldChunks are big enough for the biggest possible island
        if (worldChunkSize.x < maxIslandSize.y) {
            // maxIslandSize.x = minimum size, .y = maximum size
            float size = maxIslandSize.y * 1.5f;
            worldChunkSize = new Vector3(size, size, size);
        }

        // set static current world reference
        currentWorld = this;

        // set seed
        if (seed == 0) { 
			seed = Random.Range(0, int.MaxValue);
        }
        Random.seed = seed;

        // set player transform reference
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) {
            Debug.LogError("Player not found. (Tag \"Player\" missing?)");
            playerTransform = transform;
        } else {
            playerTransform = player.transform;
        }
    }

    // executed after Awake()
    void Start()
    {
        // hide and lock cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        // instance a thread manager with max num threads from inspector value
        threadManager = ThreadManager.Instance(maxNumThreads);

        // call this method only every 10sec to save performance
        InvokeRepeating("TenSecondClock", 0.1f, 10f);
        
        updateGUIText();
       
        // init island list
        islands = new List<Island>();

        // create parent object for islands
        islandWrapper = (new GameObject("ISLANDS")).transform;

        //AddIsland(Island.CreateIsland(new Vector3(0, 0, 0), Region.SAND, Terrain.SWAMP));
    }
    

    // invoked every 10 seconds
    private void TenSecondClock()
    {
        // sort the list of jobs in ThreadManager
        ThreadManager.SortJobs();
    }

    // update currentWorldChunk if player left the last current one
    private void UpdateWorldChunks()
    {
        // Chunk at player position is not yet created
        if (currentWorldChunk == null || !currentWorldChunk.IsObjectInChunk(playerTransform.position))
        {
            // check if chunk is existing
            currentWorldChunk = WorldChunk.FindChunk(playerTransform.position);

            Vector3 chunkPos = WorldChunk.WorldPosToChunkPos(playerTransform.position);

            // only in allowed layers
            if (currentWorldChunk == null
              && (chunkPos.y <= ((int)Region.ICY  * worldChunkSize.x))
              && (chunkPos.y >= ((int)Region.LAVA * worldChunkSize.x))
            ) {
                currentWorldChunk = new WorldChunk(chunkPos);
            }

            // load neighbors
            if (currentWorldChunk != null) {
                currentWorldChunk.LoadNeighbors();
            }
        }
    }
    
    // add an island to the list of islands and under the parent object
    public void AddIsland(Island island)
    {
        islands.Add(island);
        island.transform.SetParent(islandWrapper);
    }
       

    // update is called once per frame
    void Update()
    {
        // update thread manager
        threadManager.Update();

        // Lerp player back a little, if higher or lower than max/min height
        /// player > maxHeight
        if (playerTransform.position.y > ((int)Region.ICY * worldChunkSize.y) + worldChunkSize.y) {
            playerTransform.position = 
                Vector3.Lerp(playerTransform.position, 
                (playerTransform.position + Vector3.down),
                Time.deltaTime * 10f
            );
        }
        /// player  < minHeight
        else if (playerTransform.position.y < ((int)Region.LAVA * worldChunkSize.y) - worldChunkSize.y) { 
            playerTransform.position = 
                Vector3.Lerp(playerTransform.position, 
                (playerTransform.position + Vector3.up), 
                Time.deltaTime * 10f
            );
        }

        // cursor stuff
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.visible = !Cursor.visible;
            if (Cursor.visible) { 
                Cursor.lockState = CursorLockMode.Confined;
            } else { 
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        // check if the player changed the current world chunk
        UpdateWorldChunks();
        
        // update UI
        buildTime = Time.realtimeSinceStartup;
        updateGUIText();
    }
    
    // update the UI
    private void updateGUIText()
    {
        // just in case
        if (infoText == null) { 
            infoText = GameObject.Find("MeshCreateInfo").GetComponent<Text>();
        }
        // unity sometimes fails to load UI stuff.
        if (infoText == null) { 
            return;
        }
        
        string text = "WorldSeed: " + seed +
                      "\nChunkSize: " + chunkWidth + ", " + chunkHeight + ", " + chunkWidth +
                      "\nBuildTime: " + buildTime + " sec" +
                      "\nActiveThreads: " + (1 + ThreadManager.ActiveThreads);

        // currendWorldChunk has an island
        if (currentWorldChunk != null && currentWorldChunk.HasIsland)
        {
            int numChunksFinished = currentWorldChunk.ContainingIsland.NumChunksFinished;
            int numChunks = currentWorldChunk.ContainingIsland.NumChunks;
            
            text += 
                "\nCurrent WorldChunk: " + currentWorldChunk.Position.x + ", " + currentWorldChunk.Position.y + ", " + currentWorldChunk.Position.z +
                "\nZone: " + currentWorldChunk.Zone +
                "\nIslandType: " + currentWorldChunk.ContainingIsland.IslandType.Label +
                "\nIslandSize:  " + (int)(currentWorldChunk.ContainingIsland.IslandSize.x) + 
                ", " + (chunkHeight * 2) + ", " + (int)(currentWorldChunk.ContainingIsland.IslandSize.y) +
                "\nChunks: " + numChunksFinished + "/" + numChunks;
        } 

        // apply text to ui
        infoText.text = text;
    }


    // Abort all threads when the game is closed.
    // this is important.. trust me...
    void OnApplicationQuit()
    {
        ThreadManager.AbortThreads();
    }


    // Get a pseudo-random number, based on a recreatable seed and the world seed
    public static int GetRandomInt(int uniqueSeed, int min, int max)
    {
        System.Random random = new System.Random(uniqueSeed + currentWorld.seed);
        return random.Next(min, max);
    }
    public static float GetRandomFloat(float uniqueSeed, float min, float max)
    { 
        System.Random random = new System.Random(Mathf.RoundToInt(Mathf.Abs(uniqueSeed + currentWorld.seed)));
        return ((float)random.NextDouble() * (max - min)) + min;
    }
    

    // --- GIZMOS ---
    Color borderColor       = new Color(1.0f, 1.0f, 1.0f, 0.05f);
    Color emptyChunkColor   = new Color(0.5f, 0.5f, 0.5f, 0.15f);
    Color islandChunkColor  = new Color(0.0f, 1.0f, 0.0f, 0.15f);
    Color currentChunkColor = new Color(0.0f, 0.0f, 1.0f, 0.15f);
    Color playerColor       = new Color(1.0f, 0.0f, 0.0f, 0.75f);
    void OnDrawGizmosSelected()
    {
        if (currentWorld == null) return;

        // draw a cube around each loaded worldChunk
        foreach (WorldChunk chunk in WorldChunk.LoadedWorldChunks)
        {
            Gizmos.color = chunk.HasIsland ? islandChunkColor : emptyChunkColor;
            Gizmos.DrawCube(chunk.Position, worldChunkSize);
            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(chunk.Position, worldChunkSize);
        }

        // color the currentWorldChunk
        if (currentWorldChunk != null)
        {

            Gizmos.color = currentChunkColor;
            Gizmos.DrawCube(currentWorldChunk.Position, worldChunkSize);
        }

        // draw something for the player
        Gizmos.color = playerColor;
        Vector3 pp = playerTransform.position;
        Gizmos.DrawLine(pp + Vector3.left    * 10f, pp + Vector3.right * 10f);
        Gizmos.DrawLine(pp + Vector3.up      * 10f, pp + Vector3.down  * 10f);
        Gizmos.DrawLine(pp + Vector3.forward * 10f, pp + Vector3.back  * 10f);
    }
}
