using MeshIO.FBX.Readers;
using MeshIO.Entities; // Added for Node type

namespace MeshIO.FBX.Templates
{
    internal class FbxRootNodeTemplate : FbxNodeTemplate
    {
        public FbxRootNodeTemplate(Node root) : base(root)
        {
        }

        public override void Build(FbxFileBuilderBase builder)
        {
            //TODO: Set properties from GlobalSettings
            // This might involve accessing builder.GetProperties(FbxFileToken.GlobalSettings)
            // and applying them to this._element (which is the scene's root Node) or a Scene object.

            // Call the reading-specific method from the base class
            base.processChildren_Reading(builder);
        }
    }
}