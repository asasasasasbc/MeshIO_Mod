using MeshIO.FBX.Readers;
using MeshIO.FBX.Writers;
using MeshIO.Shaders;
using MeshIO.Entities;
using MeshIO.Entities.Skinning;
using MeshIO.Entities.Geometries;
using System.Collections.Generic;
using System.Linq;
using System;
using MeshIO.FBX.Connections;

namespace MeshIO.FBX.Templates
{
    internal class FbxNodeTemplate : FbxObjectTemplate<Node>
    {
        public override string FbxObjectName { get { return FbxFileToken.Model; } }

        public override string FbxTypeName
        {
            get
            {
                if (_element.Entities.Any(e => e is Mesh))
                {
                    return FbxFileToken.Mesh;
                }
                return "Null";
            }
        }

        public FbxNodeTemplate(FbxNode fbxNode) : base(fbxNode, new Node()) { }
        public FbxNodeTemplate(Node meshIoNode) : base(meshIoNode) { }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder);

            FbxPropertyTemplate template = builder.GetProperties(FbxObjectName);
            Dictionary<string, FbxProperty> nodeProps = builder.ReadProperties(FbxNode);

            foreach (var t in template.Properties)
            {
                if (!nodeProps.ContainsKey(t.Key)) nodeProps.Add(t.Key, t.Value);
            }

            if (nodeProps.TryGetValue("Lcl Translation", out var translationProp))
                _element.Transform.Translation = (CSMath.XYZ)translationProp.ToProperty().Value;
            if (nodeProps.TryGetValue("Lcl Rotation", out var rotationProp))
                _element.Transform.EulerRotation = (CSMath.XYZ)rotationProp.ToProperty().Value;
            if (nodeProps.TryGetValue("Lcl Scaling", out var scalingProp))
                _element.Transform.Scale = (CSMath.XYZ)scalingProp.ToProperty().Value;
            if (nodeProps.TryGetValue("Visibility", out var visibilityProp))
                _element.IsVisible = Convert.ToBoolean(visibilityProp.ToProperty().Value);

            var processedKeys = new List<string> { "Lcl Translation", "Lcl Rotation", "Lcl Scaling", "Visibility" };
            foreach (var key in processedKeys) nodeProps.Remove(key);

            addProperties(nodeProps);

            processChildren_Reading(builder);
        }

        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 232);

            FbxInstanceProperties["Lcl Translation"] = new FbxProperty("Lcl Translation", "Lcl Translation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Translation);
            FbxInstanceProperties["Lcl Rotation"] = new FbxProperty("Lcl Rotation", "Lcl Rotation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.EulerRotation);
            FbxInstanceProperties["Lcl Scaling"] = new FbxProperty("Lcl Scaling", "Lcl Scaling", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Scale);
            FbxInstanceProperties["Visibility"] = new FbxProperty("Visibility", "Visibility", "", PropertyFlags.Animatable, _element.IsVisible ? 1.0 : 0.0);

            if (!FbxInstanceProperties.ContainsKey("Shading"))
                FbxInstanceProperties["Shading"] = new FbxProperty("Shading", "bool", "", PropertyFlags.None, true);
            if (!FbxInstanceProperties.ContainsKey("Culling"))
                FbxInstanceProperties["Culling"] = new FbxProperty("Culling", "KString", "", PropertyFlags.None, "CullingOff");

            if (!FbxInstanceProperties.ContainsKey("MultiLayer"))
                FbxInstanceProperties["MultiLayer"] = new FbxProperty("MultiLayer", "bool", "", PropertyFlags.None, false);

            base.addObjectBody(node, writer);
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            this.FbxInstanceProperties.Clear();
            foreach (var prop in template.Properties)
            {
                this.FbxInstanceProperties.Add(prop.Key, new FbxProperty(prop.Value.Name, prop.Value.FbxType, prop.Value.Label, prop.Value.Flags, prop.Value.Value));
            }
            foreach (Property item in this._element.Properties)
            {
                if (this.FbxInstanceProperties.ContainsKey(item.Name))
                {
                    this.FbxInstanceProperties[item.Name] = FbxProperty.CreateFrom(item);
                }
                else
                {
                    this.FbxInstanceProperties.Add(item.Name, FbxProperty.CreateFrom(item));
                }
            }
        }

        public override void ProcessChildren(FbxFileWriterBase fwriter) // For Writing
        {
            // 1. Handle hierarchical child nodes (Models/Bones)
            foreach (Node node_child in this._element.Nodes)
            {
                fwriter.CreateHierarchicalConnection(node_child, this);
            }

            // 2. Process entities attached to this Node.
            Mesh meshEntity = null;
            Skin skinEntity = null;
            List<Entities.Entity> otherDirectEntities = new List<Entities.Entity>();

            foreach (Entities.Entity entity in this._element.Entities)
            {
                fwriter.EnsureFbxObjectCreated(entity);
                if (entity is Mesh mesh) meshEntity = mesh;
                else if (entity is Skin skin) skinEntity = skin;
                else if (!(entity is Cluster)) otherDirectEntities.Add(entity);
            }

            // 3. Establish connections based on identified entities.

            // Connect Mesh (Geometry) to this Model (Node)
            if (meshEntity != null)
            {
                // FBX: C: "OO", <Mesh_ID_Source>, <Model_ID_Destination>
                fwriter.AddConnectionOO_ChildToParent(meshEntity, this.GetElement());
            }

            // Connect Skinning components
            if (skinEntity != null && meshEntity != null)
            {
                // Connection: Skin deforms Geometry
                // FBX: C: "Deformer", <Skin_ID_Source>, <Mesh_ID_Destination>
                fwriter.AddConnectionDeformer_SkinToMesh(skinEntity, meshEntity);

                foreach (Cluster cluster in skinEntity.Clusters)
                {
                    fwriter.EnsureFbxObjectCreated(cluster);
                    if (cluster.Link != null) fwriter.EnsureFbxObjectCreated(cluster.Link);

                    // Connection: Skin has Cluster
                    // FBX: C: "SubDeformer", <Cluster_ID_Source>, <Skin_ID_Destination>
                    fwriter.AddConnectionSubDeformer_ClusterToSkin(cluster, skinEntity);

                    if (cluster.Link != null)
                    {
                        // Connection: Cluster is linked to Bone
                        // FBX: C: "Deformer", <Cluster_ID_Source>, <Bone_ID_Destination>
                        fwriter.AddConnectionDeformer_ClusterToBone(cluster, cluster.Link);
                    }
                }
            }

            // Connect other direct entities (Lights, Cameras) to this Model
            foreach (var entity in otherDirectEntities)
            {
                fwriter.AddConnectionOO_ChildToParent(entity, this.GetElement());
            }

            // Connect Materials to this Model
            foreach (Shaders.Material mat in this._element.Materials)
            {
                fwriter.EnsureFbxObjectCreated(mat);
                fwriter.AddConnectionOO_ChildToParent(mat, this.GetElement());
            }
        }

        protected override void addProperties(Dictionary<string, FbxProperty> properties)
        {
            foreach (var prop in properties)
            {
                if (!_element.Properties.Contains(prop.Key))
                {
                    _element.Properties.Add(prop.Value.ToProperty());
                }
            }
        }

        protected void processChildren_Reading(FbxFileBuilderBase builder)
        {
            foreach (FbxConnection c in builder.GetChildren(Id))
            {
                if (!builder.TryGetTemplate(c.ChildId, out IFbxObjectTemplate template))
                {
                    builder.Notify($"[{_element.GetType().FullName}] child object not found {c.ChildId}", Core.NotificationType.Warning);
                    continue;
                }
                Element3D childElement = template.GetElement();
                addChild_Reading(childElement, c.ConnectionType);

                template.Build(builder);
            }
        }

        protected void addChild_Reading(Element3D element, FbxConnectionType connectionType)
        {
            switch (element)
            {
                case Node node:
                    _element.Nodes.Add(node);
                    // if (node.Parent == null) node.Parent = this._element; // Requires Node.Parent to be settable
                    break;
                case Material mat:
                    _element.Materials.Add(mat);
                    break;
                case Entity entity:
                    _element.Entities.Add(entity);
                    // if (!entity.ParentNodes.Contains(this._element)) entity.ParentNodes.Add(this._element); // Requires ParentNodes
                    break;
            }
        }
    }
}