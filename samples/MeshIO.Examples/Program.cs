using System;
using System.IO;
using MeshIO.Examples.Fbx;
using MeshIO.Examples.Common;
namespace MeshIO.Examples
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("HelloWorld!");
			//FbxWriterExample.RunExample();
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "SimpleSkeletonExportV3.3.fbx");
            string filePath_ascii = Path.Combine(desktopPath, "SimpleSkeletonExportV3.3_ascii.fbx");
            //FbxSkeletonExportExample.ExportSimpleArmature(filePath, true);
            //FbxSkeletonExportExample.ExportSimpleArmature(filePath_ascii, false);

            jsonBoneConvert();
        }
        static void exportSkeleton(string in_path, string out_path) {
            FbxJsonSkeletonExporter.ExportSkeletonFromJson(in_path, out_path, true);
            FbxJsonSkeletonExporter.ExportSkeletonFromJson(in_path, out_path + ".ascii.fbx", false);
        }

		static void jsonBoneConvert() {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string jsonInputPath = "D:\\MeshIO_Mod\\src\\CollectCode\\BoneExampleDuch.json"; // Place your JSON here
            string fbxOutputPath = Path.Combine(desktopPath, "JsonImportedSkeleton.fbx");

            // Create a dummy skeleton_data.json on your desktop with the content you provided.
            // Example: File.WriteAllText(jsonInputPath, "[{\"Name\":\"L_Forearm\", ... rest of your json ...}]");
            // Ensure the JSON file actually exists at jsonInputPath before running.

            if (File.Exists(jsonInputPath))
            {
                RotationOrder[] allOrders = (RotationOrder[])Enum.GetValues(typeof(RotationOrder));
                exportSkeleton(jsonInputPath, fbxOutputPath);
                //for (int i = 0; i < allOrders.Length; i++) {
                //    for (int j = 0; j < allOrders.Length; j++) {
                //        FbxJsonSkeletonExporter.source_ro = allOrders[i];
                //        FbxJsonSkeletonExporter.target_ro = allOrders[j];
                //        exportSkeleton(jsonInputPath, fbxOutputPath + i as string + j as string +".fbx");
                //    }
                //}

            }
            else
            {
                Console.WriteLine($"JSON input file not found: {jsonInputPath}");
                Console.WriteLine("Please create the JSON file with your skeleton data.");
                // You can write the sample JSON to the file here for testing if needed:
                // string sampleJsonContent = @"[{""Name"":""L_Forearm"", ...}]"; // (truncated for brevity)
                // File.WriteAllText(jsonInputPath, sampleJsonContent);
                // Console.WriteLine("Created a sample JSON. Please re-run or populate with full data.");
            }
        }
	}
}
