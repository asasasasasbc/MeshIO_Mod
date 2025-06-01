// MeshIO.FBX/Templates/FbxClusterTemplate.cs
using CSMath;
using MeshIO.Entities.Skinning;
using MeshIO.FBX.Writers;
using MeshIO.FBX.Readers;
using MeshIO.Utils; // For ToRowMajorArray
using System.Linq;

namespace MeshIO.FBX.Templates
{
    internal class FbxClusterTemplate : FbxObjectTemplate<Cluster>
    {
        public override string FbxObjectName { get { return FbxFileToken.Deformer; } }
        public override string FbxTypeName { get { return FbxFileToken.ClusterType; } }

        public FbxClusterTemplate(Cluster cluster) : base(cluster) { }
        public FbxClusterTemplate(FbxNode node, Cluster cluster) : base(node, cluster) { }

        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 100); // Common version for Cluster

            if (_element.Indexes != null && _element.Indexes.Any())
            {
                node.Add("Indexes", _element.Indexes.ToArray());
            }
            if (_element.Weights != null && _element.Weights.Any())
            {
                node.Add("Weights", _element.Weights.ToArray());
            }

            node.Add("Transform", _element.TransformMatrix.ToRowMajorArray());
            node.Add("TransformLink", _element.TransformLinkMatrix.ToRowMajorArray());
            node.Add("Mode", _element.Mode);

            // Similar to Skin, Clusters don't typically have a "Properties70" sub-node.
        }

        public override void ProcessChildren(FbxFileWriterBase fwriter)
        {
            // Clusters are connected to the Bone (Link) they control.
            // This connection is established by the FbxNodeTemplate when processing entities.
            base.ProcessChildren(fwriter);
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            // Clusters don't use a generic PropertyTemplate.
            base.ApplyTemplate(template);
        }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder); // Sets Id, Name

            if (FbxNode.TryGetNode("Indexes", out var indexesNode))
                _element.Indexes.AddRange(indexesNode.Properties.OfType<int[]>().FirstOrDefault() ?? System.Array.Empty<int>());

            if (FbxNode.TryGetNode("Weights", out var weightsNode))
                _element.Weights.AddRange(weightsNode.Properties.OfType<double[]>().FirstOrDefault() ?? System.Array.Empty<double>());

            if (FbxNode.TryGetNode("Transform", out var transformNode))
            {
                var arr = transformNode.Properties.OfType<double[]>().FirstOrDefault();
                if (arr != null && arr.Length == 16)
                    _element.TransformMatrix = new Matrix4(arr); // Assuming Matrix4 constructor from double[16]
            }

            if (FbxNode.TryGetNode("TransformLink", out var transformLinkNode))
            {
                var arr = transformLinkNode.Properties.OfType<double[]>().FirstOrDefault();
                if (arr != null && arr.Length == 16)
                    _element.TransformLinkMatrix = new Matrix4(arr);
            }
            if (FbxNode.TryGetNode("Mode", out var modeNode))
            {
                _element.Mode = modeNode.GetProperty<string>(0);
            }
            // The 'Link' (Bone) is established via connections.
        }
    }
}