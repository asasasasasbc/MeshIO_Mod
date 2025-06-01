// MeshIO.Examples/Fbx/FbxSkinningExample.cs
using MeshIO;
using MeshIO.FBX;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
using MeshIO.Entities.Geometries.Layers;
using MeshIO.Entities.Skinning; // For Skin, Cluster
using MeshIO.Shaders;
using CSMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshIO.Examples.Common; // For NotificationHelper
using MeshIO.Utils; // For Matrix4Extensions

namespace MeshIO.Examples.Fbx
{
    public static class FbxSkinningExample
    {
        public static void ExportSkinnedRectangle(string outputPath, bool binary=true)
        {
            Scene scene = new Scene { Name = "SkinnedRectScene" };

            // 1. Create Skeleton
            // Skeleton Root (often a Null node in FBX)
            Bone skeletonRoot = new Bone("Armature") { IsSkeletonRoot = true }; // Will be Model type "Skeleton"
            skeletonRoot.Transform.Translation = new XYZ(0, 0, 0);
            scene.RootNode.Nodes.Add(skeletonRoot);

            // Bone 1
            Bone bone1 = new Bone("Bone1") { Length = 1.0, IsSkeletonRoot = false }; // Model type "LimbNode"
            bone1.Transform.Translation = new XYZ(0, 0, 0); // Relative to skeletonRoot
            skeletonRoot.Nodes.Add(bone1);

            // Bone 2
            Bone bone2 = new Bone("Bone2") { Length = 1.0, IsSkeletonRoot = false }; // Model type "LimbNode"
            bone2.Transform.Translation = new XYZ(0, 1, 0); // Relative to bone1 (1 unit up in Y)
            bone1.Nodes.Add(bone2);

            // Force ID generation
            scene.RootNode.GetIdOrDefault();
            skeletonRoot.GetIdOrDefault();
            bone1.GetIdOrDefault();
            bone2.GetIdOrDefault();


            // 2. Create Mesh Node
            Node meshNode = new Node { Name = "SkinnedRectangleNode" };
            meshNode.Transform.Translation = new XYZ(0, 0, 0); // Mesh at origin
            scene.RootNode.Nodes.Add(meshNode);
            meshNode.GetIdOrDefault();

            // 3. Create Mesh
            Mesh rectMesh = new Mesh { Name = "RectangleMesh" };
            meshNode.Entities.Add(rectMesh); // Add mesh to node's entities
            rectMesh.GetIdOrDefault();

            // Vertices for a 1x2 rectangle (width 1, height 2, centered on Y-axis, on XY plane)
            //   4--5
            //   |  |
            //   2--3  (y=0, mid-point for skinning)
            //   |  |
            //   0--1  (y=-1)
            rectMesh.Vertices.AddRange(new List<XYZ>
            {
                new XYZ(-0.5, -1.0, 0), // 0
                new XYZ( 0.5, -1.0, 0), // 1
                new XYZ(-0.5,  0.0, 0), // 2
                new XYZ( 0.5,  0.0, 0), // 3
                new XYZ(-0.5,  1.0, 0), // 4
                new XYZ( 0.5,  1.0, 0)  // 5
            });

            // Faces (2 quads -> 4 triangles)
            rectMesh.Polygons.AddRange(new List<Triangle>
            {
                new Triangle(0, 1, 3), new Triangle(0, 3, 2), // Bottom quad
                new Triangle(2, 3, 5), new Triangle(2, 5, 4)  // Top quad
            });

            // Add a simple material to the mesh node
            var defaultMaterial = new MeshIO.Shaders.Material { Name = "DefaultMat", DiffuseColor = new Color(128, 128, 128) };
            meshNode.Materials.Add(defaultMaterial);
            defaultMaterial.GetIdOrDefault();

            // Add LayerElementMaterial to mesh
            var materialLayer = new LayerElementMaterial
            {
                Name = "RectMatLayer",
                MappingMode = MappingMode.AllSame, // All faces use the first material on the node
                ReferenceMode = ReferenceMode.IndexToDirect
            };
            materialLayer.Indexes.Add(0); // All polygons use material index 0 from meshNode.Materials
            rectMesh.Layers.Add(materialLayer);


            // 4. Create Skin Deformer
            Skin skin = new Skin { Name = "RectangleSkin" };
            meshNode.Entities.Add(skin); // Add skin to node's entities
            skin.GetIdOrDefault();
            skin.DeformedGeometry = rectMesh; // Link skin to the mesh

            // 5. Define Bind Pose Matrices
            // Assuming skeletonRoot, bone1, and meshNode are at world origin for bind pose.
            // bone2 is at (0,1,0) world for bind pose.
            Matrix4 meshBindMatrix = meshNode.Transform.Matrix; // Identity if at origin
            Matrix4 bone1BindMatrix = bone1.Transform.Matrix; // Identity if local (0,0,0) and parent at origin
            Matrix4 bone2BindMatrix = Matrix4.CreateTranslation(new XYZ(0, 1, 0)); // Global transform of bone2

            // 6. Create Clusters
            // Cluster for Bone1
            Cluster cluster1 = new Cluster { Name = "Cluster_Bone1", Link = bone1 };
            cluster1.GetIdOrDefault();
            cluster1.Indexes.AddRange(new int[] { 0, 1, 2, 3 }); // Vertices 0,1 (full), 2,3 (partial)
            cluster1.Weights.AddRange(new double[] { 1.0, 1.0, 0.5, 0.5 });
            cluster1.TransformMatrix = bone1BindMatrix; // Bone's world transform at bind
            cluster1.TransformLinkMatrix = meshBindMatrix; // Mesh's world transform at bind
            skin.Clusters.Add(cluster1);

            // Cluster for Bone2
            Cluster cluster2 = new Cluster { Name = "Cluster_Bone2", Link = bone2 };
            cluster2.GetIdOrDefault();
            cluster2.Indexes.AddRange(new int[] { 2, 3, 4, 5 }); // Vertices 2,3 (partial), 4,5 (full)
            cluster2.Weights.AddRange(new double[] { 0.5, 0.5, 1.0, 1.0 });
            cluster2.TransformMatrix = bone2BindMatrix; // Bone's world transform at bind
            cluster2.TransformLinkMatrix = meshBindMatrix; // Mesh's world transform at bind
            skin.Clusters.Add(cluster2);

            // 7. FBX Writer Options
            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = binary, // Or false for ASCII for debugging
                Version = FbxVersion.v7400
            };

            // 8. Write to File
            try
            {
                Console.WriteLine($"Exporting skinned rectangle to: {outputPath}");
                FbxWriter.Write(outputPath, scene, options, NotificationHelper.LogConsoleNotification);
                Console.WriteLine("Export complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during FBX export: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static void RunExample()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "SkinnedRectangle.fbx");
            ExportSkinnedRectangle(filePath);

            Console.WriteLine($"\nAttempting to read back {filePath} (FBX structure parse):");
            try
            {
                using (FbxReader reader = new FbxReader(filePath))
                {
                    reader.OnNotification += NotificationHelper.LogConsoleNotification;
                    FbxRootNode rootNodeData = reader.Parse(); // Parses the raw FBX node structure
                    Console.WriteLine($"Successfully parsed raw FBX. Version: {rootNodeData.Version}");
                    // Optionally, try to fully read into MeshIO Scene, though reader part for skinning is not implemented here.
                    // Scene sceneRead = reader.Read();
                    // Console.WriteLine($"Successfully read into Scene. Root node name: {sceneRead.RootNode.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading back FBX: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}