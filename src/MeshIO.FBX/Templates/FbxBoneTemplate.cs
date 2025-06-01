// MeshIO.FBX/Templates/FbxBoneTemplate.cs
using CSMath;
using MeshIO.Entities;
using MeshIO.FBX.Writers;
using MeshIO.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using MeshIO.FBX.Writers; // Add this if not present

namespace MeshIO.FBX.Templates
{
    internal class FbxBoneTemplate : FbxObjectTemplate<Bone>
    {
        public override string FbxObjectName { get { return FbxFileToken.Model; } }

        public override string FbxTypeName
        {
            get
            {
                return _element.IsSkeletonRoot ? FbxFileToken.SkeletonType : FbxFileToken.LimbNode;
            }
        }

        public FbxBoneTemplate(Bone bone) : base(bone) { }

        // Constructor for reading FBX (implement when needed)
        // public FbxBoneTemplate(FbxNode node, Bone bone) : base(node, bone) { }


        // ***** ADD THIS METHOD *****
        public override void ProcessChildren(FbxFileWriterBase fwriter)
        {
            // Create and connect NodeAttribute FOR THIS BONE
            // The FbxTypeName for NodeAttribute is the same as the bone's Model SubType (Skeleton/LimbNode)
            var nodeAttribute = new FbxNodeAttributeTemplate(this._element, this.FbxTypeName);
            fwriter.EnsureFbxObjectCreatedInternal(nodeAttribute); // Add to object list for definition

            // Connection: NodeAttribute (Source) -> Bone Model (Destination)
            // C: "OO", <NodeAttributeID_Source>, <BoneModelID_Destination>
            fwriter.AddConnectionOO_TemplateToTemplate(nodeAttribute, this);


            // Hierarchical connections for child bones
            foreach (Node node_child in this._element.Nodes) // _element is a Bone (Node)
            {
                fwriter.CreateHierarchicalConnection(node_child, this);
            }
        }

        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 232); // Common version for Model nodes

            // Apply specific properties for bones, overriding any defaults from the "Model" template.
            // These will be part of FbxInstanceProperties when base.addObjectBody is called.

            // Transform properties (will overwrite if already in FbxInstanceProperties from template)
            FbxInstanceProperties["Lcl Translation"] = new FbxProperty("Lcl Translation", "Lcl Translation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Translation);
            FbxInstanceProperties["Lcl Rotation"] = new FbxProperty("Lcl Rotation", "Lcl Rotation", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.EulerRotation);
            FbxInstanceProperties["Lcl Scaling"] = new FbxProperty("Lcl Scaling", "Lcl Scaling", "", PropertyFlags.Animatable | PropertyFlags.Animated, _element.Transform.Scale);

            // Visibility property
            FbxInstanceProperties["Visibility"] = new FbxProperty("Visibility", "Visibility", "", PropertyFlags.Animatable, 1.0);

            // Read RotationOrder from the Bone's custom properties
            int fbxRotationOrder = 0; // Default to XYZ if not found
            var rotOrderProp = _element.Properties.FirstOrDefault(p => p.Name == "RotationOrder");
            if (rotOrderProp != null && rotOrderProp.Value is int rotOrderVal)
            {
                fbxRotationOrder = rotOrderVal;
            }
            FbxInstanceProperties["RotationOrder"] = new FbxProperty("RotationOrder", "enum", "", PropertyFlags.None, fbxRotationOrder);

            // Bone-specific "Size" property (represents length in FBX for LimbNodes)
            if (_element.Length.HasValue)
            {
                FbxInstanceProperties["Size"] = new FbxProperty("Size", "double", "Number", PropertyFlags.None, _element.Length.Value);
            }
            else
            {
                // Default size if not specified. Blender often uses 0 for leaf bones if length isn't set.
                // Using a small default value or what's common in FBX (e.g., 1.0 or even 100 if units are cm).
                // Let's use 1.0 as a neutral default if not set.
                FbxInstanceProperties["Size"] = new FbxProperty("Size", "double", "Number", PropertyFlags.None, 1.0);
            }

            // Other typical Model properties that might be relevant for bones.
            // These ensure they are present if not in the generic "Model" template or override them.
            if (!FbxInstanceProperties.ContainsKey("Shading"))
                FbxInstanceProperties["Shading"] = new FbxProperty("Shading", "bool", "", PropertyFlags.None, true);

            if (!FbxInstanceProperties.ContainsKey("Culling"))
                FbxInstanceProperties["Culling"] = new FbxProperty("Culling", "KString", "", PropertyFlags.None, "CullingOff");

            // Ensure "MultiLayer" is present and typically false for bones.
            FbxInstanceProperties["MultiLayer"] = new FbxProperty("MultiLayer", "bool", "", PropertyFlags.None, false);

            // Ensure Color is present, even if bones don't usually render with it directly.
            if (!FbxInstanceProperties.ContainsKey("Color"))
            {
                FbxInstanceProperties["Color"] = new FbxProperty("Color", "ColorRGB", "Color", PropertyFlags.None, new Color(128, 128, 128)); // Default grey
            }


            // Call the base method to write the "Properties70" node using the FbxInstanceProperties
            base.addObjectBody(node, writer);
        }

        public override void ApplyTemplate(FbxPropertyTemplate template)
        {
            // 1. Start by populating FbxInstanceProperties with a copy of the generic "Model" template's properties.
            this.FbxInstanceProperties.Clear(); // Ensure a fresh start
            foreach (var prop in template.Properties)
            {
                // Create a new FbxProperty instance from the template's property.
                // This avoids modifying the shared template and ensures each bone has its own property set.
                this.FbxInstanceProperties.Add(prop.Key, new FbxProperty(prop.Value.Name, prop.Value.FbxType, prop.Value.Label, prop.Value.Flags, prop.Value.Value));
            }

            // 2. Add/Override with custom properties defined on the Bone element itself (_element.Properties).
            // These could be user-defined properties intended for FBX.
            foreach (Property item in this._element.Properties)
            {
                if (this.FbxInstanceProperties.ContainsKey(item.Name))
                {
                    // If a property with the same name exists (e.g., from the template),
                    // the user's custom property on the Bone element overrides it.
                    this.FbxInstanceProperties[item.Name] = FbxProperty.CreateFrom(item);
                }
                else
                {
                    // If it's a new property not in the template, add it.
                    this.FbxInstanceProperties.Add(item.Name, FbxProperty.CreateFrom(item));
                }
            }

            // Note: Critical properties like Lcl Translation, Size, etc., will be definitively set or
            // overridden in the `addObjectBody` method just before writing, ensuring the Bone's
            // specific data takes precedence for those well-defined FBX properties.
        }
    }
}