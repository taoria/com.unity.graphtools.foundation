using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.GraphToolsFoundation.Overdrive;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace UnityEditor.GraphToolsFoundation.Overdrive.BasicModel
{
    [Serializable]
    public abstract class GraphModel : IGraphModel
    {
        const string k_DefaultPlacematName = "Placemat";
        static readonly Vector2 k_PortalOffset = Vector2.right * 150;

        [SerializeReference]
        GraphAssetModel m_AssetModel;

        [SerializeReference]
        protected List<INodeModel> m_GraphNodeModels;

        [SerializeField, Obsolete]
        // ReSharper disable once Unity.RedundantFormerlySerializedAsAttribute
        List<EdgeModel> m_EdgeModels;

        [SerializeReference]
        protected List<IEdgeModel> m_GraphEdgeModels;

        [SerializeReference, Obsolete]
        List<EdgeModel> m_PolymorphicEdgeModels;

        [SerializeField, Obsolete]
        List<StickyNoteModel> m_StickyNoteModels;

        [SerializeReference]
        protected List<IStickyNoteModel> m_GraphStickyNoteModels;

        [SerializeField, Obsolete]
        List<PlacematModel> m_PlacematModels;

        [SerializeReference]
        protected List<IPlacematModel> m_GraphPlacematModels;

        [SerializeReference]
        protected List<IVariableDeclarationModel> m_GraphVariableModels;

        [SerializeReference]
        protected List<IDeclarationModel> m_GraphPortalModels;

        [SerializeReference]
        Stencil m_Stencil;

        [SerializeField, FormerlySerializedAs("name")]
        string m_Name;

        GraphChangeList m_LastChanges;

        Dictionary<GUID, INodeModel> m_NodesByGuid;

        public IGraphAssetModel AssetModel
        {
            get => m_AssetModel;
            set => m_AssetModel = (GraphAssetModel)value;
        }

        public Stencil Stencil
        {
            get => m_Stencil;
            set => m_Stencil = value;
        }

        public IReadOnlyList<INodeModel> NodeModels => m_GraphNodeModels;

        public IReadOnlyList<IEdgeModel> EdgeModels => m_GraphEdgeModels;

        public IReadOnlyList<IStickyNoteModel> StickyNoteModels => m_GraphStickyNoteModels;

        public IReadOnlyList<IPlacematModel> PlacematModels => m_GraphPlacematModels;

        public IList<IVariableDeclarationModel> VariableDeclarations => m_GraphVariableModels;

        public IReadOnlyList<IDeclarationModel> PortalDeclarations => m_GraphPortalModels;

        public IReadOnlyDictionary<GUID, INodeModel> NodesByGuid => m_NodesByGuid ?? (m_NodesByGuid = new Dictionary<GUID, INodeModel>());

        Dictionary<string, IGuidUpdate> m_OldToNewGuids;

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string FriendlyScriptName => StringExtensions.CodifyString(AssetModel.Name);

        public GraphChangeList LastChanges => m_LastChanges ?? (m_LastChanges = new GraphChangeList());

        protected GraphModel()
        {
            m_GraphNodeModels = new List<INodeModel>();
            m_GraphEdgeModels = new List<IEdgeModel>();
            m_GraphStickyNoteModels = new List<IStickyNoteModel>();
            m_GraphPlacematModels = new List<IPlacematModel>();
            m_GraphVariableModels = new List<IVariableDeclarationModel>();
            m_GraphPortalModels = new List<IDeclarationModel>();
        }

        public virtual List<IPortModel> GetCompatiblePorts(IPortModel startPortModel)
        {
            var startEdgePortalModel = startPortModel.NodeModel as IEdgePortalModel;

            return this.GetPortModels().ToList().Where(pModel =>
            {
                if (startPortModel.PortDataType == typeof(ExecutionFlow) && pModel.PortDataType != typeof(ExecutionFlow))
                    return false;
                if (pModel.PortDataType == typeof(ExecutionFlow) && startPortModel.PortDataType != typeof(ExecutionFlow))
                    return false;

                // No good if ports belong to same node that does not allow self connect
                if (pModel == startPortModel ||
                    (pModel.NodeModel != null || startPortModel.NodeModel != null) &&
                    !startPortModel.NodeModel.AllowSelfConnect && pModel.NodeModel == startPortModel.NodeModel)
                    return false;

                // No good if it's on the same portal either.
                if (pModel.NodeModel is IEdgePortalModel edgePortalModel)
                {
                    if (edgePortalModel.DeclarationModel.Guid == startEdgePortalModel?.DeclarationModel.Guid)
                        return false;
                }

                // This is true for all ports
                return pModel.Direction != startPortModel.Direction;
            })
                // deep in GraphView's EdgeDragHelper, this list is used to find the first port to use when dragging an
                // edge. as ports are returned in hierarchy order (back to front), in case of a conflict, the one behind
                // the others is returned. reverse the list to get the most logical one, the one on top of everything else
                .Reverse()
                .ToList();
        }

        public string GetAssetPath()
        {
            return AssetDatabase.GetAssetPath(m_AssetModel);
        }

        public abstract string GetSourceFilePath();

        public TNodeType CreateNode<TNodeType>(string nodeName = "", Vector2 position = default,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<TNodeType> preDefineSetup = null, GUID? guid = null)
            where TNodeType : class, INodeModel
        {
            Action<INodeModel> setupWrapper = null;
            if (preDefineSetup != null)
            {
                setupWrapper = n => preDefineSetup.Invoke(n as TNodeType);
            }

            return (TNodeType)CreateNodeInternal(typeof(TNodeType), nodeName, position, spawnFlags, setupWrapper, guid);
        }

        public INodeModel CreateNode(Type nodeTypeToCreate, string nodeName, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<INodeModel> preDefineSetup = null, GUID? guid = null)
        {
            return CreateNodeInternal(nodeTypeToCreate, nodeName, position, spawnFlags, preDefineSetup, guid);
        }

        protected virtual INodeModel CreateNodeInternal(Type nodeTypeToCreate, string nodeName, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<INodeModel> preDefineSetup = null, GUID? guid = null)
        {
            if (nodeTypeToCreate == null)
                throw new ArgumentNullException(nameof(nodeTypeToCreate));
            INodeModel nodeModel;
            if (typeof(IConstant).IsAssignableFrom(nodeTypeToCreate))
                nodeModel = new ConstantNodeModel {Value = (IConstant)Activator.CreateInstance(nodeTypeToCreate)};
            else if (typeof(INodeModel).IsAssignableFrom(nodeTypeToCreate))
                nodeModel = (INodeModel)Activator.CreateInstance(nodeTypeToCreate);
            else
                throw new ArgumentOutOfRangeException(nameof(nodeTypeToCreate));

            if (nodeModel is IHasTitle titled)
                titled.Title = nodeName ?? nodeTypeToCreate.Name;

            nodeModel.Position = position;
            nodeModel.Guid = guid ?? GUID.Generate();
            nodeModel.AssetModel = AssetModel;
            preDefineSetup?.Invoke(nodeModel);
            nodeModel.DefineNode();
            if (!spawnFlags.IsOrphan())
            {
                AddNode(nodeModel);
                if (m_AssetModel)
                    EditorUtility.SetDirty(m_AssetModel);
            }
            return nodeModel;
        }

        public virtual IVariableNodeModel CreateVariableNode(IVariableDeclarationModel declarationModel, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, GUID? guid = null)
        {
            return CreateNode<VariableNodeModel>(declarationModel.DisplayTitle, position, spawnFlags,
                v => v.DeclarationModel = declarationModel, guid);
        }

        public virtual IConstantNodeModel CreateConstantNode(string constantName, TypeHandle constantTypeHandle, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, GUID? guid = null, Action<IConstantNodeModel> preDefine = null)
        {
            var nodeType = Stencil.GetConstantNodeValueType(constantTypeHandle);

            void PreDefineSetup(INodeModel model)
            {
                if (model is IConstantNodeModel constantModel)
                {
                    constantModel.PredefineSetup();
                    preDefine?.Invoke(constantModel);
                }
            }

            return (IConstantNodeModel)CreateNode(nodeType, constantName, position, spawnFlags, PreDefineSetup, guid);
        }

        public void AddNode(INodeModel nodeModel)
        {
            AddNodeInternal(nodeModel);
            LastChanges?.ChangedElements.Add(nodeModel);
        }

        void AddNodeInternal(INodeModel nodeModel)
        {
            nodeModel.AssetModel = AssetModel;
            m_GraphNodeModels.Add(nodeModel);
            if (m_NodesByGuid == null)
                m_NodesByGuid = new Dictionary<GUID, INodeModel>();
            m_NodesByGuid.Add(nodeModel.Guid, nodeModel);
        }

        public INodeModel DuplicateNode(INodeModel copiedNode, Dictionary<INodeModel, INodeModel> mapping, Vector2 delta)
        {
            // PF FIXME cast to NodeModel
            var pastedNodeModel = (copiedNode as NodeModel).Clone();

            // Set graphmodel BEFORE define node as it is commonly use during Define
            pastedNodeModel.AssetModel = AssetModel;
            pastedNodeModel.Title = (copiedNode as IHasTitle)?.Title ?? "";
            pastedNodeModel.AssignNewGuid();
            pastedNodeModel.DefineNode();
            pastedNodeModel.ReinstantiateInputConstants();
            mapping.Add(copiedNode, pastedNodeModel);

            AddNode(pastedNodeModel);
            pastedNodeModel.Position += delta;

            return pastedNodeModel;
        }

        public virtual void CreateItemizedNode(State state, int nodeOffset, ref IPortModel outputPortModel)
        {
            if (!outputPortModel.IsConnected())
                return;

            if (outputPortModel.NodeModel is IConstantNodeModel || outputPortModel.NodeModel is IVariableNodeModel)
            {
                CreateItemizedNode(nodeOffset, ref outputPortModel);
            }
        }

        protected void CreateItemizedNode(int nodeOffset, ref IPortModel outputPortModel)
        {
            Vector2 offset = Vector2.up * nodeOffset;
            var nodeToConnect = DuplicateNode(outputPortModel.NodeModel,
                new Dictionary<INodeModel, INodeModel>(), offset) as IInOutPortsNode;
            outputPortModel = nodeToConnect?.OutputsById[outputPortModel.UniqueName];
        }

        public void DeleteNodes(IReadOnlyCollection<INodeModel> nodesToDelete, DeleteConnections deleteConnections)
        {
            foreach (var node in nodesToDelete)
                DeleteNode(node, deleteConnections);
        }

        public void DeleteNode(INodeModel nodeModel, DeleteConnections deleteConnections)
        {
            if (LastChanges != null)
                LastChanges.DeletedElements += 1;
            m_GraphNodeModels.Remove(nodeModel);

            if (deleteConnections == DeleteConnections.True)
            {
                var connectedEdges = nodeModel.GetConnectedEdges().ToList();
                DeleteEdges(connectedEdges);
            }
            m_NodesByGuid?.Remove(nodeModel.Guid);

            // If this is the last portal with the given declaration, delete the declaration.
            if (nodeModel is EdgePortalModel edgePortalModel &&
                !this.FindReferencesInGraph<IEdgePortalModel>(edgePortalModel.DeclarationModel).Any())
            {
                m_GraphPortalModels.Remove(edgePortalModel.DeclarationModel);
            }

            nodeModel.Destroy();
        }

        public virtual IEdgeModel CreateEdge(IPortModel inputPort, IPortModel outputPort)
        {
            var existing = GetEdgeConnectedToPorts(inputPort, outputPort);
            if (existing != null)
                return existing;

            var edgeModel = CreateOrphanEdge<EdgeModel>(inputPort, outputPort);
            if (edgeModel != null)
            {
                m_GraphEdgeModels.Add(edgeModel);
                LastChanges?.ChangedElements.Add(edgeModel);
                LastChanges?.ChangedElements.Add(inputPort.NodeModel);
                LastChanges?.ChangedElements.Add(outputPort.NodeModel);
            }

            return edgeModel;
        }

        protected EdgeT CreateOrphanEdge<EdgeT>(IPortModel input, IPortModel output) where EdgeT : IEdgeModel, new()
        {
            Assert.IsNotNull(input);
            Assert.IsNotNull(input.NodeModel);
            Assert.IsNotNull(output);
            Assert.IsNotNull(output.NodeModel);

            var edgeModel = new EdgeT { AssetModel = AssetModel, EdgeLabel = "" };
            edgeModel.SetPorts(input, output);

            input.NodeModel.OnConnection(input, output);
            output.NodeModel.OnConnection(output, input);

            return edgeModel;
        }

        public void DeleteEdge(IEdgeModel edgeModel)
        {
            edgeModel?.ToPort?.NodeModel?.OnDisconnection(edgeModel.ToPort, edgeModel.FromPort);
            edgeModel?.FromPort?.NodeModel?.OnDisconnection(edgeModel.FromPort, edgeModel.ToPort);

            LastChanges?.ChangedElements.Add(edgeModel?.ToPort?.NodeModel);
            LastChanges?.ChangedElements.Add(edgeModel?.FromPort?.NodeModel);

            m_GraphEdgeModels.Remove(edgeModel as EdgeModel);
            if (LastChanges != null)
            {
                LastChanges.DeletedEdges.Add(edgeModel);
                LastChanges.DeletedElements += 1;
            }
        }

        public void DeleteEdges(IEnumerable<IEdgeModel> edgeModels)
        {
            var edgesCopy = edgeModels.ToList();
            foreach (var edgeModel in edgesCopy)
                DeleteEdge(edgeModel);
        }

        public void MoveEdgeBefore(IEdgeModel toMove, IEdgeModel reference)
        {
            m_GraphEdgeModels.Remove((EdgeModel)toMove);
            m_GraphEdgeModels.Insert(m_GraphEdgeModels.IndexOf((EdgeModel)reference), (EdgeModel)toMove);
        }

        public void MoveEdgeAfter(IEdgeModel toMove, IEdgeModel reference)
        {
            m_GraphEdgeModels.Remove((EdgeModel)toMove);
            m_GraphEdgeModels.Insert(m_GraphEdgeModels.IndexOf((EdgeModel)reference) + 1, (EdgeModel)toMove);
        }

        public void DeleteElements(IEnumerable<IGraphElementModel> graphElementModels)
        {
            foreach (var model in graphElementModels)
            {
                switch (model)
                {
                    case INodeModel nodeModel:
                        m_GraphNodeModels.Remove(nodeModel);
                        // If this is the last portal with the given declaration, delete the declaration.
                        if (nodeModel is EdgePortalModel edgePortalModel &&
                            !this.FindReferencesInGraph<IEdgePortalModel>(edgePortalModel.DeclarationModel).Any())
                        {
                            m_GraphPortalModels.Remove(edgePortalModel.DeclarationModel);
                        }
                        break;
                    case EdgeModel edgeModel:
                        m_GraphEdgeModels.Remove(edgeModel);
                        break;
                    case StickyNoteModel stickyNoteModel:
                        m_GraphStickyNoteModels.Remove(stickyNoteModel);
                        break;
                    case PlacematModel placematModel:
                        m_GraphPlacematModels.Remove(placematModel);
                        break;
                }
            }
        }

        public virtual IStickyNoteModel CreateStickyNote(Rect position, SpawnFlags dataSpawnFlags = SpawnFlags.Default)
        {
            var stickyNodeModel = CreateOrphanStickyNote<StickyNoteModel>(position);
            if (!dataSpawnFlags.IsOrphan())
            {
                m_GraphStickyNoteModels.Add(stickyNodeModel);
                LastChanges?.ChangedElements.Add(stickyNodeModel);
            }
            return stickyNodeModel;
        }

        protected StickyNoteT CreateOrphanStickyNote<StickyNoteT>(Rect position) where StickyNoteT : IStickyNoteModel, new()
        {
            var stickyNodeModel = new StickyNoteT();
            stickyNodeModel.PositionAndSize = position;
            stickyNodeModel.AssetModel = AssetModel;

            return stickyNodeModel;
        }

        void DeleteStickyNote(StickyNoteModel stickyNoteModel)
        {
            m_GraphStickyNoteModels.Remove(stickyNoteModel);
            if (LastChanges != null)
                LastChanges.DeletedElements += 1;

            stickyNoteModel.Destroy();
        }

        public void DeleteStickyNotes(IStickyNoteModel[] stickyNotesToDelete)
        {
            foreach (IStickyNoteModel stickyNoteModel in stickyNotesToDelete)
                DeleteStickyNote(stickyNoteModel as StickyNoteModel);
        }

        public virtual IPlacematModel CreatePlacemat(string title, Rect position, SpawnFlags dataSpawnFlags = SpawnFlags.Default)
        {
            var placematModel = CreateOrphanPlacemat<PlacematModel>(title ?? k_DefaultPlacematName, position);
            if (!dataSpawnFlags.IsOrphan())
                AddPlacemat(placematModel);

            return placematModel;
        }

        PlacematT CreateOrphanPlacemat<PlacematT>(string title, Rect position) where PlacematT : IPlacematModel, new()
        {
            var placematModel = new PlacematT();
            placematModel.Title = title;
            placematModel.PositionAndSize = position;
            placematModel.AssetModel = AssetModel;
            placematModel.ZOrder = GetPlacematTopZOrder();
            return placematModel;
        }

        int GetPlacematTopZOrder()
        {
            int maxZ = Int32.MinValue;
            foreach (var model in m_GraphPlacematModels)
            {
                maxZ = Math.Max(model.ZOrder, maxZ);
            }
            return maxZ == Int32.MinValue ? 1 : maxZ + 1;
        }

        void AddPlacemat(PlacematModel placematModel)
        {
            m_GraphPlacematModels.Add(placematModel);
            LastChanges?.ChangedElements.Add(placematModel);
        }

        void DeletePlacemat(PlacematModel placematModel)
        {
            m_GraphPlacematModels.Remove(placematModel);
            if (LastChanges != null)
            {
                LastChanges.ChangedElements.AddRange(placematModel.HiddenElements);
                LastChanges.DeletedElements += 1;
            }

            placematModel.Destroy();
        }

        public void DeletePlacemats(IPlacematModel[] placematsToDelete)
        {
            foreach (var placematModel in placematsToDelete)
                DeletePlacemat(placematModel as PlacematModel);
        }

        public IVariableDeclarationModel CreateGraphVariableDeclaration(string variableName,
            TypeHandle variableDataType, ModifierFlags modifierFlags, bool isExposed,
            IConstant initializationModel = null, GUID? guid = null)
        {
            var field = VariableDeclarationModel.Create(variableName, variableDataType, isExposed, this, VariableType.GraphVariable, modifierFlags, initializationModel, guid);
            VariableDeclarations.Add(field);
            return field;
        }

        public List<IVariableDeclarationModel> DuplicateGraphVariableDeclarations(List<IVariableDeclarationModel> variableDeclarationModels)
        {
            List<IVariableDeclarationModel> duplicatedModels = new List<IVariableDeclarationModel>();
            foreach (var original in variableDeclarationModels)
            {
                if (original.VariableType != VariableType.GraphVariable)
                    continue;
                string uniqueName = original.Title;
                VariableDeclarationModel copy = ((VariableDeclarationModel)original).Clone();
                copy.Title = uniqueName;
                if (copy.InitializationModel != null)
                {
                    copy.CreateInitializationValue();
                    copy.InitializationModel.ObjectValue = original.InitializationModel.ObjectValue;
                }

                EditorUtility.SetDirty((Object)AssetModel);

                duplicatedModels.Add(copy);
                LastChanges.ChangedElements.Add(copy);
            }

            m_GraphVariableModels.AddRange(duplicatedModels);

            return duplicatedModels;
        }

        public void ReorderGraphVariableDeclaration(IVariableDeclarationModel variableDeclarationModel, int index)
        {
            Assert.IsTrue(index >= 0);

            if (variableDeclarationModel.VariableType == VariableType.GraphVariable)
            {
                var oldIndex = VariableDeclarations.IndexOf(variableDeclarationModel);
                VariableDeclarations.RemoveAt(oldIndex);
                if (index > oldIndex) index--;    // the actual index could have shifted due to the removal
                if (index >= VariableDeclarations.Count)
                    VariableDeclarations.Add(variableDeclarationModel);
                else
                    VariableDeclarations.Insert(index, variableDeclarationModel);
                LastChanges.ChangedElements.Add(variableDeclarationModel);
                LastChanges.DeletedElements++;
            }
        }

        public void DeleteVariableDeclarations(IEnumerable<IVariableDeclarationModel> variableModels, bool deleteUsages)
        {
            foreach (var variableModel in variableModels)
            {
                if (LastChanges != null)
                {
                    LastChanges.BlackBoardChanged = true;
                }
                if (variableModel.VariableType == VariableType.GraphVariable)
                {
                    VariableDeclarations.Remove(variableModel);
                }
                if (deleteUsages)
                {
                    var nodesToDelete = this.FindReferencesInGraph(variableModel).Cast<INodeModel>().ToList();
                    DeleteNodes(nodesToDelete, DeleteConnections.True);
                }
            }
        }

        public IVariableDeclarationModel CreateGraphPortalDeclaration(string portalName, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            // TODO JOCE: We should use something lighter than a variable declaration for portals. We don't need all that's in there
            var field = VariableDeclarationModel.Create(portalName, TypeHandle.Unknown, false, this, VariableType.EdgePortal, ModifierFlags.ReadWrite);

            if (!spawnFlags.IsOrphan())
                m_GraphPortalModels.Add(field);

            return field;
        }

        public IEdgePortalModel CreateOppositePortal(IEdgePortalModel edgePortalModel, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            var offset = Vector2.zero;
            switch (edgePortalModel)
            {
                case IEdgePortalEntryModel _:
                    offset = k_PortalOffset;
                    break;
                case IEdgePortalExitModel _:
                    offset = -k_PortalOffset;
                    break;
            }
            var currentPos = edgePortalModel.Position;
            return CreateOppositePortal(edgePortalModel, currentPos + offset, spawnFlags);
        }

        public IEdgePortalModel CreateOppositePortal(IEdgePortalModel edgePortalModel, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            EdgePortalModel createdPortal = null;
            Type oppositeType = null;
            switch (edgePortalModel)
            {
                case ExecutionEdgePortalEntryModel _:
                    oppositeType = typeof(ExecutionEdgePortalExitModel);
                    break;
                case ExecutionEdgePortalExitModel _:
                    oppositeType = typeof(ExecutionEdgePortalEntryModel);
                    break;
                case DataEdgePortalEntryModel _:
                    oppositeType = typeof(DataEdgePortalExitModel);
                    break;
                case DataEdgePortalExitModel _:
                    oppositeType = typeof(DataEdgePortalEntryModel);
                    break;
            }

            if (oppositeType != null)
                createdPortal = (EdgePortalModel)CreateNode(oppositeType, edgePortalModel.Title, position, spawnFlags);

            if (createdPortal != null)
            {
                createdPortal.DeclarationModel = edgePortalModel.DeclarationModel;
            }

            return createdPortal;
        }

        public IEdgePortalEntryModel CreateEntryPortalFromEdge(IEdgeModel edgeModel)
        {
            var outputPortModel = edgeModel.FromPort;
            if (outputPortModel.PortType == PortType.Execution)
                return CreateNode<ExecutionEdgePortalEntryModel>();

            return CreateNode<DataEdgePortalEntryModel>();
        }

        public IEdgePortalExitModel CreateExitPortalFromEdge(IEdgeModel edgeModel)
        {
            var inputPortModel = edgeModel.ToPort;
            if (inputPortModel?.PortType == PortType.Execution)
                return CreateNode<ExecutionEdgePortalExitModel>();

            return CreateNode<DataEdgePortalExitModel>();
        }

        internal void AddGuidToUpdate(IGuidUpdate element, string oldGuid)
        {
            if (m_OldToNewGuids == null)
                m_OldToNewGuids = new Dictionary<string, IGuidUpdate>();


            if (oldGuid == "00000000000000000000000000000000")
                oldGuid = (-m_OldToNewGuids.Count).ToString();

            Debug.Assert(!m_OldToNewGuids.ContainsKey(oldGuid), element + " already owns " + oldGuid);
            m_OldToNewGuids[oldGuid] = element;
        }

        void UpdateGuids()
        {
            if (m_OldToNewGuids == null)
                return;

            // Generate missing GUIDs
            foreach (var model in m_OldToNewGuids)
            {
                model.Value.AssignGuid(model.Key);
            }

            // Update placemat hidden elements.
            foreach (var model in m_OldToNewGuids)
            {
                if (model.Value is PlacematModel placematModel)
                    placematModel.UpdateHiddenGuids(m_OldToNewGuids);
            }

            m_OldToNewGuids.Clear();
        }

        public virtual void OnEnable()
        {
            if (m_GraphEdgeModels == null)
                m_GraphEdgeModels = new List<IEdgeModel>();

            if (m_GraphStickyNoteModels == null)
                m_GraphStickyNoteModels = new List<IStickyNoteModel>();

            if (m_GraphPlacematModels == null)
                m_GraphPlacematModels = new List<IPlacematModel>();

#pragma warning disable 612
            // Serialized data conversion code
            if (m_EdgeModels?.Count > 0)
            {
                m_GraphEdgeModels.AddRange(m_EdgeModels);
                m_EdgeModels = null;
            }

            if (m_PolymorphicEdgeModels?.Count > 0)
            {
                m_GraphEdgeModels.AddRange(m_PolymorphicEdgeModels);
                m_PolymorphicEdgeModels = null;
            }

            // Serialized data conversion code
            if (m_StickyNoteModels != null)
            {
                m_GraphStickyNoteModels.AddRange(m_StickyNoteModels);
                m_StickyNoteModels = null;
            }

            // Serialized data conversion code
            if (m_PlacematModels != null)
            {
                m_GraphPlacematModels.AddRange(m_PlacematModels);
                m_PlacematModels = null;
            }
#pragma warning restore 612

            UpdateGuids();

            if (m_GraphNodeModels == null)
                m_GraphNodeModels = new List<INodeModel>();

            m_NodesByGuid = new Dictionary<GUID, INodeModel>(m_GraphNodeModels.Count);

            foreach (var model in NodeModels)
            {
                if (model is null)
                    continue;
                model.AssetModel = AssetModel;
                Debug.Assert(!model.Guid.Empty());
                m_NodesByGuid.Add(model.Guid, model);
            }
        }

        public void OnDisable()
        {
        }

        public void Dispose() {}

        public IEnumerable<IEdgeModel> GetEdgesConnections(IPortModel portModel)
        {
            return EdgeModels.Where(e => portModel.Direction == Direction.Input ? PortModel.Equivalent(e.ToPort, portModel) : PortModel.Equivalent(e.FromPort, portModel));
        }

        public IEnumerable<IEdgeModel> GetEdgesConnections(INodeModel nodeModel)
        {
            return EdgeModels.Where(e => e.ToPort?.NodeModel.Guid == nodeModel.Guid
                || e.FromPort?.NodeModel.Guid == nodeModel.Guid);
        }

        public IEnumerable<IPortModel> GetConnections(IPortModel portModel)
        {
            return GetEdgesConnections(portModel)
                .Select(e => portModel.Direction == Direction.Input ? e.FromPort : e.ToPort)
                .Where(p => p != null);
        }

        public IEdgeModel GetEdgeConnectedToPorts(IPortModel input, IPortModel output)
        {
            return EdgeModels.FirstOrDefault(e => e.ToPort == input && e.FromPort == output);
        }

        public void ResetChangeList()
        {
            m_LastChanges = new GraphChangeList();
        }

        public void UndoRedoPerformed()
        {
            OnEnable();
        }

        public void Repair()
        {
            m_GraphNodeModels.RemoveAll(n => n == null);
            m_GraphStickyNoteModels.RemoveAll(s => s == null);
            m_GraphPlacematModels.RemoveAll(p => p == null);
            DeleteEdges(m_GraphEdgeModels.Where(e => e.ToPort == null || e.FromPort == null));
            m_GraphEdgeModels.RemoveAll(e => e == null);
        }

        public bool CheckIntegrity(Verbosity errors)
        {
            Assert.IsTrue((Object)AssetModel, "graph asset is invalid");
            bool failed = false;
            for (var i = 0; i < m_GraphEdgeModels.Count; i++)
            {
                var edge = m_GraphEdgeModels[i];
                if (edge.ToPort == null)
                {
                    failed = true;
                    Debug.Log($"Edge {i} input is null, output: {edge.FromPort}");
                }

                if (edge.FromPort == null)
                {
                    failed = true;
                    Debug.Log($"Edge {i} output is null, input: {edge.ToPort}");
                }
            }

            CheckNodeList(m_GraphNodeModels);
            if (!failed && errors == Verbosity.Verbose)
                Debug.Log("Integrity check succeeded");
            return !failed;
        }

        void CheckNodeList(IList<INodeModel> nodeModels, Dictionary<GUID, int> existingGuids = null)
        {
            if (existingGuids == null)
                existingGuids = new Dictionary<GUID, int>(nodeModels.Count * 4); // wild guess of total number of nodes, including stacked nodes
            for (var i = 0; i < nodeModels.Count; i++)
            {
                INodeModel node = nodeModels[i];

                Assert.IsTrue(node.GraphModel != null, $"Node {i} {node} graph is null");
                Assert.IsTrue(node.AssetModel != null, $"Node {i} {node} asset is null");
                Assert.IsNotNull(node, $"Node {i} is null");
                Assert.IsTrue(AssetModel.GetHashCode() == node.AssetModel?.GetHashCode(), $"Node {i} asset is not matching its actual asset");
                Assert.IsFalse(node.Guid.Empty(), $"Node {i} ({node.GetType()}) has an empty Guid");
                Assert.IsFalse(existingGuids.TryGetValue(node.Guid, out var oldIndex), $"duplicate GUIDs: Node {i} ({node.GetType()}) and Node {oldIndex} have the same guid {node.Guid}");
                existingGuids.Add(node.Guid, i);

                if (node.Destroyed)
                    continue;

                if (node is IInOutPortsNode portHolder)
                {
                    CheckNodePorts(portHolder.InputsById);
                    CheckNodePorts(portHolder.OutputsById);
                }

                if (node is VariableNodeModel variableNode && variableNode.DeclarationModel != null)
                {
                    if (variableNode.VariableDeclarationModel.VariableType == VariableType.GraphVariable)
                    {
                        var originalDeclarations = VariableDeclarations.Where(d => d.Guid == variableNode.DeclarationModel.Guid);
                        Assert.IsTrue(originalDeclarations.Count() <= 1);
                        var originalDeclaration = originalDeclarations.SingleOrDefault();
                        Assert.IsNotNull(originalDeclaration, $"Variable Node {i} {variableNode.Title} has a declaration model, but it was not present in the graph's variable declaration list");
                        Assert.IsTrue(ReferenceEquals(originalDeclaration, variableNode.DeclarationModel), $"Variable Node {i} {variableNode.Title} has a declaration model that was not ReferenceEquals() to the matching one in the graph");
                    }
                }
            }
        }

        static void CheckNodePorts(IReadOnlyDictionary<string, IPortModel> portsById)
        {
            foreach (var kv in portsById)
            {
                string portId = portsById[kv.Key].UniqueName;
                Assert.AreEqual(kv.Key, portId, $"Node {kv.Key} port and its actual id {portId} mismatch");
            }
        }

        public void QuickCleanup()
        {
            for (var i = m_GraphEdgeModels.Count - 1; i >= 0; i--)
            {
                var edge = m_GraphEdgeModels[i];
                if (edge?.ToPort == null || edge.FromPort == null)
                    m_GraphEdgeModels.RemoveAt(i);
            }

            CleanupNodes(m_GraphNodeModels);
        }

        static void CleanupNodes(IList<INodeModel> models)
        {
            for (var i = models.Count - 1; i >= 0; i--)
            {
                if (models[i].Destroyed)
                    models.RemoveAt(i);
            }
        }

        public abstract CompilationResult Compile(ITranslator translator);
    }
}
