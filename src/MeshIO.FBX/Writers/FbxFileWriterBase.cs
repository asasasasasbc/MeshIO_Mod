using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MeshIO.FBX.Connections;
using MeshIO.FBX.Templates;
using MeshIO.Entities.Skinning;
using MeshIO.Entities.Geometries;
using MeshIO.Entities;
using MeshIO.Utils;
using CSMath; // For Matrix4

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
        private readonly HashSet<ulong> _currentlyProcessingForCreation = new HashSet<ulong>();

        protected FbxFileWriterBase(Scene scene, FbxWriterOptions options)
        {
            this.Scene = scene;
            this.Options = options;
            this.fbxRoot = new FbxRootNode { Version = this.Options.Version };
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
            this.initializeRoot();

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
            this.fbxRoot.Nodes.Add(this.nodeDefinitions());
            this.fbxRoot.Nodes.Add(this.nodePoses());
            this.fbxRoot.Nodes.Add(this.nodeObjects());
            this.fbxRoot.Nodes.Add(this.nodeConnections());
            return this.fbxRoot;
        }

        public bool TryGetPropertyTemplate(string fbxName, out FbxPropertyTemplate template)
        {
            return this._tempaltes.TryGetValue(fbxName, out template);
        }

        public void EnsureFbxObjectCreated(Element3D element)
        {
            if (element == null || !element.Id.HasValue) return;
            ulong elementId = element.Id.Value;
            if (_objectTemplates.ContainsKey(elementId)) return;
            if (_currentlyProcessingForCreation.Contains(elementId)) return;
            _currentlyProcessingForCreation.Add(elementId);
            IFbxObjectTemplate objTemplate = FbxTemplateFactory.Create(element);
            if (objTemplate == null)
            {
                _currentlyProcessingForCreation.Remove(elementId);
                return;
            }
            _objectTemplates.Add(elementId, objTemplate);
            if (!_definedObjects.TryGetValue(objTemplate.FbxObjectName, out List<IFbxObjectTemplate> fbxObjectList))
            {
                fbxObjectList = new List<IFbxObjectTemplate>();
                _definedObjects.Add(objTemplate.FbxObjectName, fbxObjectList);
            }
            if (!fbxObjectList.Contains(objTemplate)) fbxObjectList.Add(objTemplate);
            objTemplate.ProcessChildren(this);
            _currentlyProcessingForCreation.Remove(elementId);
        }

        // Generic internal method to add connections
        // childElement_FBXSource becomes FBX Source, parentElement_FBXDestination becomes FBX Destination
        private void AddConnection(Element3D childElement_FBXSource, Element3D parentElement_FBXDestination, FbxConnectionType type, string propertyName = null)
        {
            if (childElement_FBXSource == null || !childElement_FBXSource.Id.HasValue ||
                parentElement_FBXDestination == null || !parentElement_FBXDestination.Id.HasValue)
            {
                // Console.WriteLine($"Warning: AddConnection ({type}) skipped due to null element or ID. Child(Source): {childElement_FBXSource?.Name}, Parent(Dest): {parentElement_FBXDestination?.Name}");
                return;
            }

            EnsureFbxObjectCreated(childElement_FBXSource);
            EnsureFbxObjectCreated(parentElement_FBXDestination);

            if (_objectTemplates.TryGetValue(childElement_FBXSource.Id.Value, out IFbxObjectTemplate childFbx_SourceTemplate) &&
                _objectTemplates.TryGetValue(parentElement_FBXDestination.Id.Value, out IFbxObjectTemplate parentFbx_DestinationTemplate))
            {
                _connections.Add(new FbxConnection(childFbx_SourceTemplate, parentFbx_DestinationTemplate, type, propertyName));
                // Console.WriteLine($"    Added Connection ({type}): {childFbx_SourceTemplate.Name} ({childFbx_SourceTemplate.Id}) [Source] -> {parentFbx_DestinationTemplate.Name} ({parentFbx_DestinationTemplate.Id}) [Dest]");
            }
            else
            {
                // string childStatus = _objectTemplates.ContainsKey(childElement_FBXSource.Id.Value) ? "found" : "NOT FOUND";
                // string parentStatus = _objectTemplates.ContainsKey(parentElement_FBXDestination.Id.Value) ? "found" : "NOT FOUND";
                // Console.WriteLine($"Error: AddConnection ({type}) - Could not create. Child '{childElement_FBXSource.Name}' ({childElement_FBXSource.Id.Value}) status: {childStatus}. Parent '{parentElement_FBXDestination.Name}' ({parentElement_FBXDestination.Id.Value}) status: {parentStatus}.");
            }
        }

        public void CreateHierarchicalConnection(Element3D childNodeElement, IFbxObjectTemplate parentNodeFbxTemplate)
        {
            if (childNodeElement == null || !childNodeElement.Id.HasValue || parentNodeFbxTemplate == null) return;
            EnsureFbxObjectCreated(childNodeElement);
            if (_objectTemplates.TryGetValue(childNodeElement.Id.Value, out IFbxObjectTemplate childFbx))
            {
                // Hierarchical: Child Node (Source) -> Parent Node (Destination)
                _connections.Add(new FbxConnection(childFbx, parentNodeFbxTemplate, FbxConnectionType.ObjectObject));
            }
        }

        // Standard Object-Object (e.g., Model -> Geometry, Model -> Material)
        // childElement_FBXSource (Source) is logically "contained by" or "uses" parentElement_FBXDestination (Destination)
        public void AddConnectionOO_ChildToParent(Element3D childElement_FBXSource, Element3D parentElement_FBXDestination)
        {
            AddConnection(childElement_FBXSource, parentElement_FBXDestination, FbxConnectionType.ObjectObject);
        }

        // Specific connection for Skin -> Model (Model is deformed by Skin) - REMOVED as per refined spec
        // public void AddConnectionDeformer_SkinToModel(Element3D skinElement_FBXSource, Element3D modelElement_FBXDestination)
        // {
        // 	AddConnection(skinElement_FBXSource, modelElement_FBXDestination, FbxConnectionType.Deformer);
        // }

        // Specific connection for Skin -> Mesh (Skin deforms Mesh)
        // FBX: C: "Deformer", <Skin_ID_Source>, <Mesh_ID_Destination>
        public void AddConnectionDeformer_SkinToMesh(Element3D skinElement_FBXSource, Element3D meshElement_FBXDestination)
        {
            AddConnection(skinElement_FBXSource, meshElement_FBXDestination, FbxConnectionType.Deformer);
        }

        // Specific connection for Cluster -> Skin (Skin has Cluster)
        // FBX: C: "SubDeformer", <Cluster_ID_Source>, <Skin_ID_Destination>
        public void AddConnectionSubDeformer_ClusterToSkin(Element3D clusterElement_FBXSource, Element3D skinElement_FBXDestination)
        {
            AddConnection(clusterElement_FBXSource, skinElement_FBXDestination, FbxConnectionType.SubDeformer);
        }

        // Specific connection for Cluster -> Bone
        // FBX: C: "OO", <Cluster_ID_Source>, <Bone_ID_Destination>
        public void AddConnectionOO_ClusterToBone(Element3D clusterElement_FBXSource, Element3D boneElement_FBXDestination)
        {
            AddConnection(clusterElement_FBXSource, boneElement_FBXDestination, FbxConnectionType.ObjectObject);
        }


        protected void initializeRoot()
        {
            this.RootNode.Id = 0;
            EnsureFbxObjectCreated(this.RootNode);
        }

        private FbxNode nodeFBXHeaderExtension()
        {
            FbxNode header = new FbxNode(FbxFileToken.FBXHeaderExtension);
            header.Nodes.Add(new FbxNode(FbxFileToken.FBXHeaderVersion, 1003));
            header.Nodes.Add(new FbxNode("FBXVersion", (int)this.Version));
            if (this.Options.IsBinaryFormat) header.Add("EncryptionType", 0);
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
            return header;
        }

        private FbxNode nodeGlobalSettings()
        {
            if (!_definedObjects.TryGetValue(FbxFileToken.GlobalSettings, out var globalSettingsList) || !globalSettingsList.Any())
            {
                FbxGlobalSettingsTemplate globalSettingsTemplate = new FbxGlobalSettingsTemplate();
                if (globalSettingsList == null) globalSettingsList = new List<IFbxObjectTemplate>();
                _definedObjects.Add(FbxFileToken.GlobalSettings, globalSettingsList);
                globalSettingsList.Add(globalSettingsTemplate);
            }
            var globalSettingsInstance = _definedObjects[FbxFileToken.GlobalSettings].First() as FbxGlobalSettingsTemplate;
            var propertiesToUse = globalSettingsInstance?.FbxProperties ?? new FbxGlobalSettingsTemplate().FbxProperties;
            FbxNode settings = new FbxNode(FbxFileToken.GlobalSettings);
            settings.Nodes.Add(new FbxNode(FbxFileToken.Version, 1000));
            settings.Nodes.Add(this.PropertiesToNode(propertiesToUse));
            return settings;
        }

        private FbxNode nodeDocuments()
        {
            FbxNode documents = new FbxNode(FbxFileToken.Documents);
            documents.Nodes.Add(new FbxNode(FbxFileToken.Count, this.Scene.SubScenes.Count + 1));
            var sceneId = this.Scene.Id.HasValue ? (long)this.Scene.Id.Value : this.Scene.GetIdOrDefault();
            var doc = documents.Add(FbxFileToken.Document, sceneId, this.Scene.Name ?? "Scene", FbxFileToken.Scene);
            doc.Add(FbxFileToken.RootNode, (long)0);
            return documents;
        }

        private FbxNode nodeReferences()
        {
            return new FbxNode(FbxFileToken.References);
        }

        private FbxNode nodeDefinitions()
        {
            FbxNode definitions = new FbxNode(FbxFileToken.Definitions);
            definitions.Nodes.Add(new FbxNode(FbxFileToken.Version, 100));
            definitions.Nodes.Add(new FbxNode(FbxFileToken.Count, this._definedObjects.Count));
            foreach (var item in this._definedObjects)
            {
                FbxNode objectTypeNode = new FbxNode(FbxFileToken.ObjectType, item.Key);
                int countForObjectType = item.Value.Count;

                if (item.Key == FbxFileToken.Pose)
                {
                    FbxNode topLevelPoseNode = this.fbxRoot.Nodes.FirstOrDefault(n => n.Name == FbxFileToken.Pose);
                    countForObjectType = topLevelPoseNode?.Nodes.Count(pn => pn.Name == "Pose") ?? 0;
                }
                objectTypeNode.Nodes.Add(new FbxNode(FbxFileToken.Count, countForObjectType));

                if (item.Key == FbxFileToken.GlobalSettings || item.Key == FbxFileToken.Deformer || item.Key == FbxFileToken.Pose)
                { /* No PropertyTemplate */ }
                else
                {
                    if (!this._tempaltes.TryGetValue(item.Key, out FbxPropertyTemplate template))
                    {
                        template = FbxPropertyTemplate.Create(item.Key);
                        this._tempaltes.Add(item.Key, template);
                    }
                    var propertyTemplateNode = new FbxNode("PropertyTemplate", template.Name);
                    FbxNode props70Node = this.PropertiesToNode(template.Properties.Values);
                    if (props70Node != null) propertyTemplateNode.Nodes.Add(props70Node);
                    objectTypeNode.Nodes.Add(propertyTemplateNode);
                }
                definitions.Nodes.Add(objectTypeNode);
            }
            return definitions;
        }

        protected Matrix4 CalculateWorldMatrix(Node node)
        {
            if (node == null) return Matrix4.Identity;

            Matrix4 worldMatrix = node.Transform.Matrix;

            // This relies on Node.Parent being correctly populated.
            // The original MeshIO.Node.Parent is { get; } and not set externally.
            // This needs to be handled in the core MeshIO library or by manually setting Parent in tests.
            Element3D currentAsElement = node.Parent;

            List<Matrix4> parentMatrices = new List<Matrix4>();
            while (currentAsElement != null && currentAsElement is Node parentNode && parentNode != this.Scene.RootNode /* Stop at scene root */)
            {
                parentMatrices.Add(parentNode.Transform.Matrix);
                currentAsElement = parentNode.Parent;
            }

            parentMatrices.Reverse(); // Apply from root-most parent down to immediate parent

            foreach (var pMat in parentMatrices)
            {
                worldMatrix = pMat * worldMatrix;
            }
            return worldMatrix;
        }


        private FbxNode nodePoses()
        {
            FbxNode posesNode = new FbxNode(FbxFileToken.Pose);
            int poseCount = 0;
            HashSet<Node> bonesInPose = new HashSet<Node>();

            // Gather all bones that are part of the scene's object templates
            foreach (var objTemplate in _objectTemplates.Values)
            {
                if (objTemplate.GetElement() is Bone bone)
                {
                    bonesInPose.Add(bone);
                }
            }
            // Also ensure bones linked by clusters are definitely in.
            foreach (var objTemplate in _objectTemplates.Values)
            {
                if (objTemplate.GetElement() is Skin skin)
                {
                    foreach (Cluster cluster in skin.Clusters)
                    {
                        if (cluster.Link != null && cluster.Link.Id.HasValue && _objectTemplates.ContainsKey(cluster.Link.Id.Value))
                        {
                            bonesInPose.Add(cluster.Link);
                        }
                    }
                }
            }

            if (bonesInPose.Any())
            {
                poseCount = 1;
                long poseNodeId = Scene.GetIdOrDefault();
                unchecked { poseNodeId = (poseNodeId == 0 ? 1 : poseNodeId) * 31 + 1001; }


                FbxNode bindPose = posesNode.Add("Pose", poseNodeId, "BindPose", "BindPose");
                bindPose.Add("Type", "BindPose");
                bindPose.Add("NbPoseNodes", bonesInPose.Count);

                foreach (Node boneNode in bonesInPose)
                {
                    if (!boneNode.Id.HasValue || !_objectTemplates.ContainsKey(boneNode.Id.Value)) continue;
                    FbxNode poseNodeEntry = bindPose.Add("PoseNode");
                    poseNodeEntry.Add("Node", (long)boneNode.Id.Value);

                    Matrix4 boneWorldBindMatrix = Matrix4.Identity;
                    Cluster foundCluster = _objectTemplates.Values
                        .Select(ot => ot.GetElement()).OfType<Skin>()
                        .SelectMany(s => s.Clusters).FirstOrDefault(c => c.Link == boneNode);

                    if (foundCluster != null) boneWorldBindMatrix = foundCluster.TransformMatrix;
                    else boneWorldBindMatrix = CalculateWorldMatrix(boneNode);

                    poseNodeEntry.Add("Matrix", boneWorldBindMatrix.ToRowMajorArray());
                }
            }

            if (posesNode.Nodes.Any(n => n.Name == "Pose"))
            {
                posesNode.Nodes.Insert(0, new FbxNode("Count", poseCount));
            }
            else
            {
                posesNode.Add("Count", 0);
            }

            if (poseCount > 0)
            {
                if (!_definedObjects.ContainsKey(FbxFileToken.Pose))
                {
                    _definedObjects.Add(FbxFileToken.Pose, new List<IFbxObjectTemplate>());
                }
            }
            return posesNode;
        }

        private FbxNode nodeObjects()
        {
            FbxNode objectsNode = new FbxNode(FbxFileToken.Objects);
            foreach (IFbxObjectTemplate objTemplate in this._objectTemplates.Values.Where(ot => ot.Id != "0"))
            {
                if (!this._tempaltes.TryGetValue(objTemplate.FbxObjectName, out FbxPropertyTemplate propertyDefTemplate))
                {
                    if (objTemplate.FbxObjectName == FbxFileToken.Deformer || objTemplate.FbxObjectName == FbxFileToken.Pose)
                        propertyDefTemplate = new FbxPropertyTemplate(objTemplate.FbxObjectName, objTemplate.FbxTypeName, new Dictionary<string, FbxProperty>());
                    else propertyDefTemplate = new FbxPropertyTemplate();
                }
                objTemplate.ApplyTemplate(propertyDefTemplate);
                objectsNode.Nodes.Add(objTemplate.ToFbxNode(this));
            }
            return objectsNode;
        }

        private FbxNode nodeConnections()
        {
            FbxNode connectionsNode = new FbxNode(FbxFileToken.Connections);
            foreach (FbxConnection c in this._connections)
            {
                if (c.ParentId == "0" && c.ChildId == "0") continue;
                FbxNode con = connectionsNode.Add("C");

                con.Properties.Add(c.GetTypeString());

                if (!long.TryParse(c.ChildId, out long childIdL) || !long.TryParse(c.ParentId, out long parentIdL))
                {
                    connectionsNode.Nodes.RemoveAt(connectionsNode.Nodes.Count - 1);
                    continue;
                }
                con.Properties.Add(childIdL); // FBX Source ID (FbxConnection.ChildId)
                con.Properties.Add(parentIdL);  // FBX Destination ID (FbxConnection.ParentId)

                if (c.ConnectionType == FbxConnectionType.ObjectProperty && !string.IsNullOrEmpty(c.PropertyName))
                {
                    con.Properties.Add(c.PropertyName);
                }
            }
            return connectionsNode;
        }

        public FbxNode PropertiesToNode(IEnumerable<Property> properties)
        {
            if (properties == null || !properties.Any()) return null;
            FbxNode node = new FbxNode(FbxFileToken.GetPropertiesName(this.Version));
            foreach (Property p in properties)
            {
                FbxProperty fbxProp = (p is FbxProperty alreadyFbxProp) ? alreadyFbxProp : FbxProperty.CreateFrom(p);
                node.Nodes.Add(fbxProp.ToNode());
            }
            return node;
        }
    }
}