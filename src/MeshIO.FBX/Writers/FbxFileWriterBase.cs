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

        public void EnsureFbxObjectCreatedInternal(IFbxObjectTemplate objTemplate)
        {
            // Check if already exists by ID (if IFbxObjectTemplate guarantees unique ID string)
            // This simple check by value might be okay if templates are unique instances.
            if (_objectTemplates.Values.Any(ot => ot.Id == objTemplate.Id)) return;

            // Assuming objTemplate.Id is a string that can be parsed to ulong for the main dictionary key
            // For NodeAttribute, its ID is generated and unique.
            _objectTemplates.Add(ulong.Parse(objTemplate.Id), objTemplate);

            if (!_definedObjects.TryGetValue(objTemplate.FbxObjectName, out List<IFbxObjectTemplate> fbxObjectList))
            {
                fbxObjectList = new List<IFbxObjectTemplate>();
                _definedObjects.Add(objTemplate.FbxObjectName, fbxObjectList);
            }
            if (!fbxObjectList.Contains(objTemplate))
            {
                fbxObjectList.Add(objTemplate);
            }
            // objTemplate.ProcessChildren(this); // NodeAttributes don't have children to process typically
        }

        // New connection method for template-to-template (e.g., NodeAttribute to Model)
        public void AddConnectionOO_TemplateToTemplate(IFbxObjectTemplate childFbx_SourceTemplate, IFbxObjectTemplate parentFbx_DestinationTemplate)
        {
            if (childFbx_SourceTemplate == null || parentFbx_DestinationTemplate == null) return;
            _connections.Add(new FbxConnection(childFbx_SourceTemplate, parentFbx_DestinationTemplate, FbxConnectionType.ObjectObject));
        }

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
            // ... (else error logging) ...
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

        // Specific connection for Skin -> Mesh (Skin deforms Mesh)
        // OLD FBX: C: "Deformer", <Skin_ID_Source>, <Mesh_ID_Destination>
        // NEW FBX: C: "OO", <Skin_ID_Source>, <Mesh_ID_Destination>
        public void AddConnectionDeformer_SkinToMesh(Element3D skinElement_FBXSource, Element3D meshElement_FBXDestination)
        {
            // Changed FbxConnectionType.Deformer to FbxConnectionType.ObjectObject
            AddConnection(skinElement_FBXSource, meshElement_FBXDestination, FbxConnectionType.ObjectObject);
        }

        // Specific connection for Cluster -> Skin (Skin has Cluster)
        // OLD FBX: C: "SubDeformer", <Cluster_ID_Source>, <Skin_ID_Destination>
        // NEW FBX: C: "OO", <Cluster_ID_Source>, <Skin_ID_Destination>
        public void AddConnectionSubDeformer_ClusterToSkin(Element3D clusterElement_FBXSource, Element3D skinElement_FBXDestination)
        {
            // Changed FbxConnectionType.SubDeformer to FbxConnectionType.ObjectObject
            AddConnection(clusterElement_FBXSource, skinElement_FBXDestination, FbxConnectionType.ObjectObject);
        }
        // Specific connection for Bone -> Cluster
        // OLD FBX: C: "Deformer", <Cluster_ID_Source>, <Bone_ID_Destination>  (Cluster linked to Bone)
        // NEW FBX: C: "OO", <Bone_ID_Source>, <Cluster_ID_Destination> (Bone owns/links to Cluster)
        public void AddConnectionDeformer_ClusterToBone(Element3D clusterElement_FBXDestination, Element3D boneElement_FBXSource) // Parameters swapped for clarity
        {
            // Changed FbxConnectionType.Deformer to FbxConnectionType.ObjectObject
            // Swapped order: boneElement_FBXSource is now the FBX Source, clusterElement_FBXDestination is the FBX Destination
            AddConnection(boneElement_FBXSource, clusterElement_FBXDestination, FbxConnectionType.ObjectObject);
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

            // Ensure NodeAttribute is in _definedObjects if any NodeAttributes were created
            if (_objectTemplates.Values.Any(ot => ot is FbxNodeAttributeTemplate) && !_definedObjects.ContainsKey("NodeAttribute"))
            {
                _definedObjects.Add("NodeAttribute", _objectTemplates.Values.Where(ot => ot is FbxNodeAttributeTemplate).ToList());
            }

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

                // NodeAttribute, Deformer, GlobalSettings, Pose don't have a PropertyTemplate in Definitions
                if (item.Key == FbxFileToken.GlobalSettings ||
                    item.Key == FbxFileToken.Deformer ||
                    item.Key == FbxFileToken.Pose ||
                    item.Key == "NodeAttribute") // Added NodeAttribute here
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

            Element3D currentAsElement = node.Parent;

            List<Matrix4> parentMatrices = new List<Matrix4>();
            while (currentAsElement != null && currentAsElement is Node parentNode && parentNode != this.Scene.RootNode /* Stop at scene root */)
            {
                parentMatrices.Add(parentNode.Transform.Matrix);
                currentAsElement = parentNode.Parent; // This requires Node.Parent to be correctly set.
            }

            parentMatrices.Reverse();

            foreach (var pMat in parentMatrices)
            {
                worldMatrix = pMat * worldMatrix;
            }
            return worldMatrix;
        }

        // In MeshIO.FBX/Writers/FbxFileWriterBase.cs

        private FbxNode nodePoses()
        {
            FbxNode posesNode = new FbxNode(FbxFileToken.Pose);
            int poseCount = 0;
            HashSet<Node> nodesInBindPose = new HashSet<Node>();
            Node skinnedModelNode = null; // The Node instance representing the mesh/model being skinned
            Skin skinDeformerInstance = null; // The Skin instance

            // First, find the skinned model node and its skin deformer.
            // This assumes there's at most one primary skinned model with a Skin entity for this logic.
            // If multiple independent skinned models exist, this part might need to be more sophisticated
            // or the concept of a single "BindPose" might need to be re-evaluated (e.g. multiple Pose nodes).
            foreach (var objTemplate in _objectTemplates.Values)
            {
                if (objTemplate.GetElement() is Node modelNode)
                {
                    foreach (var entity in modelNode.Entities)
                    {
                        if (entity is Skin skin && skin.DeformedGeometry != null)
                        {
                            skinnedModelNode = modelNode;
                            skinDeformerInstance = skin;
                            nodesInBindPose.Add(skinnedModelNode); // Add the mesh node itself
                                                                   // Console.WriteLine($"DEBUG: Added skinnedModelNode to BindPose: {skinnedModelNode.Name} (ID: {skinnedModelNode.Id})");
                            break; // Found the primary skinned model
                        }
                    }
                }
                if (skinnedModelNode != null) break; // Stop searching once found
            }

            // If a skin deformer was found, add all bones (and their ancestors up to the scene root)
            // that are linked by its clusters.
            if (skinDeformerInstance != null)
            {
                // Console.WriteLine($"DEBUG: Processing skinDeformerInstance: {skinDeformerInstance.Name}");
                foreach (Cluster cluster in skinDeformerInstance.Clusters)
                {
                    if (cluster.Link != null) // cluster.Link is a MeshIO.Node (specifically a Bone or its parent)
                    {
                        // Console.WriteLine($"DEBUG: Processing cluster.Link: {cluster.Link.Name} (ID: {cluster.Link.Id})");
                        Node currentBoneOrParent = cluster.Link;
                        while (currentBoneOrParent != null && currentBoneOrParent != this.Scene.RootNode)
                        {
                            if (nodesInBindPose.Add(currentBoneOrParent)) // Add returns true if item was added
                            {
                                // Console.WriteLine($"DEBUG: Added Bone/Ancestor to BindPose: {currentBoneOrParent.Name} (ID: {currentBoneOrParent.Id})");
                            }

                            if (currentBoneOrParent.Parent is Node parentNode)
                            {
                                currentBoneOrParent = parentNode;
                            }
                            else
                            {
                                break; // No more parents or parent is not a Node
                            }
                        }
                    }
                }
            }
            // As a fallback or for non-skinned armatures, one might add all existing bones.
            // However, for a "BindPose" related to skinning, the above logic is more targeted.
            // If you need to support general bind poses for non-skinned hierarchies, you might add:
            else
            {
                // Console.WriteLine("DEBUG: No skinDeformerInstance found. Looking for any bones.");
                foreach (var objTemplate in _objectTemplates.Values)
                {
                    if (objTemplate.GetElement() is Bone bone) // Check if it's specifically a Bone type
                    {
                        // Console.WriteLine($"DEBUG: Found loose Bone: {bone.Name} (ID: {bone.Id})");
                        Node currentBoneOrParent = bone;
                        while (currentBoneOrParent != null && currentBoneOrParent != this.Scene.RootNode)
                        {
                            if (nodesInBindPose.Add(currentBoneOrParent))
                            {
                                // Console.WriteLine($"DEBUG: Added Bone/Ancestor (no skin): {currentBoneOrParent.Name} (ID: {currentBoneOrParent.Id})");
                            }

                            if (currentBoneOrParent.Parent is Node parentNode)
                            {
                                currentBoneOrParent = parentNode;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            if (nodesInBindPose.Any())
            {
                poseCount = 1; // Assuming one "BindPose" for now

                // Generate a somewhat unique ID for the Pose object itself.
                // This ID is for the FBX "Pose" object, not a MeshIO element.
                long poseFbxObjectId = Scene.GetIdOrDefault(); // Start with scene ID or a default
                unchecked // Allow overflow for hashing effect
                {
                    // Combine with a hash of the nodes involved to make it more unique if multiple poses were ever needed
                    int nodesHash = 0;
                    foreach (Node n in nodesInBindPose)
                    {
                        nodesHash = (nodesHash * 31) + (n.Id?.GetHashCode() ?? 0);
                    }
                    poseFbxObjectId = (poseFbxObjectId == 0 ? 1 : poseFbxObjectId) * 31 + 1001 + nodesHash;
                    if (poseFbxObjectId < 0) poseFbxObjectId = -poseFbxObjectId; // Ensure positive
                    if (poseFbxObjectId == 0) poseFbxObjectId = DateTime.Now.Ticks; // Ultimate fallback
                }


                FbxNode bindPoseNode = posesNode.Add("Pose", poseFbxObjectId, "BindPose", "BindPose");
                bindPoseNode.Add("Type", "BindPose");
                bindPoseNode.Add("NbPoseNodes", nodesInBindPose.Count);

                // Console.WriteLine($"DEBUG: Creating BindPose with {nodesInBindPose.Count} nodes. Pose Object ID: {poseFbxObjectId}");

                foreach (Node nodeToPose in nodesInBindPose)
                {
                    if (!nodeToPose.Id.HasValue)
                    {
                        // Console.WriteLine($"DEBUG: nodeToPose {nodeToPose.Name} has no ID, skipping.");
                        continue;
                    }
                    if (!_objectTemplates.ContainsKey(nodeToPose.Id.Value))
                    {
                        // This should not happen if EnsureFbxObjectCreated was called for all relevant nodes
                        // Console.WriteLine($"DEBUG: nodeToPose {nodeToPose.Name} (ID: {nodeToPose.Id}) not in _objectTemplates, skipping. This might indicate an issue with EnsureFbxObjectCreated.");
                        EnsureFbxObjectCreated(nodeToPose); // Try to create it now, though it's late
                        if (!_objectTemplates.ContainsKey(nodeToPose.Id.Value)) continue; // Still not there, skip
                    }

                    FbxNode poseNodeEntry = bindPoseNode.Add("PoseNode");
                    poseNodeEntry.Add("Node", (long)nodeToPose.Id.Value); // Use the MeshIO Node's ID

                    // Directly calculate the world matrix for the node.
                    // This assumes nodeToPose.Transform and its parent hierarchy
                    // correctly represent the bind pose at the time of export.
                    Matrix4 worldBindMatrix = CalculateWorldMatrix(nodeToPose);

                    // double[] matrixArray = worldBindMatrix.ToRowMajorArray();
                    // string matrixStr = string.Join(",", matrixArray.Select(d => d.ToString("F7")));
                    // Console.WriteLine($"DEBUG: PoseNode for {nodeToPose.Name} (ID: {nodeToPose.Id.Value}), Matrix: [{matrixStr}]");

                    poseNodeEntry.Add("Matrix", worldBindMatrix.ToRowMajorArray());
                }
            }

            // Add the "Count" node at the beginning of posesNode children if any "Pose" sub-nodes exist.
            if (posesNode.Nodes.Any(n => n.Name == "Pose")) // Check if any "Pose" sub-node was actually added
            {
                posesNode.Nodes.Insert(0, new FbxNode("Count", poseCount));
            }
            else // No "Pose" sub-nodes were added (e.g., nodesInBindPose was empty)
            {
                posesNode.Add("Count", 0); // Add Count: 0 if no poses
            }

            // Register "Pose" in _definedObjects if a pose was actually created.
            // This is used by nodeDefinitions() to declare object types.
            if (poseCount > 0)
            {
                if (!_definedObjects.ContainsKey(FbxFileToken.Pose))
                {
                    _definedObjects.Add(FbxFileToken.Pose, new List<IFbxObjectTemplate>());
                    // Note: The "Pose" object itself (the one with poseFbxObjectId) isn't an IFbxObjectTemplate
                    // in the current system. _definedObjects for "Pose" is more of a flag for nodeDefinitions.
                    // This might need refinement if actual FbxPoseTemplate objects were introduced.
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