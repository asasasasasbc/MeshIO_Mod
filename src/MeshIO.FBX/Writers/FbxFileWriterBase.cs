using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MeshIO.FBX.Connections;
using MeshIO.FBX.Templates;
using MeshIO.Entities.Skinning; // Added for Skin, Cluster
using MeshIO.Entities.Geometries; // Added for Geometry

namespace MeshIO.FBX.Writers
{
    internal abstract class FbxFileWriterBase
    {
        public FbxVersion Version { get { return this.Options.Version; } }

        public FbxWriterOptions Options { get; }

        public Scene Scene { get; }

        public Node RootNode { get { return this.Scene.RootNode; } }

        protected readonly Dictionary<string, FbxPropertyTemplate> _tempaltes = new();

        protected readonly Dictionary<string, List<IFbxObjectTemplate>> _definedObjects = new();

        protected readonly Dictionary<ulong, IFbxObjectTemplate> _objectTemplates = new();

        protected readonly List<FbxConnection> _connections = new();

        private readonly FbxRootNode fbxRoot;

        private readonly string MeshIOVersion;

        // To prevent recursion during EnsureFbxObjectCreated
        private readonly HashSet<ulong> _currentlyProcessingForCreation = new HashSet<ulong>();


        protected FbxFileWriterBase(Scene scene, FbxWriterOptions options)
        {
            this.Scene = scene;
            this.Options = options;

            this.fbxRoot = new FbxRootNode
            {
                Version = this.Options.Version
            };

            this.MeshIOVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public static FbxFileWriterBase Create(Scene scene, FbxWriterOptions options)
        {
            FbxVersion version = options.Version;
            switch (version)
            {
                case FbxVersion.v2000:
                case FbxVersion.v2001:
                case FbxVersion.v3000:
                case FbxVersion.v3001:
                case FbxVersion.v4000:
                case FbxVersion.v4001:
                case FbxVersion.v4050:
                case FbxVersion.v5000:
                case FbxVersion.v5800:
                case FbxVersion.v6000:
                case FbxVersion.v6100:
                    throw new NotSupportedException($"Fbx version {version} no supported for writer");
                case FbxVersion.v7000:
                case FbxVersion.v7100:
                case FbxVersion.v7200:
                case FbxVersion.v7300:
                case FbxVersion.v7400:
                case FbxVersion.v7500:
                case FbxVersion.v7600:
                case FbxVersion.v7700:
                    return new FbxFileWriter7000(scene, options);
                default:
                    throw new NotSupportedException($"Unknown Fbx version {version} for writer");
            }
        }

        public FbxRootNode ToNodeStructure()
        {
            this.initializeRoot(); // This will populate _definedObjects and _objectTemplates through EnsureFbxObjectCreated

            this.fbxRoot.Nodes.Add(this.nodeFBXHeaderExtension());

            if (this.Options.IsBinaryFormat)
            {
                byte[] id = new byte[16];
                Random random = new Random();
                random.NextBytes(id);
                this.fbxRoot.Add("FileId", id);
                this.fbxRoot.Add("CreationTime", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss:fff", CultureInfo.InvariantCulture));
                this.fbxRoot.Add("Creator", $"MeshIO.FBX {this.MeshIOVersion}");
            }

            this.fbxRoot.Nodes.Add(this.nodeGlobalSettings());
            this.fbxRoot.Nodes.Add(this.nodeDocuments());
            this.fbxRoot.Nodes.Add(this.nodeReferences());
            this.fbxRoot.Nodes.Add(this.nodeDefinitions()); // Must be called after initializeRoot populates _definedObjects
            this.fbxRoot.Nodes.Add(this.nodeObjects());
            this.fbxRoot.Nodes.Add(this.nodeConnections());

            return this.fbxRoot;
        }

        public bool TryGetPropertyTemplate(string fbxName, out FbxPropertyTemplate template)
        {
            return this._tempaltes.TryGetValue(fbxName, out template);
        }

        /// <summary>
        /// Ensures that an FBX object template for the given Element3D is created,
        /// added to the internal tracking dictionaries (_objectTemplates, _definedObjects),
        /// and its children are processed. This is the primary method for building the object hierarchy.
        /// </summary>
        public void EnsureFbxObjectCreated(Element3D element)
        {
            if (element == null || !element.Id.HasValue)
            {
                // Console.WriteLine($"Warning: EnsureFbxObjectCreated skipped for null element or element without ID.");
                return;
            }

            ulong elementId = element.Id.Value;

            // If already fully processed and in _objectTemplates, nothing more to do.
            if (_objectTemplates.ContainsKey(elementId))
            {
                return;
            }

            // Prevent recursion: if we are already in the process of creating this specific element, stop.
            if (_currentlyProcessingForCreation.Contains(elementId))
            {
                // Console.WriteLine($"Debug: EnsureFbxObjectCreated - Recursion prevented for element ID {elementId} ({element.Name}).");
                return;
            }
            _currentlyProcessingForCreation.Add(elementId);
            // Console.WriteLine($"Debug: EnsureFbxObjectCreated - Starting processing for element ID {elementId} ({element.Name}, Type: {element.GetType().Name}).");


            IFbxObjectTemplate objTemplate = FbxTemplateFactory.Create(element);
            if (objTemplate == null)
            {
                // Console.WriteLine($"Warning: No FBX template found for element type {element.GetType().FullName}. Element: {element.Name}, ID: {elementId}");
                _currentlyProcessingForCreation.Remove(elementId);
                return;
            }

            // Add to _objectTemplates so it's available for connections *during* its own ProcessChildren call.
            _objectTemplates.Add(elementId, objTemplate);

            // Add to _definedObjects for the "Definitions" section.
            if (!_definedObjects.TryGetValue(objTemplate.FbxObjectName, out List<IFbxObjectTemplate> fbxObjectList))
            {
                fbxObjectList = new List<IFbxObjectTemplate>();
                _definedObjects.Add(objTemplate.FbxObjectName, fbxObjectList);
            }
            if (!fbxObjectList.Contains(objTemplate)) // Ensure not to add duplicates if called multiple times
            {
                fbxObjectList.Add(objTemplate);
            }

            // Console.WriteLine($"  Added to _objectTemplates and _definedObjects: '{element.Name}' (ID: {elementId}), FbxObjectName: {objTemplate.FbxObjectName}");

            // Recursively process children of this element. This will call EnsureFbxObjectCreated for children.
            objTemplate.ProcessChildren(this);

            // Console.WriteLine($"Debug: EnsureFbxObjectCreated - Finished processing for element ID {elementId} ({element.Name}).");
            _currentlyProcessingForCreation.Remove(elementId);
        }


        /// <summary>
        /// Creates a hierarchical connection (e.g., Node to child Node/Bone).
        /// Assumes the parentFbxTemplate is already known and being processed.
        /// Ensures the childElement is created as an FBX object.
        /// </summary>
        public void CreateHierarchicalConnection(Element3D childElement, IFbxObjectTemplate parentFbxTemplate)
        {
            if (childElement == null || !childElement.Id.HasValue || parentFbxTemplate == null)
            {
                // Console.WriteLine($"Warning: CreateHierarchicalConnection skipped due to null child/parent or missing ID. Child: {childElement?.Name}, Parent: {parentFbxTemplate?.Name}");
                return;
            }
            // Console.WriteLine($"CreateHierarchicalConnection: Child='{childElement.Name}' (ID:{childElement.Id.Value}), Parent='{parentFbxTemplate.Name}' (ID:{parentFbxTemplate.Id})");


            EnsureFbxObjectCreated(childElement); // This will create childFbx if not exists and process its children.

            if (_objectTemplates.TryGetValue(childElement.Id.Value, out IFbxObjectTemplate childFbx))
            {
                // Console.WriteLine($"  Found childFbx: {childFbx.Name}, ParentFbx: {parentFbxTemplate.Name}");
                FbxConnection conn = new FbxConnection(childFbx, parentFbxTemplate);
                this._connections.Add(conn);
                // Console.WriteLine($"    Added OO Connection: {childFbx.Name} ({childFbx.Id}) -> {parentFbxTemplate.Name} ({parentFbxTemplate.Id})");
            }
            else
            {
                // Console.WriteLine($"Error: CreateHierarchicalConnection - Child FBX template for '{childElement.Name}' (ID {childElement.Id.Value}) not found after Ensure. Parent: {parentFbxTemplate.Name}");
            }
        }

        /// <summary>
        /// Creates an Object-Object (OO) connection between two arbitrary Element3D instances.
        /// Ensures both elements are created as FBX objects before attempting to connect.
        /// </summary>
        public void AddConnectionOO(Element3D childElement, Element3D parentElement)
        {
            if (childElement == null || !childElement.Id.HasValue || parentElement == null || !parentElement.Id.HasValue)
            {
                // Console.WriteLine($"Warning: AddConnectionOO skipped due to null element or ID. Child: {childElement?.Name}, Parent: {parentElement?.Name}");
                return;
            }
            // Console.WriteLine($"AddConnectionOO: Child='{childElement.Name}' (ID:{childElement.Id.Value}), Parent='{parentElement.Name}' (ID:{parentElement.Id.Value})");


            EnsureFbxObjectCreated(childElement);
            EnsureFbxObjectCreated(parentElement);

            if (_objectTemplates.TryGetValue(childElement.Id.Value, out IFbxObjectTemplate childFbx) &&
                _objectTemplates.TryGetValue(parentElement.Id.Value, out IFbxObjectTemplate parentFbx))
            {
                // Console.WriteLine($"  Found childFbx: {childFbx.Name}, parentFbx: {parentFbx.Name}");
                FbxConnection conn = new FbxConnection(childFbx, parentFbx);
                this._connections.Add(conn);
                // Console.WriteLine($"    Added OO Connection: {childFbx.Name} ({childFbx.Id}) -> {parentFbx.Name} ({parentFbx.Id})");
            }
            else
            {
                // string childStatus = _objectTemplates.ContainsKey(childElement.Id.Value) ? "found" : "NOT FOUND";
                // string parentStatus = _objectTemplates.ContainsKey(parentElement.Id.Value) ? "found" : "NOT FOUND";
                // Console.WriteLine($"Error: AddConnectionOO - Could not create connection. Child '{childElement.Name}' ({childElement.Id.Value}) status: {childStatus}. Parent '{parentElement.Name}' ({parentElement.Id.Value}) status: {parentStatus}.");
            }
        }


        protected void initializeRoot()
        {
            // Root node should be processed to create the connections but it is not written in the file with ID 0 in Objects.
            // It is referenced by ID 0 in the "Documents" section.
            this.RootNode.Id = 0; // Special ID for scene root node.
            EnsureFbxObjectCreated(this.RootNode);
        }


        private FbxNode nodeFBXHeaderExtension()
        {
            FbxNode header = new FbxNode(FbxFileToken.FBXHeaderExtension);

            header.Nodes.Add(new FbxNode(FbxFileToken.FBXHeaderVersion, 1003));
            header.Nodes.Add(new FbxNode("FBXVersion", (int)this.Version));

            if (this.Options.IsBinaryFormat)
            {
                header.Add("EncryptionType", 0);
            }

            DateTime now = DateTime.Now;
            FbxNode tiemespan = new FbxNode(FbxFileToken.CreationTimeStamp);
            tiemespan.Nodes.Add(new FbxNode(FbxFileToken.Version, 1000));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Year), now.Year));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Month), now.Month));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Day), now.Day));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Hour), now.Hour));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Minute), now.Minute));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Second), now.Second));
            tiemespan.Nodes.Add(new FbxNode(nameof(now.Millisecond), now.Millisecond));
            header.Nodes.Add(tiemespan);

            header.Add(FbxFileToken.Creator, $"MeshIO.FBX {this.MeshIOVersion}");

            // SceneInfo can be added here if needed
            // FbxNode sceneInfo = header.Add("SceneInfo", $"GlobalInfo::{this.Scene.Name}", "UserData");
            // sceneInfo.Add("Type", "UserData");
            // sceneInfo.Add("Version", 100);
            // FbxNode metadata = sceneInfo.Add("MetaData");
            // metadata.Add("Version", 100);
            // metadata.Add("Title", this.Scene.Name ?? "Untitled Scene");
            // metadata.Add("Subject", "");
            // metadata.Add("Author", "MeshIO User");
            // metadata.Add("Keywords", "");
            // metadata.Add("Revision", "1.0");
            // metadata.Add("Comment", $"Exported with MeshIO.FBX {this.MeshIOVersion}");

            return header;
        }

        private FbxNode nodeGlobalSettings()
        {
            // Check if GlobalSettings has already been added to _definedObjects (e.g., by a custom setup)
            if (!_definedObjects.TryGetValue(FbxFileToken.GlobalSettings, out var globalSettingsList) || !globalSettingsList.Any())
            {
                FbxGlobalSettingsTemplate globalSettingsTemplate = new FbxGlobalSettingsTemplate();
                if (globalSettingsList == null)
                {
                    globalSettingsList = new List<IFbxObjectTemplate>();
                    _definedObjects.Add(FbxFileToken.GlobalSettings, globalSettingsList);
                }
                globalSettingsList.Add(globalSettingsTemplate); // Add to defined objects so it's counted in Definitions
            }

            // Retrieve the (potentially user-modified) global settings properties
            var globalSettingsInstance = _definedObjects[FbxFileToken.GlobalSettings].First() as FbxGlobalSettingsTemplate;
            var propertiesToUse = globalSettingsInstance?.FbxProperties ?? new FbxGlobalSettingsTemplate().FbxProperties;


            FbxNode settings = new FbxNode(FbxFileToken.GlobalSettings);
            settings.Nodes.Add(new FbxNode(FbxFileToken.Version, 1000)); // Common version for GlobalSettings
            settings.Nodes.Add(this.PropertiesToNode(propertiesToUse));

            return settings;
        }

        private FbxNode nodeDocuments()
        {
            FbxNode documents = new FbxNode(FbxFileToken.Documents);
            documents.Nodes.Add(new FbxNode(FbxFileToken.Count, this.Scene.SubScenes.Count + 1));

            // Scene.GetIdOrDefault() will generate an ID if null, but for root, we use 0.
            // The main scene document uses the scene's actual ID if set, or a generated one.
            // The RootNode property of the document refers to the scene's root node ID (which is 0).
            var sceneId = this.Scene.Id.HasValue ? (long)this.Scene.Id.Value : this.Scene.GetIdOrDefault();
            var doc = documents.Add(FbxFileToken.Document, sceneId, this.Scene.Name ?? "Scene", FbxFileToken.Scene); // FBX SubType
            doc.Add(FbxFileToken.RootNode, (long)0); // Always 0 for the main scene document's root.

            // TODO: Handle SubScenes if any
            // foreach (Scene subScene in this.Scene.SubScenes) { ... }

            return documents;
        }

        private FbxNode nodeReferences()
        {
            FbxNode references = new FbxNode(FbxFileToken.References);
            // Typically empty for simple exports
            // references.Nodes.Add(null); // An empty node list is represented by adding a null
            return references;
        }

        private FbxNode nodeDefinitions()
        {
            FbxNode definitions = new FbxNode(FbxFileToken.Definitions);

            definitions.Nodes.Add(new FbxNode(FbxFileToken.Version, 100));
            definitions.Nodes.Add(new FbxNode(FbxFileToken.Count, this._definedObjects.Count)); // Count of ObjectType entries

            foreach (var item in this._definedObjects) // item.Key is FbxObjectName (e.g., "Model", "Geometry")
            {
                FbxNode objectTypeNode = new FbxNode(FbxFileToken.ObjectType, item.Key);
                objectTypeNode.Nodes.Add(new FbxNode(FbxFileToken.Count, item.Value.Count)); // Number of actual objects of this type

                // GlobalSettings and Deformers (Skin, Cluster) don't have a "PropertyTemplate" sub-node in Definitions.
                if (item.Key == FbxFileToken.GlobalSettings || item.Key == FbxFileToken.Deformer)
                {
                    // No PropertyTemplate for these.
                }
                else
                {
                    // For Model, Geometry, Material, etc., create or get their template.
                    if (!this._tempaltes.TryGetValue(item.Key, out FbxPropertyTemplate template))
                    {
                        template = FbxPropertyTemplate.Create(item.Key); // Create default template
                        this._tempaltes.Add(item.Key, template);
                    }

                    var propertyTemplateNode = new FbxNode("PropertyTemplate", template.Name); // e.g., "FbxNode", "FbxMesh"
                    FbxNode props70Node = this.PropertiesToNode(template.Properties.Values);
                    if (props70Node != null) // PropertiesToNode can return null if no properties
                    {
                        propertyTemplateNode.Nodes.Add(props70Node);
                    }
                    objectTypeNode.Nodes.Add(propertyTemplateNode);
                }
                definitions.Nodes.Add(objectTypeNode);
            }
            return definitions;
        }

        private FbxNode nodeObjects()
        {
            FbxNode objectsNode = new FbxNode(FbxFileToken.Objects);

            // Iterate over _objectTemplates, which should be populated by EnsureFbxObjectCreated
            // The RootNode (ID 0) itself is not written as an object here.
            foreach (IFbxObjectTemplate objTemplate in this._objectTemplates.Values.Where(ot => ot.Id != "0"))
            {
                if (!this._tempaltes.TryGetValue(objTemplate.FbxObjectName, out FbxPropertyTemplate propertyDefTemplate))
                {
                    // For types like Deformer (Skin, Cluster), there's no predefined PropertyTemplate in _tempaltes.
                    // So, create an empty one or handle as appropriate.
                    if (objTemplate.FbxObjectName == FbxFileToken.Deformer)
                    {
                        propertyDefTemplate = new FbxPropertyTemplate(objTemplate.FbxObjectName, objTemplate.FbxTypeName, new Dictionary<string, FbxProperty>());
                    }
                    else
                    {
                        // This case should ideally be covered by nodeDefinitions ensuring _tempaltes is populated.
                        // If we reach here for Model, Geometry, etc., it's likely an issue.
                        // Console.WriteLine($"Warning: Property template not found for FbxObjectName '{objTemplate.FbxObjectName}' during nodeObjects. Creating empty.");
                        propertyDefTemplate = new FbxPropertyTemplate(); // Fallback
                    }
                }

                objTemplate.ApplyTemplate(propertyDefTemplate); // Apply the definition template to the instance
                objectsNode.Nodes.Add(objTemplate.ToFbxNode(this));
            }

            return objectsNode;
        }

        private FbxNode nodeConnections()
        {
            FbxNode connectionsNode = new FbxNode(FbxFileToken.Connections);

            foreach (FbxConnection c in this._connections)
            {
                // Skip connections involving the scene root (ID 0) if it's the parent,
                // as the scene root is not an "object" in the Objects block.
                // However, children can connect TO the root (ID 0).
                // The main scene object in "Documents" has a "RootNode: 0" property.
                if (c.Parent.Id == "0" && c.Child.Id == "0") continue; // Self-connection of root, skip

                FbxNode con = connectionsNode.Add("C");

                switch (c.ConnectionType) // This is currently always ObjectObject from FbxConnection constructor
                {
                    case FbxConnectionType.ObjectObject:
                        con.Properties.Add("OO");
                        break;
                    // Add other cases (OP, PO, PP) if they become relevant
                    default:
                        // Console.WriteLine($"Warning: Unsupported FbxConnectionType '{c.ConnectionType}' found.");
                        con.Properties.Add("OO"); // Default to OO
                        break;
                }

                // Ensure IDs are valid longs.
                if (!long.TryParse(c.Child.Id, out long childIdL) || !long.TryParse(c.Parent.Id, out long parentIdL))
                {
                    // Console.WriteLine($"Error: Invalid ID format for connection. Child: '{c.Child.Id}', Parent: '{c.Parent.Id}'. Skipping connection.");
                    connectionsNode.Nodes.RemoveAt(connectionsNode.Nodes.Count - 1); // Remove the "C" node
                    continue;
                }

                con.Properties.Add(childIdL);
                con.Properties.Add(parentIdL);

                // Optional: Add property name if it's a Property-Object or Object-Property connection
                // if (c.ConnectionType == FbxConnectionType.ObjectProperty || c.ConnectionType == FbxConnectionType.PropertyObject)
                // {
                //     con.Properties.Add(c.PropertyName ?? ""); // Placeholder
                // }
            }

            return connectionsNode;
        }

        public FbxNode PropertiesToNode(IEnumerable<Property> properties)
        {
            if (properties == null || !properties.Any())
            {
                return null; // Return null if there are no properties, so an empty Properties70 node isn't created.
            }

            FbxNode node = new FbxNode(FbxFileToken.GetPropertiesName(this.Version));

            foreach (Property p in properties)
            {
                FbxProperty fbxProp;
                if (p is FbxProperty alreadyFbxProp)
                {
                    fbxProp = alreadyFbxProp;
                }
                else
                {
                    fbxProp = FbxProperty.CreateFrom(p);
                }
                node.Nodes.Add(fbxProp.ToNode());
            }

            return node;
        }
    }
}