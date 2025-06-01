// MeshIO.FBX/Templates/FbxSkinTemplate.cs
using MeshIO.Entities.Skinning;
using MeshIO.FBX.Writers;
using MeshIO.FBX.Readers;

namespace MeshIO.FBX.Templates
{
    internal class FbxSkinTemplate : FbxObjectTemplate<Skin>
    {
        public override string FbxObjectName { get { return FbxFileToken.Deformer; } }
        public override string FbxTypeName { get { return FbxFileToken.SkinType; } }

        public FbxSkinTemplate(Skin skin) : base(skin) { }
        public FbxSkinTemplate(FbxNode node, Skin skin) : base(node, skin) { }


        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 101); // Common version for Skin deformer
            node.Add("Link_DeformAcuracy", _element.LinkDeformAccuracy);

            // SkinningType in FBX properties is a double: 0 for Linear, 1 for Rigid, 2 for Dual Quaternion.
            double skinningTypeValue = 0; // Default to Linear
            if (_element.SkinningType.Equals("Rigid", System.StringComparison.OrdinalIgnoreCase))
                skinningTypeValue = 1;
            else if (_element.SkinningType.Equals("DualQuaternion", System.StringComparison.OrdinalIgnoreCase))
                skinningTypeValue = 2;

            // The actual property name in Properties70 for SkinningType might vary or be implicit.
            // Often, FBX viewers determine this by presence of DQ data or other flags.
            // For now, we'll add a custom property if needed, or rely on FBX default interpretation.
            // The "SkinningType" node property (string) is more common.
            node.Add("SkinningType", _element.SkinningType); // String version "Linear", "DualQuaternion"

            // Note: Skin deformers in FBX don't typically have a "Properties70" sub-node with many user-settable properties.
            // Their attributes are usually direct children of the Deformer node.
            // So, we don't call base.addObjectBody(node, writer) which adds Properties70.
        }

        public override void ProcessChildren(FbxFileWriterBase fwriter)
        {
            // Skin deformers are connected to Geometry and their Clusters.
            // These connections are established by the parent (e.g., FbxNodeTemplate processing its entities).
            // No direct hierarchical children to process from here in the FBX node tree.
            base.ProcessChildren(fwriter);
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            // Skin deformers don't use a generic PropertyTemplate from Definitions.
            // Their properties are specific and usually hardcoded.
            // Call base for any Element3D properties.
            base.ApplyTemplate(template);
        }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder); // Sets Id, Name from FbxNode

            if (FbxNode.TryGetNode("SkinningType", out FbxNode skinTypeNode))
            {
                _element.SkinningType = skinTypeNode.GetProperty<string>(0);
            }
            if (FbxNode.TryGetNode("Link_DeformAcuracy", out FbxNode accuracyNode))
            {
                _element.LinkDeformAccuracy = accuracyNode.GetProperty<double>(0);
            }
            // DeformedGeometry and Clusters are linked via connections, not direct properties here.
        }
    }
}