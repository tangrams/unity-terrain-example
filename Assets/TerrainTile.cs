using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainTile : MonoBehaviour
{
    // Editor-visible fields:
    public int ZoomLevel = 11;
    public int Resolution = 32;
    public bool UseNormalMap = true;
    public Texture2D ElevationTexture;
    public Texture2D NormalTexture;

    // Internal fields:
    private int mResolution;
    private bool mUseNormalMap;

    // Runs once when the GameObject is created.
    public void Start()
    {
        // When this GameObject is created, create an elevation mesh.
        ApplyElevation();
    }

    // Generate a uniform grid of vertices, elevate them according to a texture, and apply a normal map.
    private void ApplyElevation()
    {
        // Set the mesh for this GameObject to a new Mesh instance.
        var mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        // Generate a vertex grid for the mesh. The offset vector makes the final mesh centered on 0 in X and Z.
        GenerateElevationGrid(mesh, Resolution, new Vector3(-0.5f, 0.0f, -0.5f));

        // Elevate the vertices of the mesh.
        ApplyElevation(mesh);

        // Apply or remove the normal map from our material, based on the option set in the editor.
        var material = GetComponent<MeshRenderer>().material;
        if (UseNormalMap)
        {
            ApplyNormalTexture(material);
        }
        else
        {
            RemoveNormalTexture(material);
            // When the normal map is not used, use approximate normals calculated at vertices.
            mesh.RecalculateNormals();
        }

        mResolution = Resolution;
        mUseNormalMap = UseNormalMap;
    }

    // Runs once per frame.
    public void Update()
    {
        // When the mouse button is held, use the mouse position to rotate the model.
        if (Input.GetMouseButton(0))
        {
            transform.Rotate(Vector3.up, -Input.GetAxis("Mouse X") * 10.0f);
        }

        // If the editor has updated any options, apply them now.
        if (mResolution != Resolution || mUseNormalMap != UseNormalMap)
        {
            ApplyElevation();
        }
    }

    // Generate a uniform 2D grid of vertices for the mesh, with resolution+1 vertices on each side.
    // The vertices span 0.0-1.0 in X and Z by default. The offset vector is added to every vertex position.
    public void GenerateElevationGrid(Mesh mesh, int resolution, Vector3 offset)
    {
        // Create fresh, empty lists for all of the vertex values we need to set.
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();

        // Iterate over the rows and columns of a grid in X and Z.
        int index = 0;
        for (int col = 0; col <= resolution; col++)
        {
            float y = (float)col / resolution;
            for (int row = 0; row <= resolution; row++)
            {
                float x = (float)row / resolution;

                // Add the values for a new vertex.
                vertices.Add(new Vector3(x, 0, y) + offset);
                colors.Add(Color.white);
                uvs.Add(new Vector2(x, y));

                // We add the 'up' vector as the normal for every vertex so that when we apply our normal map,
                // it will be effectively interpreted as 'object space' normals rather than 'tangent space'.
                normals.Add(Vector3.up);

                // Similar to the normals, we add the 'right' vector as the tangent for every vertex so that
                // our added normal map will be treated like 'object space'. The 4th value determines the sign
                // of cross product used to calculate the binormal, for us it happens to be -1.
                tangents.Add(new Vector4(1, 0, 0, -1));

                // Add indices for form triangles between this vertex and its neighbors left and down, unless
                // we're at the end of a column or row.
                if (row < resolution && col < resolution)
                {
                    indices.Add(index);
                    indices.Add(index + resolution + 1);
                    indices.Add(index + 1);

                    indices.Add(index + 1);
                    indices.Add(index + resolution + 1);
                    indices.Add(index + resolution + 2);
                }

                index++;
            }
        }

        // Clear the previous values from the mesh and set our newly made values.
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
    }

    public void ApplyElevation(Mesh mesh)
    {
        // Iterate over the vertex positions and UVs of the mesh.
        var vertices = mesh.vertices;
        var uvs = mesh.uv;
        for (int i = 0; i < vertices.Length; i++)
        {
            // At each vertex, sample the elevation texture at the corresponding UV coordinate.
            int x = Convert.ToInt32(uvs[i].x * ElevationTexture.width);
            int y = Convert.ToInt32(uvs[i].y * ElevationTexture.height);
            Color color = ElevationTexture.GetPixel(x, y);

            // Convert the resulting color value to an elevation in meters.
            float elevation = ColorToElevation(color);

            // Use the tile size in meters at the given zoom level to determine the relative
            // scale of elevation values in the mesh.
            const double earthCircumferenceMeters = 6378137.0 * Math.PI * 2.0;
            double tileSize = earthCircumferenceMeters / (1 << ZoomLevel);
            double height = elevation / tileSize;
            vertices[i].y = (float)height;
        }
        // Assign the new vertex positions to the mesh.
        mesh.vertices = vertices;
    }

    public void ApplyNormalTexture(Material material)
    {
        // https://docs.unity3d.com/Manual/MaterialsAccessingViaScript.html
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_BumpMap", NormalTexture);
    }

    public void RemoveNormalTexture(Material material)
    {
        material.DisableKeyword("_NORMALMAP");
    }

    public static float ColorToElevation(Color color)
    {
        // Convert from color channel values in 0.0-1.0 range to elevation in meters:
        // https://mapzen.com/documentation/terrain-tiles/formats/#terrarium
        return (color.r * 256.0f * 256.0f + color.g * 256.0f + color.b) - 32768.0f;
    }
}
