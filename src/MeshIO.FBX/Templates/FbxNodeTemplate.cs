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

namespace MeshIO.FBX.Templates
{
    internal class FbxNodeTemplate : FbxObjectTemplate<Node>
    {
        public override string FbxObjectName { get { return FbxFileToken.Model; } }

        // ***** THIS IS THE KEY CHANGE *****
        public override string FbxTypeName { get { return "Null"; } } // Changed from FbxFileToken.Mesh or other

        public FbxNodeTemplate(FbxNode node) : base(node, new Node())
        {
        }

        public FbxNodeTemplate(Node root) : base(root)
        {
        }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder);
            processChildren(builder);
        }

        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 232); // Standard version for Model nodes

            // Apply specific properties for nodes, overriding any defaults from the "Model" template.
            FbxInstanceProperties["Lcl Translation"] = new FbxProperty("Lcl Translation", "Lcl Translation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Translation);
            FbxInstanceProperties["Lcl Rotation"] = new FbxProperty("Lcl Rotation", "Lcl Rotation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.EulerRotation);
            FbxInstanceProperties["Lcl Scaling"] = new FbxProperty("Lcl Scaling", "Lcl Scaling", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Scale);
            FbxInstanceProperties["Visibility"] = new FbxProperty("Visibility", "Visibility", "", PropertyFlags.Animatable, _element.IsVisible ? 1.0 : 0.0);

            // Add standard model properties often expected for Null nodes as well
            if (!FbxInstanceProperties.ContainsKey("Shading"))
                FbxInstanceProperties["Shading"] = new FbxProperty("Shading", "bool", "", PropertyFlags.None, true);
            if (!FbxInstanceProperties.ContainsKey("Culling"))
                FbxInstanceProperties["Culling"] = new FbxProperty("Culling", "KString", "", PropertyFlags.None, "CullingOff");
            // Ensure "MultiLayer" is present and typically false.
            FbxInstanceProperties["MultiLayer"] = new FbxProperty("MultiLayer", "bool", "", PropertyFlags.None, false);


            base.addObjectBody(node, writer); // This will write out FbxInstanceProperties

            // These were specific to the old "Mesh" type idea, remove or ensure they are not added if type is "Null"
            // For "Null" type, these are not strictly necessary but FBX is flexible.
            // Let's keep them minimal for "Null" nodes.
            // The base.addObjectBody already writes Properties70.
            // node.Add(FbxFileToken.Shading, 'T'); // Usually for renderable objects
            // node.Add(FbxFileToken.CullingOff, "CullingOff"); // Culling is relevant, "Culling" property above handles it
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            // Start with a copy of the generic "Model" template's properties.
            this.FbxInstanceProperties.Clear();
            foreach (var prop in template.Properties)
            {
                this.FbxInstanceProperties.Add(prop.Key, new FbxProperty(prop.Value.Name, prop.Value.FbxType, prop.Value.Label, prop.Value.Flags, prop.Value.Value));
            }

            // Override/add with custom properties from the Node element.
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
            Console.WriteLine($"ProcessChildren for: '{this._element.Name}' (ID: {this._element.Id}, Type: {this._element.GetType().Name}) with {this._element.Nodes.Count} child nodes.");
            base.ProcessChildren(fwriter);

            foreach (Node node in this._element.Nodes)
            {
                Console.WriteLine($"  Processing child node: '{node.Name}' of '{this._element.Name}'");
                fwriter.CreateConnection(node, this);
            }

            foreach (Shaders.Material mat in this._element.Materials)
            {
                fwriter.CreateConnection(mat, this);
            }

            foreach (Entities.Entity entity in this._element.Entities)
            {
                fwriter.CreateConnection(entity, this);
            }
        }

        protected override void addProperties(Dictionary<string, FbxProperty> properties)
        {
            // Lcl Translation, Rotation, Scaling are now handled in addObjectBody via FbxInstanceProperties
            // Visibility is also handled there.
            // So, we only need to add any *other* custom properties here.

            var standardProps = new HashSet<string> { "Lcl Translation", "Lcl Rotation", "Lcl Scaling", "Visibility" };

            foreach (var prop in properties)
            {
                if (!standardProps.Contains(prop.Key) && !_element.Properties.Contains(prop.Key)) // Avoid adding if already directly set on _element or is standard
                {
                    _element.Properties.Add(prop.Value.ToProperty());
                }
            }
            // base.addProperties(properties); // Don't call base if it also tries to add these.
        }

        protected void processChildren(FbxFileBuilderBase builder)
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

        protected void addChild(Element3D element)
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