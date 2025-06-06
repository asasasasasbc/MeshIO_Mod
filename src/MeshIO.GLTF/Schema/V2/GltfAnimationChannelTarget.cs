//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MeshIO.GLTF.Schema.V2 {
    using System.Linq;
    using System.Runtime.Serialization;
    
    
    public class GltfAnimationChannelTarget {
        
        /// <summary>
        /// Backing field for Node.
        /// </summary>
        private System.Nullable<int> _node;
        
        /// <summary>
        /// Backing field for Path.
        /// </summary>
        private PathEnum _path;
        
        /// <summary>
        /// Backing field for Extensions.
        /// </summary>
        private System.Collections.Generic.Dictionary<string, object> _extensions;
        
        /// <summary>
        /// Backing field for Extras.
        /// </summary>
        private GltfExtras _extras;
        
        /// <summary>
        /// The index of the node to target.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("node")]
        public System.Nullable<int> Node {
            get {
                return this._node;
            }
            set {
                if ((value < 0)) {
                    throw new System.ArgumentOutOfRangeException("Node", value, "Expected value to be greater than or equal to 0");
                }
                this._node = value;
            }
        }
        
        /// <summary>
        /// The name of the node's TRS property to modify, or the "weights" of the Morph Targets it instantiates. For the "translation" property, the values that are provided by the sampler are the translation along the x, y, and z axes. For the "rotation" property, the values are a quaternion in the order (x, y, z, w), where w is the scalar. For the "scale" property, the values are the scaling factors along the x, y, and z axes.
        /// </summary>
        [Newtonsoft.Json.JsonConverterAttribute(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        [Newtonsoft.Json.JsonRequiredAttribute()]
        [Newtonsoft.Json.JsonPropertyAttribute("path")]
        public PathEnum Path {
            get {
                return this._path;
            }
            set {
                this._path = value;
            }
        }
        
        /// <summary>
        /// Dictionary object with extension-specific objects.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("extensions")]
        public System.Collections.Generic.Dictionary<string, object> Extensions {
            get {
                return this._extensions;
            }
            set {
                this._extensions = value;
            }
        }
        
        /// <summary>
        /// Application-specific data.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("extras")]
        public GltfExtras Extras {
            get {
                return this._extras;
            }
            set {
                this._extras = value;
            }
        }
        
        public bool ShouldSerializeNode() {
            return ((_node == null) 
                        == false);
        }
        
        public bool ShouldSerializeExtensions() {
            return ((_extensions == null) 
                        == false);
        }
        
        public bool ShouldSerializeExtras() {
            return ((_extras == null) 
                        == false);
        }
        
        public enum PathEnum {
            
            translation,
            
            rotation,
            
            scale,
            
            weights,
        }
    }
}
