using UnityEngine;
using System.Collections;

// REGION for islandType calculation
// the integer values are used by the WorldChunk class
public enum Region
{
    ICY      = +1, // Snow Planes, Icy Mountains
    GREEN    = +0, // Swamp, Greenland, Forest
    TROPICAL = -1, // Dschungle, Swamp
    SAND     = -2, // Desert, Savanna
    LAVA     = -3  // Volcano
};

// structure for MarchingCubes
// the voxel array of a chunk is transformed into one of these
public struct GridCell
{
    public Vector3[] p; // position
    public Vector3[] n; // normal
    public float[] val; // value

    // constructor
    public GridCell(Vector3[] _p, Vector3[] _n, float[] _val)
    {
        p = _p;
        n = _n;
        val = _val;
    }
}

// output of the MarchingCubes algorithm
// will be transformed into MeshData
public struct Triangle
{
    public Vector3[] p; // position
    public Vector3[] n; // normal

    // constructor
    public Triangle(Vector3[] _p, Vector3[] _n)
    {
        p = _p;
        n = _n;
    }
}

// everything a mesh needs
// will be calculated in seperate threads and applied to
// the mesh in the mainThread
public struct MeshData
{
    Vector3[] verts;
    int[] indices;
    Vector3[] normals;

    // constructor
    public MeshData(Vector3[] _verts, Vector3[] _normals, int[] _indices)
    {
        verts = _verts;
        normals = _normals;
        indices = _indices;
    }

    // apply meshData to the mesh
    // this function is called from the main thread
    public void SetMeshData(Mesh mesh)
    {
        // just in case
        if (mesh == null) { 
            mesh = new Mesh();
        }

        mesh.vertices = verts;
        mesh.triangles = indices;
        mesh.normals = normals;

        // Recalculate the bounding volume of 
        // the mesh from the vertices
        mesh.RecalculateBounds();

        // should always be called for generated meshs
        // makes the mesh rendering faster (stripification and stuff)
        mesh.Optimize();
    }
}
