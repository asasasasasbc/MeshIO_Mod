// MeshIO.FBX/Templates/FbxNodeTemplate.cs
#if NETFRAMEWORK
using CSUtilities.Extensions;
#endif
using CSMath;
using MeshIO.Entities;
using MeshIO.FBX.Connections;
using MeshIO.FBX.Readers;
using MeshIO.FBX.Writers;
using MeshIO.Shaders;
using System.Collections.Generic;
using System.Reflection;
using System;
using MeshIO.Entities.Geometries;
using MeshIO.Entities.Skinning;

// MeshIO.FBX/Templates/FbxNodeTemplate.cs
// ... (usings)

namespace MeshIO.FBX.Templates
{
    internal class FbxNodeTemplate : FbxObjectTemplate<Node>
    {
        public override string FbxObjectName { get { return FbxFileToken.Model; } }
        public override string FbxTypeName { get { return "Null"; } } // Default for generic nodes

        public FbxNodeTemplate(FbxNode node) : base(node, new Node()) { }
        public FbxNodeTemplate(Node root) : base(root) { }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder);
            processChildren(builder);
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

        public override void ProcessChildren(FbxFileWriterBase fwriter)
        {
            // 1. Handle hierarchical child nodes (other Models/Bones)
            foreach (Node node_child in this._element.Nodes)
            {
                fwriter.CreateHierarchicalConnection(node_child, this);
            }

            // 2. Process entities attached to this Node.
            // This ensures their FBX objects are created and definitions are ready.
            Mesh meshEntity = null;
            Skin skinEntity = null;
            List<Entities.Entity> otherDirectEntities = new List<Entities.Entity>();

            foreach (Entities.Entity entity in this._element.Entities)
            {
                fwriter.EnsureFbxObjectCreated(entity); // Critical: creates FBX object for entity and processes its children
                if (entity is Mesh mesh) meshEntity = mesh;
                else if (entity is Skin skin) skinEntity = skin;
                else if (!(entity is Cluster))
                {
                    otherDirectEntities.Add(entity);
                }
            }

            // 3. Establish connections based on identified entities.

            // Connect Mesh (Geometry) to this Model (Node)
            if (meshEntity != null)
            {
                // Connection: Model -> Geometry (child is Geometry, parent is Model)
                fwriter.AddConnectionOO(meshEntity, this.GetElement());
            }

            // Connect Skinning components
            if (skinEntity != null) // Skin entity was found on this Node
            {
                // Connection: Model -> Skin (child is Skin, parent is Model)
                // This is a key connection for DCCs to recognize the Model is skinned.
                fwriter.AddConnectionOO(skinEntity, this.GetElement());

                if (meshEntity != null) // If there's also a mesh on this Node, Skin deforms it.
                {
                    // Connection: Geometry -> Skin (child is Skin, parent is Geometry)
                    // This indicates which Geometry the Skin deformer acts upon.
                    fwriter.AddConnectionOO(skinEntity, meshEntity);
                }

                // Skin.ProcessChildren (called during EnsureFbxObjectCreated(skinEntity))
                // should have called EnsureFbxObjectCreated for its Clusters.
                // Now, connect those Clusters to the Skin and their respective Bones.
                foreach (Cluster cluster in skinEntity.Clusters)
                {
                    // Ensure Cluster and its Link (Bone) are created (might be redundant but safe)
                    fwriter.EnsureFbxObjectCreated(cluster);
                    if (cluster.Link != null)
                    {
                        fwriter.EnsureFbxObjectCreated(cluster.Link);
                    }

                    // Connection: Skin -> Cluster (child is Cluster, parent is Skin)
                    fwriter.AddConnectionOO(cluster, skinEntity);

                    if (cluster.Link != null)
                    {
                        // Connection: Cluster -> Bone (child is Cluster, parent is Bone)
                        fwriter.AddConnectionOO(cluster, cluster.Link);
                    }
                }
            }

            // Connect other direct entities (Lights, Cameras) to this Model
            foreach (var entity in otherDirectEntities)
            {
                // Connection: Model -> OtherEntity (child is OtherEntity, parent is Model)
                fwriter.AddConnectionOO(entity, this.GetElement());
            }

            // Connect Materials to this Model
            foreach (Shaders.Material mat in this._element.Materials)
            {
                fwriter.EnsureFbxObjectCreated(mat);
                // Connection: Model -> Material (child is Material, parent is Model)
                fwriter.AddConnectionOO(mat, this.GetElement());
            }
        }
        protected override void addProperties(Dictionary<string, FbxProperty> properties)
        {
            var standardProps = new HashSet<string> { "Lcl Translation", "Lcl Rotation", "Lcl Scaling", "Visibility" };
            foreach (var prop in properties)
            {
                if (!standardProps.Contains(prop.Key) && !_element.Properties.Contains(prop.Key))
                {
                    _element.Properties.Add(prop.Value.ToProperty());
                }
            }
        }

        protected void processChildren(FbxFileBuilderBase builder) // This is for READING
        {
            foreach (FbxConnection c in builder.GetChildren(Id))
            {
                if (!builder.TryGetTemplate(c.ChildId, out IFbxObjectTemplate template))
                {
                    builder.Notify($"[{_element.GetType().FullName}] child object not found {c.ChildId}", Core.NotificationType.Warning);
                    continue;
                }
                addChild(template.GetElement());
                template.Build(builder);
            }
        }

        protected void addChild(Element3D element) // This is for READING
        {
            switch (element)
            {
                case Node node:
                    _element.Nodes.Add(node);
                    break;
                case Material mat:
                    _element.Materials.Add(mat);
                    break;
                case Entity entity:
                    _element.Entities.Add(entity);
                    break;
                default:
                    break;
            }
        }
    }
}