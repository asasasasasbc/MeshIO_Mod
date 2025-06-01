using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// MeshIO/Entities/Bone.cs
namespace MeshIO.Entities
{
    /// <summary>
    /// Represents a bone in a skeleton hierarchy.
    /// Bones are nodes with specific properties relevant to skinning and animation.
    /// </summary>
    public class Bone : Node
    {
        /// <summary>
        /// Optional length of the bone.
        /// In many systems, bone length is implicitly defined by the distance to its child or a specific end point.
        /// FBX uses a "Size" property for LimbNode type, which can represent this.
        /// </summary>
        public double? Length { get; set; }

        /// <summary>
        /// Indicates if this bone should be treated as the root of a skeleton hierarchy in FBX.
        /// If true, its FBX Model type will be "Skeleton"; otherwise, "LimbNode".
        /// </summary>
        public bool IsSkeletonRoot { get; set; } = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Bone() : base() { }

        /// <summary>
        /// Constructor with a name.
        /// </summary>
        /// <param name="name">The name of the bone.</param>
        public Bone(string name) : base(name) { }
    }
}