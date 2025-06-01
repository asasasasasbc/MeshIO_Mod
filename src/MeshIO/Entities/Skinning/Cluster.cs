// MeshIO/Entities/Skinning/Cluster.cs
using CSMath;
using System.Collections.Generic;

namespace MeshIO.Entities.Skinning
{
    /// <summary>
    /// Represents an FBX Cluster, which links a bone to a set of vertices with weights.
    /// </summary>
    public class Cluster : Entity
    {
        /// <summary>
        /// The bone (Node) this cluster deforms.
        /// </summary>
        public Node Link { get; set; }

        /// <summary>
        /// Indices of the vertices in the target geometry that this cluster influences.
        /// </summary>
        public List<int> Indexes { get; set; } = new List<int>();

        /// <summary>
        /// Weights corresponding to the vertex indices. Must have the same count as Indexes.
        /// </summary>
        public List<double> Weights { get; set; } = new List<double>();

        /// <summary>
        /// Transform of the link (bone) in global space at bind time.
        /// This is the inverse of the bone's world matrix at bind pose.
        /// FBX stores it as: MeshWorldBindPose * BoneWorldBindPoseInverse * MeshRestPoseInverse (often MeshRestPoseInverse is Identity)
        /// Or more simply, it's often the bone's world matrix when the mesh is in its bind pose.
        /// For FBX: This is the world transformation matrix of the bone (Link) at bind time.
        /// </summary>
        public Matrix4 TransformMatrix { get; set; } = Matrix4.Identity;

        /// <summary>
        /// Transform of the geometry (mesh) in global space at bind time.
        /// For FBX: This is the world transformation matrix of the mesh at bind time.
        /// </summary>
        public Matrix4 TransformLinkMatrix { get; set; } = Matrix4.Identity;

        /// <summary>
        /// Mode of the cluster, e.g., "TotalOne", "Normalized", "Additive".
        /// </summary>
        public string Mode { get; set; } = "Normalized"; // Common default

        public Cluster() : base() { }
        public Cluster(string name) : base(name) { }
    }
}