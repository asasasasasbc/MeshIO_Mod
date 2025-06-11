using CSMath;
using System.Collections.Generic;

namespace MeshIO.Entities.Geometries.Layers
{
    public class LayerElementBinormal : LayerElement
	{
		public List<XYZ> Binormals { get; set; } = new List<XYZ>(); // change from Normals to Binormals

		public List<double> Weights { get; set; } = new List<double>();
	}
}
