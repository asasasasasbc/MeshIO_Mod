using MeshIO;
using MeshIO.FBX;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
using MeshIO.Entities.Geometries.Layers;
using MeshIO.Shaders; // For MeshIO.Shaders.Material
using CSMath; // For XYZ, XY, Color
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshIO.Examples.Common;

namespace MeshIO.Examples.Fbx
{
    public static class FbxWriterExample
    {
        public static void ExportComplexCube(string outputPath)
        {
            // 1. Create a scene
            Scene scene = new Scene { Name = "ComplexCubeScene" };

            // 2. Create a Node for our cube
            Node cubeNode = new Node { Name = "MyCubeNode" };
            scene.RootNode.Nodes.Add(cubeNode);

            // 3. Create Materials
            var redMaterial = new MeshIO.Shaders.Material
            {
                Name = "RedMaterial",
                DiffuseColor = new Color(255, 0, 0) // Red
            };
            var blueMaterial = new MeshIO.Shaders.Material
            {
                Name = "BlueMaterial",
                DiffuseColor = new Color(0, 0, 255) // Blue
            };

            // Add materials to the node (FBX often links materials through nodes)
            cubeNode.Materials.Add(redMaterial);
            cubeNode.Materials.Add(blueMaterial);

            // 4. Create a Mesh entity
            Mesh cubeMesh = new Mesh { Name = "MyCubeMesh" };
            cubeNode.Entities.Add(cubeMesh);

            // 5. Define Mesh Data

            // Vertices (8 vertices for a cube)
            cubeMesh.Vertices.AddRange(new List<XYZ>
            {
                new XYZ(-1, -1, -1), // 0
                new XYZ( 1, -1, -1), // 1
                new XYZ( 1,  1, -1), // 2
                new XYZ(-1,  1, -1), // 3
                new XYZ(-1, -1,  1), // 4
                new XYZ( 1, -1,  1), // 5
                new XYZ( 1,  1,  1), // 6
                new XYZ(-1,  1,  1)  // 7
            });

            // Faces (12 triangles for a cube)
            // Note: FBX polygon vertex indices are 0-based.
            // The last index of a polygon is negated and decremented by 1 to mark the end.
            // For triangles, this means (idx0, idx1, -(idx2+1))
            // However, MeshIO's Polygon/Triangle classes handle this internally for FBX export.
            var faces = new List<Triangle>
            {
                // Front face (Z-) - Material 0
                new Triangle(0, 1, 2), new Triangle(0, 2, 3),
                // Back face (Z+) - Material 0
                new Triangle(5, 4, 7), new Triangle(5, 7, 6),
                // Left face (X-) - Material 0
                new Triangle(4, 0, 3), new Triangle(4, 3, 7),
                // Right face (X+) - Material 1
                new Triangle(1, 5, 6), new Triangle(1, 6, 2),
                // Top face (Y+) - Material 1
                new Triangle(3, 2, 6), new Triangle(3, 6, 7),
                // Bottom face (Y-) - Material 1
                new Triangle(4, 5, 1), new Triangle(4, 1, 0)
            };
            cubeMesh.Polygons.AddRange(faces);

            // Layer Elements
            // Normals, Tangents, UVs are often defined per-polygon-vertex.
            // For a cube with 12 triangles, there are 12 * 3 = 36 polygon vertices.

            // Normals (LayerElementNormal)
            var normalLayer = new LayerElementNormal
            {
                Name = "CubeNormals",
                MappingMode = MappingMode.ByPolygonVertex, // One normal per vertex of each polygon
                ReferenceMode = ReferenceMode.Direct       // Normals are listed directly
            };
            // For simplicity, assign face normals, duplicated for each vertex of that face.
            XYZ nFront = new XYZ(0, 0, -1);
            XYZ nBack = new XYZ(0, 0, 1);
            XYZ nLeft = new XYZ(-1, 0, 0);
            XYZ nRight = new XYZ(1, 0, 0);
            XYZ nTop = new XYZ(0, 1, 0);
            XYZ nBottom = new XYZ(0, -1, 0);

            normalLayer.Normals.AddRange(Enumerable.Repeat(nFront, 6));  // Front face (2 triangles)
            normalLayer.Normals.AddRange(Enumerable.Repeat(nBack, 6));   // Back face
            normalLayer.Normals.AddRange(Enumerable.Repeat(nLeft, 6));   // Left face
            normalLayer.Normals.AddRange(Enumerable.Repeat(nRight, 6));  // Right face
            normalLayer.Normals.AddRange(Enumerable.Repeat(nTop, 6));    // Top face
            normalLayer.Normals.AddRange(Enumerable.Repeat(nBottom, 6));// Bottom face
            cubeMesh.Layers.Add(normalLayer);

            // Tangents (LayerElementTangent)
            var tangentLayer = new LayerElementTangent
            {
                Name = "CubeTangents",
                MappingMode = MappingMode.ByPolygonVertex,
                ReferenceMode = ReferenceMode.Direct
            };
            // Simplified tangents (e.g., mostly along X-axis for faces)
            // A real model would have tangents derived from UVs.
            XYZ tX = new XYZ(1, 0, 0);
            XYZ tY = new XYZ(0, 1, 0);
            XYZ tZ = new XYZ(0, 0, 1); // Not typically used directly as tangent like this

            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tX, 6)); // Front
            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tX, 6)); // Back
            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tZ, 6)); // Left (tangent along Z for X-normal face)
            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tZ, 6)); // Right
            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tX, 6)); // Top
            tangentLayer.Tangents.AddRange(Enumerable.Repeat(tX, 6)); // Bottom
            cubeMesh.Layers.Add(tangentLayer);

            // Texture Coordinates (UVs) (LayerElementUV)
            var uvLayer = new LayerElementUV
            {
                Name = "CubeUVs",
                MappingMode = MappingMode.ByPolygonVertex,
                ReferenceMode = ReferenceMode.IndexToDirect // Use a shared pool of UVs
            };

            // Define a pool of unique UV coordinates for a standard cube map
            uvLayer.UV.AddRange(new List<XY>
            {
                new XY(0, 0), // 0
                new XY(1, 0), // 1
                new XY(0, 1), // 2
                new XY(1, 1)  // 3
            });

            // Define UV indices for each of the 36 polygon vertices.
            // Each face (2 triangles) will map to these 4 UVs (0,0), (1,0), (0,1), (1,1)
            // Tri 1: (0,1)-(1,1)-(1,0) -> UV indices: 2, 3, 1
            // Tri 2: (0,1)-(1,0)-(0,0) -> UV indices: 2, 1, 0
            var faceUvIndices = new int[] { 2, 3, 1, 2, 1, 0 };
            for (int i = 0; i < 6; i++) // For each of the 6 cube faces
            {
                uvLayer.Indexes.AddRange(faceUvIndices);
            }
            cubeMesh.Layers.Add(uvLayer);

            // Material Index for each mesh face (LayerElementMaterial)
            var materialLayer = new LayerElementMaterial
            {
                Name = "CubeFaceMaterials",
                MappingMode = MappingMode.ByPolygon,    // One material index per polygon (triangle in our case)
                ReferenceMode = ReferenceMode.Direct    // Indices directly reference the material list on the Node
            };

            // We have 12 triangles (faces). Assign materials.
            // First 3 faces (6 triangles) get Material 0 (RedMaterial)
            // Next 3 faces (6 triangles) get Material 1 (BlueMaterial)
            for (int i = 0; i < 6; i++) materialLayer.Indexes.Add(0); // Red
            for (int i = 0; i < 6; i++) materialLayer.Indexes.Add(1); // Blue
            cubeMesh.Layers.Add(materialLayer);


            // 6. Set up FbxWriterOptions
            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = true, // Or false for ASCII
                Version = FbxVersion.v7400 // A common modern version
                // GlobalSettings can be configured here if needed, otherwise defaults are used.
            };

            // 7. Write the scene to a file
            try
            {
                Console.WriteLine($"Exporting complex cube to: {outputPath}");
                FbxWriter.Write(outputPath, scene, options, NotificationHelper.LogConsoleNotification);
                Console.WriteLine("Export complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during FBX export: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // You can call this from your Main method or a test runner
        public static void RunExample()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "ComplexCubeExport.fbx");
            ExportComplexCube(filePath);

            // For verification, you can try reading it back
            Console.WriteLine($"\nAttempting to read back {filePath} (header only):");
            try
            {
                using (FbxReader reader = new FbxReader(filePath))
                {
                    reader.OnNotification += NotificationHelper.LogConsoleNotification;
                    FbxRootNode node = reader.Parse();
                    Console.WriteLine($"Successfully parsed. FBX Version: {node.Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading back FBX: {ex.Message}");
            }
        }
    }
}