// MeshIO.FBX/Connections/FbxConnectionType.cs
using CSUtilities.Attributes; // Assuming this is for StringValue, if not, remove attribute

namespace MeshIO.FBX.Connections
{
    internal enum FbxConnectionType
    {
        [StringValue("OO")] // Object to Object (generic, hierarchical)
        ObjectObject,

        [StringValue("OP")] // Object to Property
        ObjectProperty,

        // [StringValue("PO")] // Property to Object - Less common for what we are doing
        // PropertyObject,

        // [StringValue("PP")] // Property to Property - Less common
        // PropertyProperty,

        [StringValue("Deformer")] // Specific for Skin -> Mesh connection
        Deformer,

        [StringValue("SubDeformer")] // Specific for Skin -> Cluster connection
        SubDeformer,

        // Note: For Cluster -> Bone, "OO" is common, but sometimes "Deformer" is also seen.
        // We'll stick to "OO" for Cluster->Bone as it's widely accepted.
    }
}