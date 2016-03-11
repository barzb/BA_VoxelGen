using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class Vegetation : MonoBehaviour
{
    // PREFABS (set in insprector)
    public GameObject[] greenTrees;
    public GameObject[] swampTrees;
    public GameObject[] dschungleTrees;

    // STATIC ATTRIBUTES
    // list with ALL trees
    private static List<GameObject> globalTreeList = new List<GameObject>();
    // list with trees that are yet to create
    private static List<Vector3> treesToCreate = new List<Vector3>();
    // synced with treesToCreate -> parent chunk objects of the trees
    private static List<Transform> treeParents = new List<Transform>();
    // visibility toggle for (all) trees
    private static bool showTrees = true;

    // PROPERTIES
    // Show or hide all trees.
    public static bool ShowTrees 
    {
        get { return showTrees; }
        set
        {
            // do not do anything if value is already current status
            if (showTrees == value) return;

            // hide all existing trees
            showTrees = value;
            foreach (GameObject tree in globalTreeList)
            {
                tree.SetActive(showTrees);
            }
        }
    }

    /*** Generates Trees ***
       Search for random vertices in the chunk mesh and check if they are
       a legit tree spawn position. -> Add the position to creation queue.
       - is the terrain point facing up?
       - is the point in a cave or is other terrain above the point?
       - are other trees nearby that are too close?
       - is the vertex near the terrain border?
    */
    public static void GenerateTrees(Chunk target, int maxNum = 0)
    {
        // cancel if no trees need to be created
        if (maxNum <= 0) return;

        // get mesh of the chunk
        Mesh m = target.Mesh;
        // no mesh found or mesh is empty
        if (m == null || m.vertexCount < 1) return;
        
        // get number of vertices in mesh
        int vCount = m.vertexCount;

        // in case the mesh has less vertices than maxTrees
        maxNum = Math.Min(vCount/10, maxNum);

        // so we don't end up in an endless loop
        int safetyCounter = maxNum * 10;
        // don't create too much trees and don't try to end up in endless loop
        while(maxNum > 0 && --safetyCounter > 0)
        {
            // create a start value for the pseudo random number generator
            int randomNum = Mathf.RoundToInt(target.Position.x - target.Position.z + safetyCounter*1234 - maxNum*912);
            // get a random vertex in the mesh
            int vertexIndex = World.GetRandomInt(randomNum, 0, vCount);

            // vertex is flat terrain? we don't want trees at hillsides
            if(m.normals[vertexIndex].y > 0.8f)
            {
                // get position of the vertex in world space
                Vector3 treePos = m.vertices[vertexIndex] + target.Position;

                // check if position is in a cave or terrain is 50m above the position
                RaycastHit hit;
                if (Physics.Raycast(treePos, Vector3.up, out hit, 50f))
                {
                    // was terrain hit or something other?
                    Chunk hitChunk = hit.collider.gameObject.GetComponent<Chunk>();
                    // it was terrain!
                    if (hitChunk != null)
                    {
                        // this is no place for a tree. search for other position
                        continue;
                    }
                }


                // check if position is directly at the border of the island
                bool borderTree = false;
                Vector3 offset = new Vector3(3f, 2f, 0f);
                for(int i = 0; i < 6; i++)
                {
                    if (!Physics.Raycast(treePos + offset, Vector3.down, out hit, 4f))
                    {
                        borderTree = true;
                        break;
                    }
                    // rotate the offset 60°
                    offset = Quaternion.Euler(0f, 60f, 0f) * offset;
                }
                if (borderTree) continue;


                // now check if another tree is too close
                bool awkward = false;
                foreach(Vector3 pos in treesToCreate)
                {
                    // closer than 3m ?
                    if(Vector3.Distance(pos, treePos) < 5f)
                    {
                        // no ->  discard this position
                        awkward = true;
                        break;
                    }
                }
                if(awkward) continue;
                
                // ----------- FINALLY! ----------
                // this is a good place for a tree
                treesToCreate.Add(treePos);
                treeParents.Add(target.transform);
                // one less to create
                maxNum--;
            }
        }
    }

    // create only 1 tree per frame (performaaaance!)
    void Update()
    {
        //  press "T" to hide/show trees
        if(Input.GetKeyUp(KeyCode.T))
        {
            ShowTrees = !ShowTrees;
        }

        // grab first tree from list if there are any left to create
        if (treesToCreate.Count > 0)
        {
            // pop the first position of the list
            Vector3 creationPos = treesToCreate[0];
            treesToCreate.RemoveAt(0);

            // pop the first parent transform reference from the list
            Transform parentObj = treeParents[0];
            treeParents.RemoveAt(0);

            // get chunk reference of tree parent
            Chunk parentChunk = parentObj.GetComponent<Chunk>();
            if (parentChunk == null) return;
            // get terrain type
            Terrain chunkType = parentChunk.Island.IslandType;
            // reference tree prefabs based on terrain type
            GameObject[] trees = greenTrees;
            if (chunkType == Terrain.SWAMP) { 
                trees = swampTrees;
            } else if (chunkType == Terrain.DSCHUNGLE) { 
                trees = dschungleTrees;
            }

            float randomNum = creationPos.x - creationPos.z + creationPos.y;
            // use one random tree from the list of tree prefabs
            int treeNum = World.GetRandomInt(Mathf.RoundToInt(randomNum * 197.5902f), 0, trees.Length);
            // calculate random rotation
            Quaternion treeRotation = Quaternion.Euler(0f, World.GetRandomFloat(randomNum * 192.012f, 0f, 360f), 0f);

            // instantiate the tree
            GameObject treeInstance = Instantiate(trees[treeNum], creationPos, treeRotation) as GameObject;
            // set parent of the tree
            treeInstance.transform.parent = parentObj;

            // add to list of all trees
            globalTreeList.Add(treeInstance);

            // hide tree if showTrees is false
            if(!showTrees) {
                treeInstance.SetActive(false);
            }
        }
    }
}
