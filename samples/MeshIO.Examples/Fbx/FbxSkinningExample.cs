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
// using MeshIO.Utils; // Matrix4Extensions might not be needed if using CSMath directly or if MeshIO handles it

namespace MeshIO.Examples.Fbx
{
    public static class FbxSkinningExample
    {
        public static void ExportSkinnedRectangle(string outputPath, bool binary = true)
        {
            Scene scene = new Scene { Name = "SkinnedRectScene" };

            // 1. Create Skeleton
            Bone skeletonRoot = new Bone("Armature") { IsSkeletonRoot = true };
            skeletonRoot.Transform.Translation = new XYZ(0, 0, 0);
            scene.RootNode.AddChildNode(skeletonRoot); // Use AddNode for consistency

            Bone bone1 = new Bone("Bone1") { Length = 1.0 }; // IsSkeletonRoot defaults to false
            bone1.Transform.Translation = new XYZ(0, 0.5, 0);
            skeletonRoot.AddChildNode(bone1);

            Bone bone2 = new Bone("Bone2") { Length = 1.0 };
            bone2.Transform.Translation = new XYZ(0, 0.5, 0);
            bone1.AddChildNode(bone2);

            // Force ID generation (MeshIO should handle this, but good for explicitness)
            scene.RootNode.GetIdOrDefault();
            skeletonRoot.GetIdOrDefault();
            bone1.GetIdOrDefault();
            bone2.GetIdOrDefault();

            // 2. Create Mesh and its Node
            // In MeshIO, a Mesh is an Entity that can be attached to a Node.
            // The FbxWriter should interpret a Node with a Mesh entity as a "Model" of type "Mesh".
            Mesh rectMesh = new Mesh { Name = "RectangleMesh" };
            rectMesh.GetIdOrDefault(); // Assign ID to the mesh itself

            // Vertices
            rectMesh.Vertices.AddRange(new List<XYZ>
            {
                new XYZ(-0.5, -1.0, 0), new XYZ(0.5, -1.0, 0),
                new XYZ(-0.5,  0.0, 0), new XYZ(0.5,  0.0, 0),
                new XYZ(-0.5,  1.0, 0), new XYZ(0.5,  1.0, 0)
            });

            // Faces
            rectMesh.Polygons.AddRange(new List<Triangle>
            {
                new Triangle(0, 1, 3), new Triangle(0, 3, 2),
                new Triangle(2, 3, 5), new Triangle(2, 5, 4)
            });

            // Create the Node that will hold the mesh geometry and skinning info
            Node meshNode = new Node { Name = "SkinnedRectangleNode" };

            meshNode.Transform.Translation = new XYZ(0, 0, 0); // Mesh at origin
            //scene.RootNode.Nodes.Add(meshNode);
            //bone1.Nodes.Add(meshNode);
            skeletonRoot.AddChildNode(meshNode);
            meshNode.GetIdOrDefault();

            // Add the Mesh as the primary geometry for this Node
            // MeshIO's FbxWriter should use this to create a Model of type "Mesh"
            // and place geometry data directly within that Model block.
            meshNode.Entities.Add(rectMesh); // This is key

            // Add a simple material to the mesh node
            var defaultMaterial = new MeshIO.Shaders.Material { Name = "DefaultMat", DiffuseColor = new Color(128, 128, 128) };
            meshNode.Materials.Add(defaultMaterial); // Material attached to the Node
            defaultMaterial.GetIdOrDefault();

            // Add LayerElementMaterial to mesh
            var materialLayer = new LayerElementMaterial
            {
                Name = "RectMatLayer",
                MappingMode = MappingMode.AllSame,
                ReferenceMode = ReferenceMode.IndexToDirect
            };
            materialLayer.Indexes.Add(0); // All polygons use material index 0 from meshNode.Materials
            rectMesh.Layers.Add(materialLayer);


            // 4. Create Skin Deformer
            Skin skin = new Skin { Name = "RectangleSkin" };
            // The Skin deformer should also be an Entity of the meshNode
            meshNode.Entities.Add(skin);
            skin.GetIdOrDefault();
            skin.DeformedGeometry = rectMesh; // Link skin to the specific mesh geometry

            // 5. Define Bind Pose Matrices
            // These matrices define the state of bones and the mesh at the moment of binding.
            // For FBX, Cluster.TransformMatrix is the world transform of the bone at bind time.
            // Cluster.TransformLinkMatrix is the world transform of the mesh (Geometry/Model) at bind time.

            // Calculate global bind matrices (important if your hierarchy isn't flat at origin)
            Matrix4 skeletonRootBindGlobalMatrix = skeletonRoot.GetGlobalMatrix(scene.RootNode);
            Matrix4 bone1BindGlobalMatrix = bone1.GetGlobalMatrix(scene.RootNode);
            Matrix4 bone2BindGlobalMatrix = bone2.GetGlobalMatrix(scene.RootNode);
            Matrix4 meshNodeBindGlobalMatrix = meshNode.GetGlobalMatrix(scene.RootNode);


            // 6. Create Clusters
            // Cluster for Bone1
            Cluster cluster1 = new Cluster { Name = "Cluster_Bone1", Link = bone1 };
            cluster1.GetIdOrDefault();
            cluster1.Indexes.AddRange(new int[] { 0, 1, 2, 3 });
            cluster1.Weights.AddRange(new double[] { 1.0, 1.0, 0.5, 0.5 });
            // TransformMatrix: The world transformation of the bone (Link) at the time of binding.
            cluster1.TransformMatrix = bone1BindGlobalMatrix;
            // TransformLinkMatrix: The world transformation of the
            //                      Geometry (DeformedGeometry) that this cluster deforms
            //                      at the time of binding.
            cluster1.TransformLinkMatrix = meshNodeBindGlobalMatrix;
            skin.Clusters.Add(cluster1);

            // Cluster for Bone2
            Cluster cluster2 = new Cluster { Name = "Cluster_Bone2", Link = bone2 };
            cluster2.GetIdOrDefault();
            cluster2.Indexes.AddRange(new int[] { 2, 3, 4, 5 });
            cluster2.Weights.AddRange(new double[] { 0.5, 0.5, 1.0, 1.0 });
            cluster2.TransformMatrix = bone2BindGlobalMatrix;
            cluster2.TransformLinkMatrix = meshNodeBindGlobalMatrix;
            skin.Clusters.Add(cluster2);

            // 7. Add Bind Pose Information to the Scene
            // This is crucial for Blender to correctly interpret the setup.
            // The Pose object in FBX stores the bind pose transformations for nodes.
            // MeshIO might handle this implicitly if a Skin deformer is present and linked,
            // but it's good to be aware of. We'll rely on MeshIO's FbxWriter to create
            // the `Pose` block correctly if it detects skinning.
            // If MeshIO doesn't automatically create a "BindPose" `Pose` object including
            // the meshNode, this might still be an issue.
            //
            // Let's assume MeshIO's FbxWriter will create the Pose block based on the
            // Skin and Cluster information. If not, MeshIO's API would need a way to
            // explicitly define a scene-level BindPose.

            // 8. FBX Writer Options
            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = binary,
                Version = FbxVersion.v7400, // Keep this or try v7500 if issues persist
                // Potentially add an option here if MeshIO supports it, e.g., to enforce
                // specific node type mappings or bind pose generation.
            };

            // 9. Write to File
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
            string filePath = Path.Combine(desktopPath, "SkinnedRectangle_MeshIO_v2.fbx"); // New name for testing
            ExportSkinnedRectangle(filePath, false); // Export as ASCII for easy inspection

            Console.WriteLine($"\nAttempting to read back {filePath} (FBX structure parse):");
            try
            {
                using (FbxReader reader = new FbxReader(filePath))
                {
                    reader.OnNotification += NotificationHelper.LogConsoleNotification;
                    FbxRootNode rootNodeData = reader.Parse();
                    Console.WriteLine($"Successfully parsed raw FBX. Version: {rootNodeData.Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading back FBX: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // Helper extension method (if not already in MeshIO.Utils or similar)
    // to get global matrix. Ensure your CSMath.Matrix4 has appropriate multiplication.
    public static class TransformExtensions
    {
        public static Matrix4 GetGlobalMatrix(this Node node, Node rootSceneNode)
        {
            Matrix4 globalMatrix = node.Transform.Matrix; // Local matrix
            var parent = node.Parent; // Assuming Transform has an Owner (Node) property
            //
            while (parent != null && parent != rootSceneNode && parent is Node) // Go up until the scene root or null
            {
                var parent_node = parent as Node;
                globalMatrix = parent_node.Transform.Matrix * globalMatrix; // Pre-multiply by parent's local matrix
                parent = parent_node.Parent;
            }
            return globalMatrix;
        }
    }
}