// MeshIO.FBX/Templates/FbxMaterialTemplate.cs
using MeshIO.Shaders;
using MeshIO.FBX.Writers;
using MeshIO.FBX.Readers; // For FbxFileBuilderBase if you implement reading
using System.Collections.Generic;
using CSMath; // For Color
using System; // For Convert
using System.Linq; // For _element.Properties.Any

namespace MeshIO.FBX.Templates
{
    internal class FbxMaterialTemplate : FbxObjectTemplate<Material>
    {
        public override string FbxObjectName { get { return FbxFileToken.Material; } }

        public override string FbxTypeName
        {
            get
            {
                // You can make this dynamic based on a ShadingModel property in MeshIO.Shaders.Material
                // For now, "FbxSurfacePhong" is a common and versatile material type.
                // "FbxSurfaceMaterial" is the base type in the template definition.
                return "FbxSurfacePhong";
            }
        }

        public FbxMaterialTemplate(Material material) : base(material) { }

        // Constructor for reading (if you implement it fully later)
        // public FbxMaterialTemplate(FbxNode node, Material material) : base(node, material) { }

        protected override void addObjectBody(FbxNode node, FbxFileWriterBase writer)
        {
            node.Add(FbxFileToken.Version, 102); // Common version for FBX materials

            // FbxInstanceProperties is populated by FbxObjectTemplate.ToFbxNode -> ApplyTemplate
            // which merges the FBX default template for "Material" with _element.Properties.
            // We now override/add specific properties based on the strongly-typed Material fields.

            // ShadingModel: Default from template is "Unknown". Override if a specific model is intended.
            // If _element (MeshIO.Shaders.Material) had a ShadingModel string property:
            // FbxInstanceProperties["ShadingModel"] = new FbxProperty("ShadingModel", "KString", "", PropertyFlags.None, _element.ShadingModel ?? "Phong");
            // For now, let's ensure it's something reasonable if not set by generic properties.
            if (!FbxInstanceProperties.ContainsKey("ShadingModel") || ((string)FbxInstanceProperties["ShadingModel"].Value) == "Unknown")
            {
                FbxInstanceProperties["ShadingModel"] = new FbxProperty("ShadingModel", "KString", "", PropertyFlags.None, "Phong");
            }
            if (!FbxInstanceProperties.ContainsKey("MultiLayer")) // Default from template is false.
            {
                FbxInstanceProperties["MultiLayer"] = new FbxProperty("MultiLayer", "bool", "", PropertyFlags.None, false);
            }


            // Diffuse Color
            var diffuse = _element.DiffuseColor; 
            FbxInstanceProperties["DiffuseColor"] = new FbxProperty("DiffuseColor", "ColorRGB", "Color", PropertyFlags.Animatable, diffuse);
            FbxInstanceProperties["Diffuse"] = new FbxProperty("Diffuse", "ColorRGB", "Color", PropertyFlags.None, diffuse); // Alias

            // Ambient Color
            var ambient = _element.AmbientColor; // Default to 20% of diffuse
            FbxInstanceProperties["AmbientColor"] = new FbxProperty("AmbientColor", "ColorRGB", "Color", PropertyFlags.Animatable, ambient);
            FbxInstanceProperties["Ambient"] = new FbxProperty("Ambient", "ColorRGB", "Color", PropertyFlags.None, ambient); // Alias

            // Specular Color
            var specular = _element.SpecularColor; // Default to dark gray for subtle specular
            FbxInstanceProperties["SpecularColor"] = new FbxProperty("SpecularColor", "ColorRGB", "Color", PropertyFlags.Animatable, specular);
            FbxInstanceProperties["Specular"] = new FbxProperty("Specular", "ColorRGB", "Color", PropertyFlags.None, specular); // Alias

            // Emissive Color
            var emissive = _element.EmissiveColor; // Default to black (no emission)
            FbxInstanceProperties["EmissiveColor"] = new FbxProperty("EmissiveColor", "ColorRGB", "Color", PropertyFlags.Animatable, emissive);
            FbxInstanceProperties["Emissive"] = new FbxProperty("Emissive", "ColorRGB", "Color", PropertyFlags.None, emissive); // Alias

            // Shininess / Specular Exponent
            // FBX Shininess/ShininessExponent often in range like 0-100 or higher.
            // Assuming _element.ShininessFactor is 0-1.
            var shininessFactor = _element.ShininessExponent; // Default to a moderate shininess
            var fbxShininess = shininessFactor * 1.0f; // 
            FbxInstanceProperties["ShininessExponent"] = new FbxProperty("ShininessExponent", "double", "Number", PropertyFlags.Animatable, (double)fbxShininess);
            FbxInstanceProperties["Shininess"] = new FbxProperty("Shininess", "double", "Number", PropertyFlags.None, (double)fbxShininess); // Alias

            // Transparency
            // _element.TransparencyFactor: 0.0 = opaque, 1.0 = fully transparent
            var transparency = _element.TransparencyFactor;
            // FBX TransparencyFactor also typically 0.0 = opaque, 1.0 = transparent
            FbxInstanceProperties["TransparentColor"] = new FbxProperty("TransparentColor", "ColorRGB", "Color", PropertyFlags.Animatable, new Color(255, 255, 255)); // Usually means the color that becomes transparent
            FbxInstanceProperties["TransparencyFactor"] = new FbxProperty("TransparencyFactor", "double", "Number", PropertyFlags.Animatable, (double)transparency);

            // Reflection
            //var reflectionFactor = _element.Refle;
            //var reflectionColor = _element.ReflectionColor; // Typically black if no reflection
            //FbxInstanceProperties["Reflectivity"] = new FbxProperty("Reflectivity", "double", "Number", PropertyFlags.Animatable, (double)reflectionFactor);
            //FbxInstanceProperties["ReflectionColor"] = new FbxProperty("ReflectionColor", "ColorRGB", "Color", PropertyFlags.Animatable, reflectionColor);

            // After FbxInstanceProperties is fully prepared with defaults and specific overrides,
            // the base class's addObjectBody (or equivalent logic in FbxObjectTemplate.ToFbxNode)
            // will use it to write the "PropertiesXX" sub-node.
            // Our FbxObjectTemplate.addObjectBody already does this:
            // node.Nodes.Add(writer.PropertiesToNode(this.FbxInstanceProperties.Values));
            // So we don't need to call it again here.
            base.addObjectBody(node, writer);
        }

        public override void Build(FbxFileBuilderBase builder)
        {
            base.Build(builder); // Handles ID, Name, and generic properties from FbxNode

            FbxPropertyTemplate template = builder.GetProperties(FbxObjectName);
            Dictionary<string, FbxProperty> nodeProps = builder.ReadProperties(FbxNode);

            // Merge template and node properties for a complete view
            foreach (var t in template.Properties)
            {
                if (!nodeProps.ContainsKey(t.Key))
                {
                    nodeProps.Add(t.Key, t.Value);
                }
            }

            // Map FBX properties back to Material element
            // Helper to get value, converting types
            TVal GetPropVal<TVal>(string propName, string altPropName = null)
            {
                if (nodeProps.TryGetValue(propName, out var prop) || (altPropName != null && nodeProps.TryGetValue(altPropName, out prop)))
                {
                    var p = FbxProperty.CreateFrom(prop).ToProperty();
                    if (p.Value is TVal val) return val;
                    try { return (TVal)Convert.ChangeType(p.Value, typeof(TVal)); }
                    catch { /* type conversion failed */ }
                }
                return default(TVal);
            }

            float? GetFloatPropVal(string propName, string altPropName = null, float scale = 1.0f)
            {
                if (nodeProps.TryGetValue(propName, out var prop) || (altPropName != null && nodeProps.TryGetValue(altPropName, out prop)))
                {
                    var pVal = FbxProperty.CreateFrom(prop).ToProperty().Value;
                    return Convert.ToSingle(pVal) * scale;
                }
                return null;
            }


            _element.DiffuseColor = GetPropVal<Color>("DiffuseColor", "Diffuse");
            _element.AmbientColor = GetPropVal<Color>("AmbientColor", "Ambient");
            _element.SpecularColor = GetPropVal<Color>("SpecularColor", "Specular");
            _element.EmissiveColor = GetPropVal<Color>("EmissiveColor", "Emissive");

            _element.ShininessExponent = (double)GetFloatPropVal("ShininessExponent", "Shininess", 1.0f); // Assuming FBX is 0-100

            _element.TransparencyFactor = (double)GetFloatPropVal("TransparencyFactor");
            // If only Opacity is present: TransparencyFactor = 1.0 - Opacity
            //if (!_element.TransparencyFactor > 0 && nodeProps.TryGetValue("Opacity", out var opacityProp))
            //{
            //    _element.TransparencyFactor = 1.0f - Convert.ToSingle(FbxProperty.CreateFrom(opacityProp).ToProperty().Value);
            //}

            //_element.ReflectionFactor = GetFloatPropVal("Reflectivity", "ReflectionFactor");
            //_element.ReflectionColor = GetPropVal<Color?>("ReflectionColor", "Reflection");

            // String properties like ShadingModel could be read here if MeshIO.Shaders.Material supports them
            // if (nodeProps.TryGetValue("ShadingModel", out var shadingModelProp))
            //    _element.ShadingModel = (string)FbxProperty.CreateFrom(shadingModelProp).ToProperty().Value;

            // Remove processed properties from nodeProps so base.addProperties doesn't add them again
            var processedKeys = new List<string> {
                "DiffuseColor", "Diffuse", "AmbientColor", "Ambient", "SpecularColor", "Specular",
                "EmissiveColor", "Emissive", "ShininessExponent", "Shininess", "TransparentColor",
                "TransparencyFactor", "Opacity", "Reflectivity", "ReflectionFactor", "ReflectionColor", "Reflection",
                "ShadingModel", "MultiLayer" // Add other known material prop names
            };
            foreach (var key in processedKeys) { nodeProps.Remove(key); }

            // Add remaining properties as generic Element3D properties
            addProperties(nodeProps); // Calls the FbxObjectTemplate.addProperties
        }
    }
}