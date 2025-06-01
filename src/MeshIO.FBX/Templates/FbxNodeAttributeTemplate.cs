// MeshIO.FBX/Templates/FbxNodeAttributeTemplate.cs
using MeshIO.FBX.Writers;
using MeshIO.FBX.Readers; // For Build if needed

namespace MeshIO.FBX.Templates
{
    internal class FbxNodeAttributeTemplate : IFbxObjectTemplate
    {
        public string Id { get; }
        public string Name { get; } // e.g., "NodeAttribute::Bone1"
        public string FbxObjectName => "NodeAttribute";
        public string FbxTypeName { get; } // "Skeleton" or "LimbNode" for bones
        public Element3D LinkedElement { get; } // The Bone model this attribute is for

        // Constructor for writing
        public FbxNodeAttributeTemplate(Element3D linkedElement, string fbxTypeNameForAttribute)
        {
            this.LinkedElement = linkedElement;
            // Ensure unique ID for the NodeAttribute itself
            this.Id = IdUtils.CreateId().ToString();
            this.Name = $"NodeAttribute::{linkedElement.Name}";
            this.FbxTypeName = fbxTypeNameForAttribute; // Same as the Model's subtype usually
        }

        public Element3D GetElement() => null; // It's an auxiliary FBX construct

        public FbxNode ToFbxNode(FbxFileWriterBase writer)
        {
            FbxNode n = new FbxNode(FbxObjectName, long.Parse(Id), Name, FbxTypeName);
            // Common property for skeleton-related NodeAttributes
            n.Add("TypeFlags", "Skeleton");
            // NodeAttributes typically don't have a Properties70 block
            return n;
        }

        public void ProcessChildren(FbxFileWriterBase fwriter) { /* No children */ }

        public void ApplyTemplate(FbxPropertyTemplate template) { /* Not typically used for NodeAttribute */ }

        public void Build(FbxFileBuilderBase builder) { /* TODO: Implement for reading if needed */ }
    }
}