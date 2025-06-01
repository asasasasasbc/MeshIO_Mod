// MeshIO.FBX/Templates/FbxClusterTemplate.cs
using CSMath;
using MeshIO.Entities.Skinning;
using MeshIO.FBX.Writers;
using MeshIO.FBX.Readers;
using MeshIO.Utils; // For ToRowMajorArray
using System.Linq;
using System;
using System.Collections.Generic; // For Convert

namespace MeshIO.FBX.Templates
{
    internal class FbxClusterTemplate : FbxObjectTemplate<Cluster>
    {
        public override string FbxObjectName { get { return FbxFileToken.Deformer; } }
        public override string FbxTypeName { get { return FbxFileToken.ClusterType; } }

        public FbxClusterTemplate(Cluster cluster) : base(cluster) { }
        public FbxClusterTemplate(FbxNode node, Cluster cluster) : base(node, cluster) { } // For Reading

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

            // ***** ADD THIS SECTION *****
            // Add the Link to the bone
            if (_element.Link != null && _element.Link.Id.HasValue)
            {
                // The "Link" property in a Deformer::Cluster object stores the ID of the bone Model.
                node.Add("Link", (long)_element.Link.Id.Value);
            }
            else
            {
                // This would be an issue, a cluster should always link to a bone.
                // Console.WriteLine($"Warning: Cluster '{_element.Name}' has no Link or Link ID.");
                node.Add("Link", (long)0); // Add a dummy link if missing, though this is problematic
            }
            // ***************************

            // Clusters don't typically have a "Properties70" sub-node.
            // So, we do not call base.addObjectBody(node, writer) here.
        }

        public override void ProcessChildren(FbxFileWriterBase fwriter)
        {
            // Clusters themselves don't have hierarchical children in the FBX node tree.
            // Their primary relationship is the 'Link' to a bone, handled by connections and the Link property.
            base.ProcessChildren(fwriter);
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            // Clusters don't use a generic PropertyTemplate from Definitions.
            base.ApplyTemplate(template); // Handles Element3D.Properties if any were intended for FBX properties
        }

        public override void Build(FbxFileBuilderBase builder) // For Reading
        {
            base.Build(builder); // Sets Id, Name

            if (FbxNode.TryGetNode("Indexes", out var indexesNode) && indexesNode.Properties.Any())
            {
                var indexArrayProp = indexesNode.Properties[0];
                if (indexArrayProp is int[] intArray)
                    _element.Indexes.AddRange(intArray);
                else if (indexArrayProp is IEnumerable<object> objEnumerable)
                    _element.Indexes.AddRange(objEnumerable.Select(o => Convert.ToInt32(o)));
            }

            if (FbxNode.TryGetNode("Weights", out var weightsNode) && weightsNode.Properties.Any())
            {
                var weightArrayProp = weightsNode.Properties[0];
                if (weightArrayProp is double[] doubleArray)
                    _element.Weights.AddRange(doubleArray);
                else if (weightArrayProp is IEnumerable<object> objEnumerable)
                    _element.Weights.AddRange(objEnumerable.Select(o => Convert.ToDouble(o)));
            }

            if (FbxNode.TryGetNode("Transform", out var transformNode) && transformNode.Properties.Any())
            {
                var arr = transformNode.Properties[0] as double[];
                if (arr != null && arr.Length == 16)
                    _element.TransformMatrix = new Matrix4(arr);
            }

            if (FbxNode.TryGetNode("TransformLink", out var transformLinkNode) && transformLinkNode.Properties.Any())
            {
                var arr = transformLinkNode.Properties[0] as double[];
                if (arr != null && arr.Length == 16)
                    _element.TransformLinkMatrix = new Matrix4(arr);
            }
            if (FbxNode.TryGetNode("Mode", out var modeNode) && modeNode.Properties.Any())
            {
                _element.Mode = modeNode.GetProperty<string>(0);
            }

            // For Reading: Find the Link property and try to associate the bone
            if (FbxNode.TryGetNode("Link", out var linkNode) && linkNode.Properties.Any())
            {
                if (linkNode.Properties[0] is long boneId && boneId != 0)
                {
                    // Store this ID. Post-processing will be needed to find the actual Node object
                    // from the builder._objectTemplates collection and assign it to _element.Link.
                    // For now, we can add a temporary property or handle it in a later pass.
                    // Example: _element.Properties.Add(new Property("FbxLinkBoneID", boneId));
                }
            }
        }
    }
}