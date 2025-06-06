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
    
    
    public class GltfSampler {
        
        /// <summary>
        /// Backing field for MagFilter.
        /// </summary>
        private System.Nullable<MagFilterEnum> _magFilter;
        
        /// <summary>
        /// Backing field for MinFilter.
        /// </summary>
        private System.Nullable<MinFilterEnum> _minFilter;
        
        /// <summary>
        /// Backing field for WrapS.
        /// </summary>
        private WrapSEnum _wrapS = WrapSEnum.REPEAT;
        
        /// <summary>
        /// Backing field for WrapT.
        /// </summary>
        private WrapTEnum _wrapT = WrapTEnum.REPEAT;
        
        /// <summary>
        /// Backing field for Name.
        /// </summary>
        private string _name;
        
        /// <summary>
        /// Backing field for Extensions.
        /// </summary>
        private System.Collections.Generic.Dictionary<string, object> _extensions;
        
        /// <summary>
        /// Backing field for Extras.
        /// </summary>
        private GltfExtras _extras;
        
        /// <summary>
        /// Magnification filter.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("magFilter")]
        public System.Nullable<MagFilterEnum> MagFilter {
            get {
                return this._magFilter;
            }
            set {
                this._magFilter = value;
            }
        }
        
        /// <summary>
        /// Minification filter.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("minFilter")]
        public System.Nullable<MinFilterEnum> MinFilter {
            get {
                return this._minFilter;
            }
            set {
                this._minFilter = value;
            }
        }
        
        /// <summary>
        /// s wrapping mode.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("wrapS")]
        public WrapSEnum WrapS {
            get {
                return this._wrapS;
            }
            set {
                this._wrapS = value;
            }
        }
        
        /// <summary>
        /// t wrapping mode.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("wrapT")]
        public WrapTEnum WrapT {
            get {
                return this._wrapT;
            }
            set {
                this._wrapT = value;
            }
        }
        
        /// <summary>
        /// The user-defined name of this object.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("name")]
        public string Name {
            get {
                return this._name;
            }
            set {
                this._name = value;
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
        
        public bool ShouldSerializeMagFilter() {
            return ((_magFilter == null) 
                        == false);
        }
        
        public bool ShouldSerializeMinFilter() {
            return ((_minFilter == null) 
                        == false);
        }
        
        public bool ShouldSerializeWrapS() {
            return ((_wrapS == WrapSEnum.REPEAT) 
                        == false);
        }
        
        public bool ShouldSerializeWrapT() {
            return ((_wrapT == WrapTEnum.REPEAT) 
                        == false);
        }
        
        public bool ShouldSerializeName() {
            return ((_name == null) 
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
        
        public enum MagFilterEnum {
            
            NEAREST = 9728,
            
            LINEAR = 9729,
        }
        
        public enum MinFilterEnum {
            
            NEAREST = 9728,
            
            LINEAR = 9729,
            
            NEAREST_MIPMAP_NEAREST = 9984,
            
            LINEAR_MIPMAP_NEAREST = 9985,
            
            NEAREST_MIPMAP_LINEAR = 9986,
            
            LINEAR_MIPMAP_LINEAR = 9987,
        }
        
        public enum WrapSEnum {
            
            CLAMP_TO_EDGE = 33071,
            
            MIRRORED_REPEAT = 33648,
            
            REPEAT = 10497,
        }
        
        public enum WrapTEnum {
            
            CLAMP_TO_EDGE = 33071,
            
            MIRRORED_REPEAT = 33648,
            
            REPEAT = 10497,
        }
    }
}
