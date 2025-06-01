using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// MeshIO.Examples/Fbx/FbxSkeletonExportExample.cs
using MeshIO;
using MeshIO.FBX;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
// No shaders needed for this basic skeleton test
using CSMath;
using System.IO;
using MeshIO.Examples.Common; // For NotificationHelper

namespace MeshIO.Examples.Fbx
{
    public static class FbxSkeletonExportExample
    {
        public static void ExportSimpleArmature(string outputPath, bool binary=true)
        {
            Scene scene = new Scene { Name = "SimpleArmatureScene" };
            Console.WriteLine("Exporting armature..."); // Ensure ID
            // Create a root node for the armature (optional, but good practice for organization)
            Node armatureNode = new Node { Name = "Armature" };
            scene.RootNode.Nodes.Add(armatureNode);
            Console.WriteLine(armatureNode.GetIdOrDefault()); // Ensure ID

            // Create Bones
            Bone rootBone = new Bone("RootBone")
            {
                Length = 1.0,
                IsSkeletonRoot = true // Mark as skeleton root for FBX type "Skeleton"
            };
            Console.WriteLine(rootBone.GetIdOrDefault()); // Ensure ID
            // rootBone.Transform.Translation = new XYZ(0, 0, 0); // Default is fine

            Bone childBone = new Bone("ChildBone")
            {
                Length = 0.8,
                IsSkeletonRoot = false // This is a limb node, FBX type "LimbNode"
            };
            Console.WriteLine(childBone.GetIdOrDefault()); // Ensure ID

            Bone childBone2 = new Bone("ChildBone2")
            {
                Length = 0.8,
                IsSkeletonRoot = false // This is a limb node, FBX type "LimbNode"
            };
            Console.WriteLine(childBone2.GetIdOrDefault()); // Ensure ID
            // Position childBone relative to rootBone.
            // If rootBone is 1.0 unit long (e.g., along the Y-axis in its local space):
            childBone.Transform.Translation = new XYZ(0, 1.0, 0); // Places the start of childBone at the end of rootBone

            // Establish hierarchy:
            // Armature (Node)
            //  `- RootBone (Bone, SkeletonType)
            //      `- ChildBone (Bone, LimbNode)
            //childBone.Nodes.Add(childBone2);
            rootBone.Nodes.Add(childBone);    // childBone is a child of rootBone

            armatureNode.Nodes.Add(rootBone); // rootBone is a child of armatureNode

            // Create a simple cuboid mesh. It won't be skinned yet, but it's good to have some geometry.
            // The cuboid will be 0.4 wide/deep, and 2.0 units tall.
            // Centered at origin, it will span from Y= -1.0 to Y = 1.0 if not transformed.
            // Let's make it sit along the bones. Base at Y=0, height 1.8 (matching bone lengths).
            Mesh cuboidMesh = new Mesh { Name = "MyCuboid" };
            double halfWidth = 0.1;
            double height = 1.8; // Total length of rootBone + childBone

            cuboidMesh.Vertices.AddRange(new List<XYZ>
            {
                new XYZ(-halfWidth, 0,      -halfWidth), // 0 Front-Bottom-Left
                new XYZ( halfWidth, 0,      -halfWidth), // 1 Front-Bottom-Right
                new XYZ( halfWidth, height, -halfWidth), // 2 Front-Top-Right
                new XYZ(-halfWidth, height, -halfWidth), // 3 Front-Top-Left
                new XYZ(-halfWidth, 0,       halfWidth), // 4 Back-Bottom-Left
                new XYZ( halfWidth, 0,       halfWidth), // 5 Back-Bottom-Right
                new XYZ( halfWidth, height,  halfWidth), // 6 Back-Top-Right
                new XYZ(-halfWidth, height,  halfWidth)  // 7 Back-Top-Left
            });
            cuboidMesh.Polygons.AddRange(new List<Triangle>
            {
                new Triangle(0, 1, 2), new Triangle(0, 2, 3), // Front face
                new Triangle(1, 5, 6), new Triangle(1, 6, 2), // Right face
                new Triangle(5, 4, 7), new Triangle(5, 7, 6), // Back face
                new Triangle(4, 0, 3), new Triangle(4, 3, 7), // Left face
                new Triangle(3, 2, 6), new Triangle(3, 6, 7), // Top face
                new Triangle(0, 4, 5), new Triangle(0, 5, 1)  // Bottom face
            });

            Node meshNode = new Node("CuboidNode");
            meshNode.Entities.Add(cuboidMesh);
            scene.RootNode.Nodes.Add(meshNode); // Add mesh node to the scene's root

            // Writer options
            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = binary, // Binary is more common
                Version = FbxVersion.v7400 // A widely compatible modern version
            };

            try
            {
                Console.WriteLine($"Exporting simple armature and cuboid to: {outputPath}");
                FbxWriter.Write(outputPath, scene, options, NotificationHelper.LogConsoleNotification);
                Console.WriteLine("Armature and cuboid export complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during FBX export: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // You would call this from your main program or a test runner.
        // Example:
        // public static void Main(string[] args)
        // {
        //     string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //     string filePath = Path.Combine(desktopPath, "SimpleArmatureCuboid.fbx");
        //     FbxSkeletonExportExample.ExportSimpleArmature(filePath);
        //     Console.WriteLine($"\nTo verify, import '{filePath}' into Blender or another 3D modeling software.");
        // }
    }
}