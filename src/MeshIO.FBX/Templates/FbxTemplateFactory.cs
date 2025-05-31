using System;
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
				case Node node:
					return new FbxNodeTemplate(node);
                case Material material: // Add this case
                    return new FbxMaterialTemplate(material);
                default:
                    // It's helpful to include the actual type name in the exception
                    throw new NotImplementedException($"{nameof(IFbxObjectTemplate)} for {element?.GetType().FullName ?? "null element"}");
            }
		}
	}
}
