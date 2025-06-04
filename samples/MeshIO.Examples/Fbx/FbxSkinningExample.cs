// MeshIO.Examples/Fbx/FbxSkinningExample.cs
using MeshIO;
using MeshIO.FBX;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
using MeshIO.Entities.Geometries.Layers;
using MeshIO.Entities.Skinning;
using MeshIO.Shaders;
using CSMath;
using System;
using System.Collections.Generic;
using System.IO;
using MeshIO.Examples.Common;

namespace MeshIO.Examples.Fbx
{
    public static class FbxSkinningExample
    {
        // ... (printMatrix and unityMatrix methods remain the same) ...
        public static string printMatrix(Matrix4 m)
        {
            string ans = "";
            ans += $"{m.m00:F2} {m.m10:F2} {m.m20:F2} {m.m30:F2}\n"; // CSMath uses 1-based indexing
            ans += $"{m.m01:F2} {m.m11:F2} {m.m21:F2} {m.m31:F2}\n";
            ans += $"{m.m02:F2} {m.m12:F2} {m.m22:F2} {m.m32:F2}\n";
            ans += $"{m.m03:F2} {m.m13:F2} {m.m23:F2} {m.m33:F2}\n";
            return ans;
        }
        public static Matrix4 unityMatrix()
        { // CSMath Matrix4.Identity
            return new Matrix4(
                new double[] {
                 1.000001  ,0 ,0 ,0 ,
                 0  ,1.000001 ,0 ,0 ,
                 0  ,0 , 1.000001 ,0,
                 0  ,0      ,0    ,1.0
                }
             );
        }

        public static void ExportSkinnedRectangle(string outputPath, bool binary = true)
        {
            Scene scene = new Scene { Name = "SkinnedRectScene" };

            // 1. Create Skeleton
            Bone skeletonRoot = new Bone("Armature") { IsSkeletonRoot = true };
            skeletonRoot.Transform.Translation = new XYZ(0, 0, 0);
            scene.RootNode.AddChildNode(skeletonRoot);

            Bone bone1 = new Bone("Bone1中文") { Length = 1.0 };
            bone1.Transform.Translation = new XYZ(1, 0.0, 0);
            skeletonRoot.AddChildNode(bone1);

            Bone bone2 = new Bone("Bone2の") { Length = 1.0 };
            bone2.Transform.EulerRotation = new XYZ(45, 0, 0);
            bone2.Transform.Translation = new XYZ(0, 1.0, 0); // Relative to bone1
            bone1.AddChildNode(bone2);

            // 2. Create Mesh and its Node
            Mesh rectMesh = new Mesh { Name = "RectangleMesh" };
            rectMesh.Vertices.AddRange(new List<XYZ>
            {
                new XYZ(-0.5, 0.0, 0.0), new XYZ(0.5, 0.0, 0.0),
                new XYZ(-0.5, 1.0, 0.0), new XYZ(0.5, 1.0, 0.0),
                new XYZ(-0.5, 2.0, 0.0), new XYZ(0.5, 2.0, 0.0)
            });
            rectMesh.Polygons.AddRange(new List<Triangle>
            {
                new Triangle(0, 1, 3), new Triangle(0, 3, 2),
                new Triangle(2, 3, 5), new Triangle(2, 5, 4)
            });

            Node meshNode = new Node { Name = "SkinnedRectangleNode" };
            meshNode.Transform.Translation = new XYZ(0, 0, 0); // Mesh node at world origin LOCAL transform
            scene.RootNode.AddChildNode(meshNode); // **** CHANGED: Mesh is child of Scene Root ****
            meshNode.Entities.Add(rectMesh);

            var defaultMaterial = new MeshIO.Shaders.Material { Name = "DefaultMat", DiffuseColor = new Color(128, 128, 128) };
            meshNode.Materials.Add(defaultMaterial);
            var materialLayer = new LayerElementMaterial { Name = "RectMatLayer", MappingMode = MappingMode.AllSame, ReferenceMode = ReferenceMode.IndexToDirect };
            materialLayer.Indexes.Add(0);
            rectMesh.Layers.Add(materialLayer);
            
            Skin skin = new Skin { Name = "RectangleSkin" };
            meshNode.Entities.Add(skin);
            skin.DeformedGeometry = rectMesh;

            // 5. Define Bind Pose Matrices (World Space)
            //Matrix4 bone1BindGlobalMatrix = bone1.GetGlobalMatrix(scene.RootNode); // World: Ty=1
            //Matrix4 bone2BindGlobalMatrix = bone2.GetGlobalMatrix(scene.RootNode); // World: Ty=2
            //Matrix4 meshNodeBindGlobalMatrix = meshNode.GetGlobalMatrix(scene.RootNode); // World: Identity (since it's child of root and local transform is identity)

            // 计算BindPose下的全局矩阵
            Matrix4 meshNodeBindGlobalMatrix = meshNode.GetGlobalMatrix(scene.RootNode);
            Matrix4 bone1BindGlobalMatrix = bone1.GetGlobalMatrix(scene.RootNode);
            Matrix4 bone2BindGlobalMatrix = bone2.GetGlobalMatrix(scene.RootNode);

            Console.WriteLine("meshNodeBindGlobalMatrix (should be Identity):\n" + printMatrix(meshNodeBindGlobalMatrix));

            Console.WriteLine("bone1BindGlobalMatrix (should be Ty=1):\n" + printMatrix(bone1BindGlobalMatrix));
            Console.WriteLine("bone2BindGlobalMatrix (should be Ty=2):\n" + printMatrix(bone2BindGlobalMatrix));
            

            // 6. Create Clusters
            Cluster cluster1 = new Cluster { Name = "Cluster_Bone1", Link = bone1 };
            cluster1.Indexes.AddRange(new int[] { 0, 1, 2, 3 });
            cluster1.Weights.AddRange(new double[] { 1.0, 1.0, 0.5, 0.5 });
            Matrix4 tmp = meshNodeBindGlobalMatrix;
            CSMath.Matrix4.Inverse(bone1BindGlobalMatrix, out tmp); // Added for Linked transform matrix
            cluster1.TransformMatrix = tmp;     // Should be Identity
            cluster1.TransformLinkMatrix = bone1BindGlobalMatrix;
            skin.Clusters.Add(cluster1);

            Cluster cluster2 = new Cluster { Name = "Cluster_Bone2", Link = bone2 };
            cluster2.Indexes.AddRange(new int[] { 2, 3, 4, 5 });
            cluster2.Weights.AddRange(new double[] { 0.5, 0.5, 1.0, 1.0 });
            CSMath.Matrix4.Inverse(bone2BindGlobalMatrix, out tmp); // Added for Linked transform matrix
            cluster2.TransformMatrix = tmp;     // Should be Identity
            cluster2.TransformLinkMatrix = bone2BindGlobalMatrix;
            skin.Clusters.Add(cluster2);
            
            //New
           

            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = binary,
                Version = FbxVersion.v7400,
            };

            try
            {
                Console.WriteLine($"Exporting skinned rectangle to: {outputPath}");
                scene.GetIdOrDefault(); // Ensure all IDs are set
                FbxWriter.Write(outputPath, scene, options, NotificationHelper.LogConsoleNotification);
                Console.WriteLine("Export complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during FBX export: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void RunExample()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "my_skinned_rect_v3.fbx");
            ExportSkinnedRectangle(filePath, false);

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

    public static class NodeExtensions // Renamed for clarity
    {
        public static Matrix4 GetMatrix4(Transform t)
        {
            var pos = t.Translation;
            var rot = t.EulerRotation;

            return t.Matrix;
        }
        public static Matrix4 GetGlobalMatrix(this Node node, Node stopAtParent = null)
        {
            if (node == null) return Matrix4.Identity;

            Matrix4 globalMatrix = GetMatrix4(node.Transform); // Start with the node's local matrix
            Node currentParent = (Node)node.Parent;

            while (currentParent != null && currentParent != stopAtParent)
            {
                globalMatrix = GetMatrix4(currentParent.Transform) * globalMatrix; // Pre-multiply by parent's local matrix
                currentParent = (Node)currentParent.Parent;
            }
            return globalMatrix;
        }
    }
}