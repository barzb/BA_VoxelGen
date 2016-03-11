using UnityEngine;
using System.Collections;

public sealed class Terrain
{
    // STATIC TERRAIN TYPE PRESETS
    // ................  Parameters (LABEL | TREECOUNT | HEIGHT | GROUNDHEIGHT | HAS CAVES)
    public static Terrain ICY_MOUNTAIN = new Terrain("ICY_MOUNTAIN", 0, 1.8f, 0.0f, true);
    public static Terrain SNOW_PLANES  = new Terrain("SNOW_PLANES" , 0, 0.8f, 0.1f, false);
    public static Terrain FOREST       = new Terrain("FOREST"      , 8, 1.0f, 0.0f, true);
    public static Terrain GREENLAND    = new Terrain("GREENLAND"   , 2, 0.8f, 0.1f, false);
    public static Terrain SWAMP        = new Terrain("SWAMP"       , 5, 0.5f, 0.3f, false);
    public static Terrain DSCHUNGLE    = new Terrain("DSCHUNGLE"   , 6, 1.0f, 0.0f, false);
    public static Terrain DESERT       = new Terrain("DESERT"      , 0, 0.5f, 0.3f, false);
    public static Terrain VOLCANO      = new Terrain("VOLCANO"     , 0, 1.8f, 0.0f, false);

    // ATTRIBUTES
    private string label;
    private int treeCount;
    private float height;
    private float groundHeight;
    private bool hasCaves;
    private bool hasVolcano;

    // PROPERTIES
    public string Label { get { return label; } }
    public int TreeCount { get { return treeCount; } }
    public float Height { get { return height; } }
    public float GroundHeight { get { return groundHeight; } }
    public bool HasCaves { get { return hasCaves; } }

    // CONSTRUCTOR
    public Terrain(string _label, int _treeCount, float _height, float _groundHeight, bool _hasCaves)
    {
        label = _label;
        treeCount = _treeCount;
        height = _height;
        groundHeight = _groundHeight;
        hasCaves = _hasCaves;
    }

    // chooeses a terrain type depending on layer
    public static Terrain ChooseTerrain(Region layer, int randomNumber)
    {
        // Choose the island type depending on the zone layer
        int randomTerrain;
        switch (layer)
        {
            // ------ ICY ------
            // Islands in the ICY region have a 1/2 chance to become snow playnes or icy mountains
            case Region.ICY:
                {
                    randomTerrain = randomNumber % 2;
                    return (randomTerrain == 0 ? Terrain.SNOW_PLANES : Terrain.ICY_MOUNTAIN);
                }
            // ------ GREEN ------
            // Islands in the GREEN region have a 1/3 chance to become a swamp,
            // a 1/3 chance to become greenland and 1/3 chance to become a forest
            case Region.GREEN:
                {
                    randomTerrain = randomNumber % 3;
                    if (randomTerrain == 0)
                        return Terrain.SWAMP;
                    else if (randomTerrain == 1)
                        return Terrain.GREENLAND;
                    else
                        return Terrain.FOREST;
                }

            // ------ TROPICAL ------
            // Islands in the TROPICAL region have a 1/4 chance to become a swamp,
            // and a 3/4 chance to become dschungle
            case Region.TROPICAL:
                {
                    randomTerrain = randomNumber % 4;
                    if (randomTerrain == 0)
                        return Terrain.SWAMP;
                    else
                        return Terrain.DSCHUNGLE;
                }
            // ------ SAND ------
            // Islands in the SAND region are always desert
            case Region.SAND:
                {
                    return Terrain.DESERT;
                }

            // ------ LAVA ------
            // Islands in the LAVA region are always vulcanos
            case Region.LAVA:
                {
                    return Terrain.VOLCANO;
                }
        }

        // DEFAULT
        return Terrain.GREENLAND;
    }

    // returns the chunk prefab for the terrain type
    public static Chunk GetChunkPrefab(Terrain terr)
    {
        switch (terr.Label)
        {
            case "SNOW_PLANES":
            case "ICY_MOUNTAIN":
                return World.currentWorld.snowChunk;

            case "DESERT":
                return World.currentWorld.sandChunk;

            case "VOLCANO":
                return World.currentWorld.lavaChunk;

            case "SWAMP":
                return World.currentWorld.swampChunk;

            default:
                return World.currentWorld.greenChunk;
        }

    }
}
