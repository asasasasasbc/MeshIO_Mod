// MeshIO.FBX/Templates/FbxTemplateFactory.cs
using System;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
using MeshIO.Shaders;
using MeshIO.Entities.Skinning; // <<<< ADD THIS

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
                case Bone bone:
                    return new FbxBoneTemplate(bone);
                case Node node:
                    return new FbxNodeTemplate(node);
                case Material material:
                    return new FbxMaterialTemplate(material);
                case Skin skin: // <<<< ADD THIS
                    return new FbxSkinTemplate(skin);
                case Cluster cluster: // <<<< ADD THIS
                    return new FbxClusterTemplate(cluster);
                default:
                    Console.WriteLine($"Warning: No FBX template factory case for {element?.GetType().FullName}. Element ID: {element?.Id}, Name: {element?.Name}");
                    // throw new NotImplementedException($"{nameof(IFbxObjectTemplate)} for {element?.GetType().FullName ?? "null element"}");
                    return null; // Or handle appropriately
            }
        }
    }
}