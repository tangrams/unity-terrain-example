using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mapzen
{
    public class TerrainTileData
    {
        public int ZoomLevel { get; }

        public Texture2D ElevationTexture { get; set; }

        public Texture2D NormalTexture { get; set; }

        public TerrainTileData(int zoomLevel)
        {
            ZoomLevel = zoomLevel;
        }

        public bool SetElevationData(byte[] imageFileData, int pixelsPerSide)
        {
            ElevationTexture = new Texture2D(pixelsPerSide, pixelsPerSide, TextureFormat.RGBA32, false);
            return ElevationTexture.LoadImage(imageFileData);
        }

        public void GenerateElevationGrid(Mesh mesh, int resolution, Vector3 offset)
        {

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();

            int index = 0;
            for (int col = 0; col <= resolution; col++)
            {
                float y = (float)col / resolution;
                for (int row = 0; row <= resolution; row++)
                {
                    float x = (float)row / resolution;
                    vertices.Add(new Vector3(x, 0, y) + offset);
                    colors.Add(Color.white);
                    uvs.Add(new Vector2(x, y));
                    normals.Add(Vector3.up);
                    tangents.Add(new Vector4(1, 0, 0, -1));

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

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
        }

        public void ApplyElevation(Mesh mesh, float heightScale)
        {
            var meshBounds = mesh.bounds.size;
            var texBounds = new Vector2 { x = ElevationTexture.width, y = ElevationTexture.height };
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                var x = uvs[i].x / meshBounds.x * texBounds.x;
                var z = uvs[i].y / meshBounds.z * texBounds.y;
                Color color = ElevationTexture.GetPixel((int)x, (int)z);
                var elevation = ColorToElevation(color);
                const double earthCircumferenceMeters = 6378137.0 * Math.PI * 2.0;
                var tileSize = earthCircumferenceMeters / (1 << ZoomLevel);
                var height = elevation / tileSize * Math.Max(meshBounds.x, meshBounds.z);
                vertices[i].y = (float)height * heightScale;
            }
            mesh.vertices = vertices;
        }

        public bool SetNormalData(byte[] imageFileData, int pixelsPerSide)
        {
            NormalTexture = new Texture2D(pixelsPerSide, pixelsPerSide, TextureFormat.RGBA32, false);
            return NormalTexture.LoadImage(imageFileData);
        }

        public void ApplyNormalTexture(Material material)
        {
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_BumpMap", NormalTexture);
        }

        public void RemoveNormalTexture(Material material)
        {
            material.DisableKeyword("_NORMALMAP");
        }

        public static float ColorToElevation(Color color)
        {
            return (color.r * 256.0f * 256.0f + color.g * 256.0f + color.b) - 32768.0f;
        }
    }
}

