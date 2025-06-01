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

            //jsonBoneConvert();
            filePath = Path.Combine(desktopPath, "SkinRectExportV3.3.fbx");
            FbxSkinningExample.ExportSkinnedRectangle(filePath, true);
            filePath = Path.Combine(desktopPath, "SkinRectExportV3.3.ascii.fbx");
            FbxSkinningExample.ExportSkinnedRectangle(filePath, false);
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
            }
            else
            {
                Console.WriteLine($"JSON input file not found: {jsonInputPath}");
                Console.WriteLine("Please create the JSON file with your skeleton data.");
            }
        }
	}
}
