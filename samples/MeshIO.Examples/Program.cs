using System;
using System.IO;
using MeshIO.Examples.Fbx;
namespace MeshIO.Examples
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("HelloWorld!");
			//FbxWriterExample.RunExample();
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "SimpleSkeletonExportV3.2.fbx");
            string filePath_ascii = Path.Combine(desktopPath, "SimpleSkeletonExportV3.2_ascii.fbx");
            FbxSkeletonExportExample.ExportSimpleArmature(filePath, true);
            FbxSkeletonExportExample.ExportSimpleArmature(filePath_ascii, false);
        }
	}
}
