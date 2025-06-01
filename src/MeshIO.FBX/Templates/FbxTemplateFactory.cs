// MeshIO.FBX/Templates/FbxTemplateFactory.cs
using System;
using MeshIO.Entities; // Required for Bone
using MeshIO.Entities.Geometries;
using MeshIO.Shaders;

namespace MeshIO.FBX.Templates
{
    internal static class FbxTemplateFactory
    {
        public static IFbxObjectTemplate Create<T>(T element)
            where T : Element3D
        {
            switch (element)
            {
                case Mesh mesh:
                    return new FbxMeshTemplate(mesh);
                case Bone bone: // Add this case for Bone
                    return new FbxBoneTemplate(bone);
                case Node node: // Node should be after Bone, as Bone is a Node
                    return new FbxNodeTemplate(node);
                case Material material:
                    return new FbxMaterialTemplate(material);
                default:
                    throw new NotImplementedException($"{nameof(IFbxObjectTemplate)} for {element?.GetType().FullName ?? "null element"}");
            }
        }
    }
}