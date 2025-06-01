// MeshIO.FBX/Connections/FbxConnection.cs
using MeshIO.FBX.Templates;
using System; // For ArgumentException

namespace MeshIO.FBX.Connections
{
    internal class FbxConnection
    {
        public FbxConnectionType ConnectionType { get; }
        public string PropertyName { get; } // For OP connections, null otherwise

        public string ParentId { get; } // In FBX "C" connections, this is the 3rd element (destination)
        public string ChildId { get; }  // In FBX "C" connections, this is the 2nd element (source)

        public IFbxObjectTemplate Child { get; }
        public IFbxObjectTemplate Parent { get; }

        // Constructor for writing: Child -> Parent with a specific type
        public FbxConnection(IFbxObjectTemplate child, IFbxObjectTemplate parent, FbxConnectionType type, string propertyName = null)
        {
            Child = child;
            Parent = parent;
            ChildId = child?.Id; // Can be null if parent is scene root (0)
            ParentId = parent?.Id;
            ConnectionType = type;
            PropertyName = propertyName;
        }

        // Constructor for reading FBX (simplified for now)
        public FbxConnection(FbxConnectionType type, string childId, string parentId, string propertyName = null)
        {
            ConnectionType = type;
            ChildId = childId;
            ParentId = parentId;
            PropertyName = propertyName;
        }

        public string GetComment()
        {
            string childName = Child?.Name ?? $"ID:{ChildId}";
            string parentName = Parent?.Name ?? $"ID:{ParentId}";
            string childType = Child?.FbxObjectName ?? "Unknown";
            string parentType = Parent?.FbxObjectName ?? "Unknown";

            return $"{childType}::{childName} -> {parentType}::{parentName} ({ConnectionType})";
        }

        public static FbxConnectionType Parse(string typeString)
        {
            switch (typeString)
            {
                case "OO": return FbxConnectionType.ObjectObject;
                case "OP": return FbxConnectionType.ObjectProperty;
                case "Deformer": return FbxConnectionType.Deformer;
                case "SubDeformer": return FbxConnectionType.SubDeformer;
                // Add PO, PP if needed later
                default:
                    // Console.WriteLine($"Warning: Unknown FBX connection type string: {typeString}. Defaulting to ObjectObject.");
                    // throw new System.ArgumentException($"Unknown Fbx connection type string: {typeString}", nameof(typeString));
                    return FbxConnectionType.ObjectObject; // Fallback for safety during reading if encountering unknown types
            }
        }

        public string GetTypeString()
        {
            switch (ConnectionType)
            {
                case FbxConnectionType.ObjectObject: return "OO";
                case FbxConnectionType.ObjectProperty: return "OP";
                case FbxConnectionType.Deformer: return "Deformer";
                case FbxConnectionType.SubDeformer: return "SubDeformer";
                default:
                    throw new InvalidOperationException($"Unsupported FbxConnectionType enum value: {ConnectionType}");
            }
        }
    }
}