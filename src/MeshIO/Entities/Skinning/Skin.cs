// MeshIO/Entities/Skinning/Skin.cs
using System.Collections.Generic;
using MeshIO.Entities.Geometries; // For Geometry

namespace MeshIO.Entities.Skinning
{
    /// <summary>
    /// Represents an FBX Skin deformer, which groups clusters to deform a geometry.
    /// </summary>
    public class Skin : Entity
    {
        /// <summary>
        /// The geometry (Mesh) this skin deforms.
        /// This is not directly stored in FBX Skin object but connected to it.
        /// </summary>
        public Geometry DeformedGeometry { get; set; }

        /// <summary>
        /// The clusters defining the influence of each bone.
        /// These are children Deformer objects of type Cluster in FBX.
        /// </summary>
        public List<Cluster> Clusters { get; } = new List<Cluster>();

        /// <summary>
        /// Skinning type. FBX uses a string or an enum-like value.
        /// Common values: "Linear", "DualQuaternion", "Blend".
        /// In FBX properties, it's often a double: 0 for Linear, 1 for Rigid, 2 for Dual Quaternion.
        /// </summary>
        public string SkinningType { get; set; } = "Linear";

        /// <summary>
        /// Accuracy of the deformation link. Default is 50.0 in FBX.
        /// </summary>
        public double LinkDeformAccuracy { get; set; } = 50.0;

        public Skin() : base() { }
        public Skin(string name) : base(name) { }
    }
}