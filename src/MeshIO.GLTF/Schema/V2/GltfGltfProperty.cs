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
    
    
    public class GltfGltfProperty {
        
        /// <summary>
        /// Backing field for Extensions.
        /// </summary>
        private System.Collections.Generic.Dictionary<string, object> _extensions;
        
        /// <summary>
        /// Backing field for Extras.
        /// </summary>
        private GltfExtras _extras;
        
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
        
        public bool ShouldSerializeExtensions() {
            return ((_extensions == null) 
                        == false);
        }
        
        public bool ShouldSerializeExtras() {
            return ((_extras == null) 
                        == false);
        }
    }
}
