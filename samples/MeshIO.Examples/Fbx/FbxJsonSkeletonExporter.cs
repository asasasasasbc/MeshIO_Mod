// In a new file, e.g., FbxJsonSkeletonExporter.cs
using MeshIO;
using MeshIO.Entities;
using MeshIO.FBX;
using CSMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshIO.Examples.Common;     // For NotificationHelper

namespace MeshIO.Examples.Fbx
{
    public static class FbxJsonSkeletonExporter
    {
        // Helper to convert JSON rotation (assuming radians) to degrees for FBX
        //TODO： convert to XYZ rotation order
        private static XYZ JsonRotationToFbxEulerDegrees(JsonVector3 jsonRotation)
        {
            // IMPORTANT ASSUMPTION: JSON rotations are Euler angles in RADIANS.
            // If they are already in degrees, remove the RadToDeg conversion.
            // If they are quaternion components, this needs a different conversion.
            float x = (float)MathUtils.RadToDeg(jsonRotation.X);
            float y = (float)MathUtils.RadToDeg(jsonRotation.Y);
            float z = (float)MathUtils.RadToDeg(jsonRotation.Z);
            MyVector3 inputAngles = new MyVector3(x,y, z); // Example: 30 deg X, 60 deg Y, 90 deg Z
            RotationOrder source = source_ro;// RotationOrder.YZX;
            RotationOrder target = target_ro;// RotationOrder.XYZ;

            var convertedAngles = EulerAngleConverter.ConvertRotationOrder(inputAngles, source, target);

            // Have to mirror Z axis due to  Right-hand left-hand issue
            // For rotation, mirror X and Y
            return new XYZ(
                -1 * (convertedAngles.X),
                -1 * (convertedAngles.Y),
                (convertedAngles.Z)
            );
        }

        public static int rotationOrder = 0; // 0 - 5

        public static RotationOrder source_ro = RotationOrder.YZX;
        public static RotationOrder target_ro = RotationOrder.ZYX;
        // FBX RotationOrder enum: 0:XYZ, 1:XZY, 2:YXZ, 3:YZX, 4:ZXY, 5:ZYX
        // Assuming your source data implies a default rotation order if not specified.
        // Let's use YZX as a default to match your C# snippet's default.


        public static void ExportSkeletonFromJson(string jsonFilePath, string outputPathFbx, bool binary=true, int rotOrder=3)
        {
            Console.WriteLine($"Reading skeleton from JSON: {jsonFilePath}");
            string jsonData = File.ReadAllText(jsonFilePath);
            List<JsonBone> jsonBones = JsonConvert.DeserializeObject<List<JsonBone>>(jsonData);

            if (jsonBones == null || !jsonBones.Any())
            {
                Console.WriteLine("No bones found in JSON or failed to deserialize.");
                return;
            }

            Scene scene = new Scene { Name = Path.GetFileNameWithoutExtension(jsonFilePath) + "_Scene" };
            
            Bone armatureRootNode = new Bone("Skeleton"); // FBX Model Type "Null"
            armatureRootNode.GetIdOrDefault();
            scene.RootNode.Nodes.Add(armatureRootNode);

            Dictionary<int, Bone> processedBones = new Dictionary<int, Bone>();
            List<Bone> rootMeshIOBones = new List<Bone>(); // Bones with ParentIndex == -1

            // First pass: Create all MeshIO.Bone objects and set their local transforms
            for (int i = 0; i < jsonBones.Count; i++)
            {
                JsonBone jBone = jsonBones[i];
                Bone mioBone = new Bone(jBone.Name);
                mioBone.GetIdOrDefault();

                // Mirror Z axis
                mioBone.Transform.Translation = new XYZ(jBone.Translation.X, jBone.Translation.Y, -1 * jBone.Translation.Z);

                // --- ROTATION HANDLING ---
                // Assuming jBone.Rotation X,Y,Z are Euler angles.
                // The critical question is: ARE THEY IN RADIANS OR DEGREES in the JSON?
                // Your C# snippet suggests your system uses degrees for matrix math after converting from something.
                // FBX Lcl Rotation is in DEGREES.
                // If JSON is RADIANS (common from game engines like Unity):
                mioBone.Transform.EulerRotation = JsonRotationToFbxEulerDegrees(jBone.Rotation);// JsonRotationToFbxEulerDegrees(jBone.Rotation);
                // If JSON is already DEGREES:
                // mioBone.Transform.EulerRotation = new XYZ(jBone.Rotation.X, jBone.Rotation.Y, jBone.Rotation.Z);

                mioBone.Transform.Scale = new XYZ(jBone.Scale.X, jBone.Scale.Y, jBone.Scale.Z);

                // Set RotationOrder as a custom property. FbxBoneTemplate will need to read this.
                mioBone.Properties.Add(new Property<int>("RotationOrder", 0));

                // Set IsSkeletonRoot for the first bone encountered that is a root,
                // or a specific named bone if that's the convention.
                // For simplicity, if ParentIndex is -1, and it's the first such bone, mark as skeleton root.
                //DELETED!
                //if (jBone.ParentIndex == -1 && !rootMeshIOBones.Any(b => b.IsSkeletonRoot))
                //{
                //    mioBone.IsSkeletonRoot = true;
                //}

                // Bone Length: FBX uses "Size" for LimbNode. We can infer or set a default.
                // For now, let FbxBoneTemplate handle default "Size" if mioBone.Length is null.
                // If you can calculate it (e.g., distance to first child), do it here.
                // Example: if a child exists at a certain local offset, length could be that offset's magnitude.
                // mioBone.Length = ... 

                processedBones.Add(i, mioBone);
            }

            // Second pass: Build hierarchy
            for (int i = 0; i < jsonBones.Count; i++)
            {
                JsonBone jBone = jsonBones[i];
                Bone currentMioBone = processedBones[i];

                if (jBone.ParentIndex == -1)
                {
                    armatureRootNode.Nodes.Add(currentMioBone); // Add to the main "Armature" Null node
                    rootMeshIOBones.Add(currentMioBone);
                }
                else
                {
                    if (processedBones.TryGetValue(jBone.ParentIndex, out Bone parentMioBone))
                    {
                        parentMioBone.Nodes.Add(currentMioBone);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Parent bone with index {jBone.ParentIndex} not found for bone '{jBone.Name}'. Attaching to armature root.");
                        armatureRootNode.Nodes.Add(currentMioBone);
                        rootMeshIOBones.Add(currentMioBone); // Treat as a root if parent is missing
                    }
                }
            }

            // Ensure at least one bone is marked as SkeletonRoot if any roots exist
            //if (rootMeshIOBones.Any() && !rootMeshIOBones.Any(b => b.IsSkeletonRoot))
            //{
            //    rootMeshIOBones.First().IsSkeletonRoot = false;
            //}
            armatureRootNode.IsSkeletonRoot = true;

            // FBX Writer Options
            var writerGlobalSettings = new FbxGlobalSettings(FbxVersion.v7400);
            // You might want to set Axis settings if your JSON source has a different convention (e.g. Z-up)
            // writerGlobalSettings.UpAxis = 2; // 0=X, 1=Y, 2=Z (Y is default for FBX)
            // writerGlobalSettings.FrontAxis = ...
            // writerGlobalSettings.CoordAxis = ... (0=RightHanded, 1=LeftHanded)

            FbxWriterOptions options = new FbxWriterOptions
            {
                IsBinaryFormat = binary, // Start with ASCII for easier debugging
                Version = FbxVersion.v7400,
                GlobalSettings = writerGlobalSettings
            };

            Console.WriteLine($"Exporting skeleton to FBX: {outputPathFbx}");
            try
            {
                FbxWriter.Write(outputPathFbx, scene, options, NotificationHelper.LogConsoleNotification);
                Console.WriteLine("FBX export complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during FBX export: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}