using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// In a new file, e.g., Models/JsonBoneModels.cs

namespace MeshIO.Examples.Fbx
{
    internal class FbxFsBoneJsonConvert
    {
    }

    public class JsonVector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class JsonBone
    {
        public string Name { get; set; }
        public int ParentIndex { get; set; }
        public int FirstChildIndex { get; set; } // We'll primarily use ParentIndex for hierarchy
        public int NextSiblingIndex { get; set; } // Also less critical if using ParentIndex
        public JsonVector3 Translation { get; set; }
        public JsonVector3 Rotation { get; set; } // Assuming these are Euler angles
        public JsonVector3 Scale { get; set; }
        // BoundingBoxMin, BoundingBoxMax, Flags are ignored for now for bone hierarchy
    }
}