using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;
using Edge = UnityEditor.Graphing.Edge;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    [FormerName("UnityEditor.ShaderGraph.MaterialGraph")]
    [FormerName("UnityEditor.ShaderGraph.SubGraph")]
    [FormerName("UnityEditor.ShaderGraph.AbstractMaterialGraph")]
    sealed partial class GraphData : ISerializationCallbackReceiver
    {
        public GraphObject owner { get; set; }

        #region Input data

        [NonSerialized]
        List<InputCategory> m_Categories = new List<InputCategory>();

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializedCategories = new List<SerializationHelper.JSONSerializedElement>();

        // TODO: z make this a list
        public List<InputCategory> categories
        {
            get { return m_Categories; }
        }

        // This is for when the category itself has been altered (name change, index, ect.), the blackboard should be rebuilt
        // although rebuilding the inputs within each categories isn't required.
        bool m_BlackboardSectionsChanged = false;

        public bool hasBlackboardSectionChanges
        {
            get { return m_BlackboardSectionsChanged; }
        }

        // TODO: look into a way such that this isn't necessary (currently only one use)
        public void SectionChangesHappened()
        {
            m_BlackboardSectionsChanged = true;
        }

        // This is for when the contents (ShaderInputs) within a category are altered: this category needs to be rebuilt.
        [SerializeField]
        List<InputCategory> m_AlteredCategories = new List<InputCategory>();

        public IEnumerable<InputCategory> alteredCategories
        {
            get { return m_AlteredCategories; }
        }

        // TODO: y check that these aren't used on frequently reoccuring steps (actually, make these functions)
        public IEnumerable<AbstractShaderProperty> properties
        {
            get
            {
                List<AbstractShaderProperty> shaderProperties = new List<AbstractShaderProperty>();

                foreach (InputCategory category in m_Categories)
                {
                    foreach (ShaderInput input in category.inputs)
                    {
                        AbstractShaderProperty property = input as AbstractShaderProperty;
                        if (property != null)
                            shaderProperties.Add(property);
                    }
                }

                return shaderProperties;
            }
        }

        public IEnumerable<ShaderKeyword> keywords
        {
            get
            {
                List<ShaderKeyword> shaderKeywords = new List<ShaderKeyword>();

                foreach (InputCategory category in m_Categories)
                {
                    foreach (ShaderInput input in category.inputs)
                    {
                        ShaderKeyword keyword = input as ShaderKeyword;
                        if (keyword != null)
                            shaderKeywords.Add(keyword);
                    }
                }

                return shaderKeywords;
            }
        }

        public IEnumerable<ShaderInput> shaderInputs
        {
            get
            {
                List<ShaderInput> shaderInputs = new List<ShaderInput>();

                foreach (InputCategory category in m_Categories)
                {
                    foreach (ShaderInput input in category.inputs)
                    {
                        shaderInputs.Add(input);
                    }
                }

                return shaderInputs;
            }
        }

        public string assetGuid { get; set; }

        #endregion

        #region Node data

        [NonSerialized]
        List<AbstractMaterialNode> m_Nodes = new List<AbstractMaterialNode>();

        [NonSerialized]
        Dictionary<Guid, AbstractMaterialNode> m_NodeDictionary = new Dictionary<Guid, AbstractMaterialNode>();

        public IEnumerable<T> GetNodes<T>()
        {
            return m_Nodes.Where(x => x != null).OfType<T>();
        }

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializableNodes = new List<SerializationHelper.JSONSerializedElement>();

        [NonSerialized]
        List<AbstractMaterialNode> m_AddedNodes = new List<AbstractMaterialNode>();

        public IEnumerable<AbstractMaterialNode> addedNodes
        {
            get { return m_AddedNodes; }
        }

        [NonSerialized]
        List<AbstractMaterialNode> m_RemovedNodes = new List<AbstractMaterialNode>();

        public IEnumerable<AbstractMaterialNode> removedNodes
        {
            get { return m_RemovedNodes; }
        }

        [NonSerialized]
        List<AbstractMaterialNode> m_PastedNodes = new List<AbstractMaterialNode>();

        public IEnumerable<AbstractMaterialNode> pastedNodes
        {
            get { return m_PastedNodes; }
        }
        #endregion

        #region Group Data

        [SerializeField]
        List<GroupData> m_Groups = new List<GroupData>();

        public IEnumerable<GroupData> groups
        {
            get { return m_Groups; }
        }

        [NonSerialized]
        List<GroupData> m_AddedGroups = new List<GroupData>();

        public IEnumerable<GroupData> addedGroups
        {
            get { return m_AddedGroups; }
        }

        [NonSerialized]
        List<GroupData> m_RemovedGroups = new List<GroupData>();

        public IEnumerable<GroupData> removedGroups
        {
            get { return m_RemovedGroups; }
        }

        [NonSerialized]
        List<GroupData> m_PastedGroups = new List<GroupData>();

        public IEnumerable<GroupData> pastedGroups
        {
            get { return m_PastedGroups; }
        }

        [NonSerialized]
        List<ParentGroupChange> m_ParentGroupChanges = new List<ParentGroupChange>();

        public IEnumerable<ParentGroupChange> parentGroupChanges
        {
            get { return m_ParentGroupChanges; }
        }

        [NonSerialized]
        GroupData m_MostRecentlyCreatedGroup;

        public GroupData mostRecentlyCreatedGroup => m_MostRecentlyCreatedGroup;

        [NonSerialized]
        Dictionary<Guid, List<IGroupItem>> m_GroupItems = new Dictionary<Guid, List<IGroupItem>>();

        public IEnumerable<IGroupItem> GetItemsInGroup(GroupData groupData)
        {
            if (m_GroupItems.TryGetValue(groupData.guid, out var nodes))
            {
                return nodes;
            }
            return Enumerable.Empty<IGroupItem>();
        }

        #endregion

        #region StickyNote Data
        [SerializeField]
        List<StickyNoteData> m_StickyNotes = new List<StickyNoteData>();

        public IEnumerable<StickyNoteData> stickyNotes => m_StickyNotes;

        [NonSerialized]
        List<StickyNoteData> m_AddedStickyNotes = new List<StickyNoteData>();

        public List<StickyNoteData> addedStickyNotes => m_AddedStickyNotes;

        [NonSerialized]
        List<StickyNoteData> m_RemovedNotes = new List<StickyNoteData>();

        public IEnumerable<StickyNoteData> removedNotes => m_RemovedNotes;

        [NonSerialized]
        List<StickyNoteData> m_PastedStickyNotes = new List<StickyNoteData>();

        public IEnumerable<StickyNoteData> pastedStickyNotes => m_PastedStickyNotes;

        #endregion

        #region Edge data

        [NonSerialized]
        List<Edge> m_Edges = new List<Edge>();

        public IEnumerable<Edge> edges
        {
            get { return m_Edges; }
        }

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializableEdges = new List<SerializationHelper.JSONSerializedElement>();

        [NonSerialized]
        Dictionary<Guid, List<IEdge>> m_NodeEdges = new Dictionary<Guid, List<IEdge>>();

        [NonSerialized]
        List<IEdge> m_AddedEdges = new List<IEdge>();

        public IEnumerable<IEdge> addedEdges
        {
            get { return m_AddedEdges; }
        }

        [NonSerialized]
        List<IEdge> m_RemovedEdges = new List<IEdge>();

        public IEnumerable<IEdge> removedEdges
        {
            get { return m_RemovedEdges; }
        }

        #endregion

        [SerializeField]
        InspectorPreviewData m_PreviewData = new InspectorPreviewData();

        public InspectorPreviewData previewData
        {
            get { return m_PreviewData; }
            set { m_PreviewData = value; }
        }

        [SerializeField]
        string m_Path;

        public string path
        {
            get { return m_Path; }
            set
            {
                if (m_Path == value)
                    return;
                m_Path = value;
                if(owner != null)
                    owner.RegisterCompleteObjectUndo("Change Path");
            }
        }

        public MessageManager messageManager { get; set; }
        public bool isSubGraph { get; set; }

        [SerializeField]
        private ConcretePrecision m_ConcretePrecision = ConcretePrecision.Float;

        public ConcretePrecision concretePrecision
        {
            get => m_ConcretePrecision;
            set => m_ConcretePrecision = value;
        }

        [NonSerialized]
        Guid m_ActiveOutputNodeGuid;

        public Guid activeOutputNodeGuid
        {
            get { return m_ActiveOutputNodeGuid; }
            set
            {
                if (value != m_ActiveOutputNodeGuid)
                {
                    m_ActiveOutputNodeGuid = value;
                    m_OutputNode = null;
                    didActiveOutputNodeChange = true;
                    UpdateTargets();
                }
            }
        }

        [SerializeField]
        string m_ActiveOutputNodeGuidSerialized;

        [NonSerialized]
        private AbstractMaterialNode m_OutputNode;

        public AbstractMaterialNode outputNode
        {
            get
            {
                // find existing node
                if (m_OutputNode == null)
                {
                    if (isSubGraph)
                    {
                        m_OutputNode = GetNodes<SubGraphOutputNode>().FirstOrDefault();
                    }
                    else
                    {
                        m_OutputNode = GetNodeFromGuid(m_ActiveOutputNodeGuid);
                    }
                }

                return m_OutputNode;
            }
        }

        #region Targets
        [NonSerialized]
        List<ITarget> m_ValidTargets = new List<ITarget>();

        [NonSerialized]
        List<ITargetImplementation> m_ValidImplementations = new List<ITargetImplementation>();

        public List<ITargetImplementation> validImplementations => m_ValidImplementations;
        #endregion

        public bool didActiveOutputNodeChange { get; set; }

        internal delegate void SaveGraphDelegate(Shader shader, object context);
        internal static SaveGraphDelegate onSaveGraph;

        public GraphData()
        {
            m_GroupItems[Guid.Empty] = new List<IGroupItem>();
        }

        public void ClearChanges()
        {
            m_AddedNodes.Clear();
            m_RemovedNodes.Clear();
            m_PastedNodes.Clear();
            m_ParentGroupChanges.Clear();
            m_AddedGroups.Clear();
            m_RemovedGroups.Clear();
            m_PastedGroups.Clear();
            m_AddedEdges.Clear();
            m_RemovedEdges.Clear();
            m_AddedStickyNotes.Clear();
            m_RemovedNotes.Clear();
            m_PastedStickyNotes.Clear();
            m_AlteredCategories.Clear();
            m_BlackboardSectionsChanged = false;
            m_MostRecentlyCreatedGroup = null;
            didActiveOutputNodeChange = false;
        }

        public void AddNode(AbstractMaterialNode node)
        {
            if (node is AbstractMaterialNode materialNode)
            {
                if (isSubGraph && !materialNode.allowedInSubGraph)
                {
                    Debug.LogWarningFormat("Attempting to add {0} to Sub Graph. This is not allowed.", materialNode.GetType());
                    return;
                }

                AddNodeNoValidate(materialNode);

                // If adding a Sub Graph node whose asset contains Keywords
                // Need to restest Keywords against the variant limit
                if(node is SubGraphNode subGraphNode &&
                    subGraphNode.asset != null &&
                    subGraphNode.asset.keywords.Count > 0)
                {
                    OnKeywordChangedNoValidate();
                }

                ValidateGraph();
            }
            else
            {
                Debug.LogWarningFormat("Trying to add node {0} to Material graph, but it is not a {1}", node, typeof(AbstractMaterialNode));
            }
        }

        public void CreateGroup(GroupData groupData)
        {
            if (AddGroup(groupData))
            {
                m_MostRecentlyCreatedGroup = groupData;
            }
        }

        bool AddGroup(GroupData groupData)
        {
            if (m_Groups.Contains(groupData))
                return false;

            m_Groups.Add(groupData);
            m_AddedGroups.Add(groupData);
            m_GroupItems.Add(groupData.guid, new List<IGroupItem>());

            return true;
        }

        public void RemoveGroup(GroupData groupData)
        {
            RemoveGroupNoValidate(groupData);
            ValidateGraph();
        }

        void RemoveGroupNoValidate(GroupData group)
        {
            if (!m_Groups.Contains(group))
                throw new InvalidOperationException("Cannot remove a group that doesn't exist.");
            m_Groups.Remove(group);
            m_RemovedGroups.Add(group);

            if (m_GroupItems.TryGetValue(group.guid, out var items))
            {
                foreach (IGroupItem groupItem in items.ToList())
                {
                    SetGroup(groupItem, null);
                }

                m_GroupItems.Remove(group.guid);
            }
        }

        public void AddStickyNote(StickyNoteData stickyNote)
        {
            if (m_StickyNotes.Contains(stickyNote))
            {
                throw new InvalidOperationException("Sticky note has already been added to the graph.");
            }

            if (!m_GroupItems.ContainsKey(stickyNote.groupGuid))
            {
                throw new InvalidOperationException("Trying to add sticky note with group that doesn't exist.");
            }

            m_StickyNotes.Add(stickyNote);
            m_AddedStickyNotes.Add(stickyNote);
            m_GroupItems[stickyNote.groupGuid].Add(stickyNote);
        }

        void RemoveNoteNoValidate(StickyNoteData stickyNote)
        {
            if (!m_StickyNotes.Contains(stickyNote))
            {
                throw new InvalidOperationException("Cannot remove a note that doesn't exist.");
            }

            m_StickyNotes.Remove(stickyNote);
            m_RemovedNotes.Add(stickyNote);

            if (m_GroupItems.TryGetValue(stickyNote.groupGuid, out var groupItems))
            {
                groupItems.Remove(stickyNote);
            }
        }

        public void RemoveStickyNote(StickyNoteData stickyNote)
        {
            RemoveNoteNoValidate(stickyNote);
            ValidateGraph();
        }

        public void SetGroup(IGroupItem node, GroupData group)
        {
            var groupChange = new ParentGroupChange()
            {
                groupItem = node,
                oldGroupGuid = node.groupGuid,
                // Checking if the groupdata is null. If it is, then it means node has been removed out of a group.
                // If the group data is null, then maybe the old group id should be removed
                newGroupGuid = group?.guid ?? Guid.Empty
            };
            node.groupGuid = groupChange.newGroupGuid;

            var oldGroupNodes = m_GroupItems[groupChange.oldGroupGuid];
            oldGroupNodes.Remove(node);

            m_GroupItems[groupChange.newGroupGuid].Add(node);
            m_ParentGroupChanges.Add(groupChange);
        }

        void AddNodeNoValidate(AbstractMaterialNode node)
        {
            if (node.groupGuid != Guid.Empty && !m_GroupItems.ContainsKey(node.groupGuid))
            {
                throw new InvalidOperationException("Cannot add a node whose group doesn't exist.");
            }
            node.owner = this;
            m_Nodes.Add(node);
            m_NodeDictionary.Add(node.guid, node);
            m_AddedNodes.Add(node);
            m_GroupItems[node.groupGuid].Add(node);
        }

        public void RemoveNode(AbstractMaterialNode node)
        {
            if (!node.canDeleteNode)
            {
                throw new InvalidOperationException($"Node {node.name} ({node.guid}) cannot be deleted.");
            }
            RemoveNodeNoValidate(node);
            ValidateGraph();
        }

        void RemoveNodeNoValidate(AbstractMaterialNode node)
        {
            if (!m_NodeDictionary.ContainsKey(node.guid))
            {
                throw new InvalidOperationException("Cannot remove a node that doesn't exist.");
            }

            m_Nodes.Remove(node);
            m_NodeDictionary.Remove(node.guid);
            messageManager?.RemoveNode(node.guid);
            m_RemovedNodes.Add(node);

            if (m_GroupItems.TryGetValue(node.groupGuid, out var groupItems))
            {
                groupItems.Remove(node);
            }
        }

        void AddEdgeToNodeEdges(IEdge edge)
        {
            List<IEdge> inputEdges;
            if (!m_NodeEdges.TryGetValue(edge.inputSlot.nodeGuid, out inputEdges))
                m_NodeEdges[edge.inputSlot.nodeGuid] = inputEdges = new List<IEdge>();
            inputEdges.Add(edge);

            List<IEdge> outputEdges;
            if (!m_NodeEdges.TryGetValue(edge.outputSlot.nodeGuid, out outputEdges))
                m_NodeEdges[edge.outputSlot.nodeGuid] = outputEdges = new List<IEdge>();
            outputEdges.Add(edge);
        }

        IEdge ConnectNoValidate(SlotReference fromSlotRef, SlotReference toSlotRef)
        {
            var fromNode = GetNodeFromGuid(fromSlotRef.nodeGuid);
            var toNode = GetNodeFromGuid(toSlotRef.nodeGuid);

            if (fromNode == null || toNode == null)
                return null;

            // if fromNode is already connected to toNode
            // do now allow a connection as toNode will then
            // have an edge to fromNode creating a cycle.
            // if this is parsed it will lead to an infinite loop.
            var dependentNodes = new List<AbstractMaterialNode>();
            NodeUtils.CollectNodesNodeFeedsInto(dependentNodes, toNode);
            if (dependentNodes.Contains(fromNode))
                return null;

            var fromSlot = fromNode.FindSlot<ISlot>(fromSlotRef.slotId);
            var toSlot = toNode.FindSlot<ISlot>(toSlotRef.slotId);

            if (fromSlot == null || toSlot == null)
                return null;

            if (fromSlot.isOutputSlot == toSlot.isOutputSlot)
                return null;

            var outputSlot = fromSlot.isOutputSlot ? fromSlotRef : toSlotRef;
            var inputSlot = fromSlot.isInputSlot ? fromSlotRef : toSlotRef;

            s_TempEdges.Clear();
            GetEdges(inputSlot, s_TempEdges);

            // remove any inputs that exits before adding
            foreach (var edge in s_TempEdges)
            {
                RemoveEdgeNoValidate(edge);
            }

            var newEdge = new Edge(outputSlot, inputSlot);
            m_Edges.Add(newEdge);
            m_AddedEdges.Add(newEdge);
            AddEdgeToNodeEdges(newEdge);

            //Debug.LogFormat("Connected edge: {0} -> {1} ({2} -> {3})\n{4}", newEdge.outputSlot.nodeGuid, newEdge.inputSlot.nodeGuid, fromNode.name, toNode.name, Environment.StackTrace);
            return newEdge;
        }

        public IEdge Connect(SlotReference fromSlotRef, SlotReference toSlotRef)
        {
            var newEdge = ConnectNoValidate(fromSlotRef, toSlotRef);
            ValidateGraph();
            return newEdge;
        }

        public void RemoveEdge(IEdge e)
        {
            RemoveEdgeNoValidate(e);
            ValidateGraph();
        }

        public void RemoveElements(AbstractMaterialNode[] nodes, IEdge[] edges, GroupData[] groups, StickyNoteData[] notes)
        {
            foreach (var node in nodes)
            {
                if (!node.canDeleteNode)
                {
                    throw new InvalidOperationException($"Node {node.name} ({node.guid}) cannot be deleted.");
                }
            }

            foreach (var edge in edges.ToArray())
            {
                RemoveEdgeNoValidate(edge);
            }

            foreach (var serializableNode in nodes)
            {
                // Check if it is a Redirect Node
                // Get the edges and then re-create all Edges
                // This only works if it has all the edges.
                // If one edge is already deleted then we can not re-create.
                if (serializableNode is RedirectNodeData redirectNode)
                {
                    redirectNode.GetOutputAndInputSlots(out SlotReference outputSlotRef, out var inputSlotRefs);

                    foreach (SlotReference slot in inputSlotRefs)
                    {
                        ConnectNoValidate(outputSlotRef, slot);
                    }
                }

                RemoveNodeNoValidate(serializableNode);
            }

            foreach (var noteData in notes)
            {
                RemoveNoteNoValidate(noteData);
            }

            foreach (var groupData in groups)
            {
                RemoveGroupNoValidate(groupData);
            }

            ValidateGraph();
        }

        void RemoveEdgeNoValidate(IEdge e)
        {
            e = m_Edges.FirstOrDefault(x => x.Equals(e));
            if (e == null)
                throw new ArgumentException("Trying to remove an edge that does not exist.", "e");
            m_Edges.Remove(e as Edge);

            List<IEdge> inputNodeEdges;
            if (m_NodeEdges.TryGetValue(e.inputSlot.nodeGuid, out inputNodeEdges))
                inputNodeEdges.Remove(e);

            List<IEdge> outputNodeEdges;
            if (m_NodeEdges.TryGetValue(e.outputSlot.nodeGuid, out outputNodeEdges))
                outputNodeEdges.Remove(e);

            m_RemovedEdges.Add(e);
        }

        public AbstractMaterialNode GetNodeFromGuid(Guid guid)
        {
            AbstractMaterialNode node;
            m_NodeDictionary.TryGetValue(guid, out node);
            return node;
        }

        public bool ContainsNodeGuid(Guid guid)
        {
            return m_NodeDictionary.ContainsKey(guid);
        }

        public T GetNodeFromGuid<T>(Guid guid) where T : AbstractMaterialNode
        {
            var node = GetNodeFromGuid(guid);
            if (node is T)
                return (T)node;
            return default(T);
        }

        public void GetEdges(SlotReference s, List<IEdge> foundEdges)
        {
            var node = GetNodeFromGuid(s.nodeGuid);
            if (node == null)
            {
                return;
            }
            ISlot slot = node.FindSlot<ISlot>(s.slotId);

            List<IEdge> candidateEdges;
            if (!m_NodeEdges.TryGetValue(s.nodeGuid, out candidateEdges))
                return;

            foreach (var edge in candidateEdges)
            {
                var cs = slot.isInputSlot ? edge.inputSlot : edge.outputSlot;
                if (cs.nodeGuid == s.nodeGuid && cs.slotId == s.slotId)
                    foundEdges.Add(edge);
            }
        }

        public IEnumerable<IEdge> GetEdges(SlotReference s)
        {
            var edges = new List<IEdge>();
            GetEdges(s, edges);
            return edges;
        }

        public InputCategory GetInputCategory(int index)
        {
            return m_Categories[index];
        }

        public int GetInputCategoryIndex(InputCategory category)
        {
            return m_Categories.IndexOf(category);
        }

        public InputCategory GetContainingInputCategory(ShaderInput input)
        {
            foreach (InputCategory category in m_Categories)
            {
                if (category.inputs.Contains(input))
                    return category;
            }

            return null;
        }

        public void AddInputCategory(InputCategory category)
        {
            owner.RegisterCompleteObjectUndo("Create Input Category");
            m_BlackboardSectionsChanged = true;
            m_Categories.Add(category);
        }

        public void RemoveInputCategory(InputCategory category)
        {
            owner.RegisterCompleteObjectUndo("Remove Input Category");
            m_BlackboardSectionsChanged = true;
            m_Categories.Remove(category);
        }

        public void MoveInputCategory(InputCategory category, int newIndex)
        {
            int currentIndex = GetInputCategoryIndex(category);
            if (currentIndex == -1)
                return;

            if (newIndex == currentIndex)
                return;

            owner.RegisterCompleteObjectUndo("Move Input Category");
            m_BlackboardSectionsChanged = true;

            m_Categories.RemoveAt(newIndex);
            if (newIndex > currentIndex)
                newIndex--;

            if (newIndex == m_Categories.Count)
                m_Categories.Add(category);
            else
                m_Categories.Insert(newIndex, category);
        }

        // TODO: z from merge with get graph input index
        // =======
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException();
        //     }
        //     m_AddedInputs.Add(input);

        public void AddShaderInputToDefaultCategory(ShaderInput input)
        {
            // TODO: y We can keep the "Properties" and "Keywords" default fields (?)
            AddShaderInput(input, m_Categories[0]);
        }

        public void AddShaderInput(ShaderInput input, InputCategory category, int index = -1)
        {
            if (input == null)
                return;
            if (properties.Contains(input) || keywords.Contains(input))
                return;

            owner.RegisterCompleteObjectUndo("Create Graph Input");
            m_AlteredCategories.Add(category);

            SanitizeGraphInputName(input);
            input.generatePropertyBlock = input.isExposable;

            category.AddInput(input, index);
            m_AlteredCategories.Add(category);

            if (input as ShaderKeyword != null)
            {
                OnKeywordChangedNoValidate();
            }
        }

        public void MoveInput(ShaderInput input, InputCategory fromCategory, InputCategory toCategory = null, int index = -1)
        {
            if (fromCategory == null)
                return;

            if (fromCategory == toCategory || toCategory == null)
            {
                MoveInputWithinCategory(input, fromCategory, index);
                return;
            }

            Debug.Log("index is ..." + index);

            fromCategory.RemoveInputByGuid(input.guid);
            toCategory.AddInput(input, index);
        }

        void MoveInputWithinCategory(ShaderInput input, InputCategory category, int newIndex)
        {
            if (category != null)
            {
                owner.RegisterCompleteObjectUndo("Move Graph Input");
                m_AlteredCategories.Add(category);

                category.MoveShaderInput(input, newIndex);
            }
        }

        public void RemoveGraphInput(ShaderInput input)
        {
            switch(input)
            {
                case AbstractShaderProperty property:
                    var propetyNodes = GetNodes<PropertyNode>().Where(x => x.propertyGuid == input.guid).ToList();
                    foreach (var propNode in propetyNodes)
                        ReplacePropertyNodeWithConcreteNodeNoValidate(propNode);
                    break;
            }

            RemoveGraphInputNoValidate(input.guid);
            ValidateGraph();
        }

        void RemoveGraphInputNoValidate(Guid guid)
        {
            InputCategory category = GetContainingCategory(guid);
            owner.RegisterCompleteObjectUndo("Removed Graph Input");
            if (category != null)
                m_AlteredCategories.Add(category);

            category.RemoveInputByGuid(guid);
            m_AlteredCategories.Add(category);
        }

        public InputCategory GetContainingCategory(ShaderInput input)
        {
            foreach (InputCategory category in m_Categories)
            {
                if (category.inputs.Contains(input))
                    return category;
            }

            return null;
        }

        InputCategory GetContainingCategory(Guid guid)
        {
            foreach (InputCategory cateogory in m_Categories)
            {
                if (cateogory.inputs.Any(i => i.guid == guid))
                    return cateogory;
            }

            return null;
        }

        public void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            foreach (var prop in properties)
            {
                if(prop is GradientShaderProperty gradientProp && generationMode == GenerationMode.Preview)
                {
                    GradientUtil.GetGradientPropertiesForPreview(collector, gradientProp.referenceName, gradientProp.value);
                    continue;
                }

                collector.AddShaderProperty(prop);
            }
        }

        public void CollectShaderKeywords(KeywordCollector collector, GenerationMode generationMode)
        {
            foreach (var keyword in keywords)
            {
                collector.AddShaderKeyword(keyword);
            }

            // Always calculate permutations when collecting
            collector.CalculateKeywordPermutations();
        }

        public void SanitizeGraphInputName(ShaderInput input)
        {
            input.displayName = input.displayName.Trim();
            switch(input)
            {
                case AbstractShaderProperty property:
                    input.displayName = GraphUtil.SanitizeName(properties.Where(p => p.guid != input.guid).Select(p => p.displayName), "{0} ({1})", input.displayName);
                    break;
                case ShaderKeyword keyword:
                    input.displayName = GraphUtil.SanitizeName(keywords.Where(p => p.guid != input.guid).Select(p => p.displayName), "{0} ({1})", input.displayName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SanitizeGraphInputReferenceName(ShaderInput input, string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return;

            string name = newName.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            if (Regex.IsMatch(name, @"^\d+"))
                name = "_" + name;

            name = Regex.Replace(name, @"(?:[^A-Za-z_0-9])|(?:\s)", "_");
            switch(input)
            {
                case AbstractShaderProperty property:
                    property.overrideReferenceName = GraphUtil.SanitizeName(properties.Where(p => p.guid != property.guid).Select(p => p.referenceName), "{0}_{1}", name);
                    break;
                case ShaderKeyword keyword:
                    keyword.overrideReferenceName = GraphUtil.SanitizeName(keywords.Where(p => p.guid != input.guid).Select(p => p.referenceName), "{0}_{1}", name).ToUpper();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static List<IEdge> s_TempEdges = new List<IEdge>();

        public void ReplacePropertyNodeWithConcreteNode(PropertyNode propertyNode)
        {
            ReplacePropertyNodeWithConcreteNodeNoValidate(propertyNode);
            ValidateGraph();
        }

        void ReplacePropertyNodeWithConcreteNodeNoValidate(PropertyNode propertyNode)
        {
            var property = properties.FirstOrDefault(x => x.guid == propertyNode.propertyGuid);
            if (property == null)
                return;

            var node = property.ToConcreteNode() as AbstractMaterialNode;
            if (node == null)
                return;

            var slot = propertyNode.FindOutputSlot<MaterialSlot>(PropertyNode.OutputSlotId);
            var newSlot = node.GetOutputSlots<MaterialSlot>().FirstOrDefault(s => s.valueType == slot.valueType);
            if (newSlot == null)
                return;

            node.drawState = propertyNode.drawState;
            node.groupGuid = propertyNode.groupGuid;
            AddNodeNoValidate(node);

            foreach (var edge in this.GetEdges(slot.slotReference))
                ConnectNoValidate(newSlot.slotReference, edge.inputSlot);

            RemoveNodeNoValidate(propertyNode);
        }

        public void OnKeywordChanged()
        {
            OnKeywordChangedNoValidate();
            ValidateGraph();
        }

        public void OnKeywordChangedNoValidate()
        {
            var allNodes = GetNodes<AbstractMaterialNode>();
            foreach(AbstractMaterialNode node in allNodes)
            {
                node.Dirty(ModificationScope.Topological);
                node.ValidateNode();
            }
        }

        public void CleanupGraph()
        {
            //First validate edges, remove any
            //orphans. This can happen if a user
            //manually modifies serialized data
            //of if they delete a node in the inspector
            //debug view.
            foreach (var edge in edges.ToArray())
            {
                var outputNode = GetNodeFromGuid(edge.outputSlot.nodeGuid);
                var inputNode = GetNodeFromGuid(edge.inputSlot.nodeGuid);

                MaterialSlot outputSlot = null;
                MaterialSlot inputSlot = null;
                if (outputNode != null && inputNode != null)
                {
                    outputSlot = outputNode.FindOutputSlot<MaterialSlot>(edge.outputSlot.slotId);
                    inputSlot = inputNode.FindInputSlot<MaterialSlot>(edge.inputSlot.slotId);
                }

                if (outputNode == null
                    || inputNode == null
                    || outputSlot == null
                    || inputSlot == null)
                {
                    //orphaned edge
                    RemoveEdgeNoValidate(edge);
                }
            }
        }

        public void ValidateGraph()
        {
            messageManager?.ClearAllFromProvider(this);
            CleanupGraph();
            GraphSetup.SetupGraph(this);
            GraphConcretization.ConcretizeGraph(this);
            GraphValidation.ValidateGraph(this);

            foreach (var edge in m_AddedEdges.ToList())
            {
                if (!ContainsNodeGuid(edge.outputSlot.nodeGuid) || !ContainsNodeGuid(edge.inputSlot.nodeGuid))
                {
                    Debug.LogWarningFormat("Added edge is invalid: {0} -> {1}\n{2}", edge.outputSlot.nodeGuid, edge.inputSlot.nodeGuid, Environment.StackTrace);
                    m_AddedEdges.Remove(edge);
                }
            }

            foreach (var groupChange in m_ParentGroupChanges.ToList())
            {
                if (groupChange.groupItem is AbstractMaterialNode node && !ContainsNodeGuid(node.guid))
                {
                    m_ParentGroupChanges.Remove(groupChange);
                }

                if (groupChange.groupItem is StickyNoteData stickyNote && !m_StickyNotes.Contains(stickyNote))
                {
                    m_ParentGroupChanges.Remove(groupChange);
                }
            }
        }

        public void AddValidationError(Guid id, string errorMessage,
            ShaderCompilerMessageSeverity severity = ShaderCompilerMessageSeverity.Error)
        {
            messageManager?.AddOrAppendError(this, id, new ShaderMessage("Validation: " + errorMessage, severity));
        }

        public void AddSetupError(Guid id, string errorMessage,
            ShaderCompilerMessageSeverity severity = ShaderCompilerMessageSeverity.Error)
        {
            messageManager?.AddOrAppendError(this, id, new ShaderMessage("Setup: " + errorMessage, severity));
        }

        public void AddConcretizationError(Guid id, string errorMessage,
            ShaderCompilerMessageSeverity severity = ShaderCompilerMessageSeverity.Error)
        {
            messageManager?.AddOrAppendError(this, id, new ShaderMessage("Concretization: " + errorMessage, severity));
        }

        public void ClearErrorsForNode(AbstractMaterialNode node)
        {
            messageManager?.ClearNodesFromProvider(this, node.ToEnumerable());
        }

        public void ReplaceWith(GraphData other)
        {
            if (other == null)
                throw new ArgumentException("Can only replace with another AbstractMaterialGraph", "other");

            using (var removedInputsPooledObject = ListPool<Guid>.GetDisposable())
            {
                var removedInputGuids = removedInputsPooledObject.value;
                foreach (var input in shaderInputs)
                    removedInputGuids.Add(input.guid);
                foreach (var inputGuid in removedInputGuids)
                    RemoveGraphInputNoValidate(inputGuid);
            }

            foreach (var otherInput in other.shaderInputs)
            {
                if (!shaderInputs.Any(p => p.guid == otherInput.guid))
                    AddShaderInputToDefaultCategory(otherInput);
            }

            other.ValidateGraph();
            ValidateGraph();

            // Current tactic is to remove all nodes and edges and then re-add them, such that depending systems
            // will re-initialize with new references.

            using (var removedGroupsPooledObject = ListPool<GroupData>.GetDisposable())
            {
                var removedGroupDatas = removedGroupsPooledObject.value;
                removedGroupDatas.AddRange(m_Groups);
                foreach (var groupData in removedGroupDatas)
                {
                    RemoveGroupNoValidate(groupData);
                }
            }

            using (var removedNotesPooledObject = ListPool<StickyNoteData>.GetDisposable())
            {
                var removedNoteDatas = removedNotesPooledObject.value;
                removedNoteDatas.AddRange(m_StickyNotes);
                foreach (var groupData in removedNoteDatas)
                {
                    RemoveNoteNoValidate(groupData);
                }
            }

            using (var pooledList = ListPool<IEdge>.GetDisposable())
            {
                var removedNodeEdges = pooledList.value;
                removedNodeEdges.AddRange(m_Edges);
                foreach (var edge in removedNodeEdges)
                    RemoveEdgeNoValidate(edge);
            }

            using (var removedNodesPooledObject = ListPool<Guid>.GetDisposable())
            {
                var removedNodeGuids = removedNodesPooledObject.value;
                removedNodeGuids.AddRange(m_Nodes.Where(n => n != null).Select(n => n.guid));
                foreach (var nodeGuid in removedNodeGuids)
                    RemoveNodeNoValidate(m_NodeDictionary[nodeGuid]);
            }

            ValidateGraph();

            foreach (GroupData groupData in other.groups)
                AddGroup(groupData);

            foreach (var stickyNote in other.stickyNotes)
            {
                AddStickyNote(stickyNote);
            }

            foreach (var node in other.GetNodes<AbstractMaterialNode>())
                AddNodeNoValidate(node);

            foreach (var edge in other.edges)
                ConnectNoValidate(edge.outputSlot, edge.inputSlot);

            ValidateGraph();
        }

        internal void PasteGraph(CopyPasteGraph graphToPaste, List<AbstractMaterialNode> remappedNodes,
            List<IEdge> remappedEdges)
        {
            var groupGuidMap = new Dictionary<Guid, Guid>();
            foreach (var group in graphToPaste.groups)
            {
                var position = group.position;
                position.x += 30;
                position.y += 30;

                GroupData newGroup = new GroupData(group.title, position);

                var oldGuid = group.guid;
                var newGuid = newGroup.guid;
                groupGuidMap[oldGuid] = newGuid;

                AddGroup(newGroup);
                m_PastedGroups.Add(newGroup);
            }

            foreach (var stickyNote in graphToPaste.stickyNotes)
            {
                var position = stickyNote.position;
                position.x += 30;
                position.y += 30;

                StickyNoteData pastedStickyNote = new StickyNoteData(stickyNote.title, stickyNote.content, position);
                if (groupGuidMap.ContainsKey(stickyNote.groupGuid))
                {
                    pastedStickyNote.groupGuid = groupGuidMap[stickyNote.groupGuid];
                }

                AddStickyNote(pastedStickyNote);
                m_PastedStickyNotes.Add(pastedStickyNote);
            }

            var nodeGuidMap = new Dictionary<Guid, Guid>();
            var nodeList = graphToPaste.GetNodes<AbstractMaterialNode>();
            foreach (var node in nodeList)
            {
                AbstractMaterialNode pastedNode = node;

                var oldGuid = node.guid;
                var newGuid = node.RewriteGuid();
                nodeGuidMap[oldGuid] = newGuid;

                // Check if the property nodes need to be made into a concrete node.
                if (node is PropertyNode propertyNode)
                {
                    // If the property is not in the current graph, do check if the
                    // property can be made into a concrete node.
                    if (!properties.Select(x => x.guid).Contains(propertyNode.propertyGuid))
                    {
                        // If the property is in the serialized paste graph, make the property node into a property node.
                        var pastedGraphMetaProperties = graphToPaste.metaProperties.Where(x => x.guid == propertyNode.propertyGuid);
                        if (pastedGraphMetaProperties.Any())
                        {
                            pastedNode = pastedGraphMetaProperties.FirstOrDefault().ToConcreteNode();
                            pastedNode.drawState = node.drawState;
                            nodeGuidMap[oldGuid] = pastedNode.guid;
                        }
                    }
                }

                AbstractMaterialNode abstractMaterialNode = (AbstractMaterialNode)node;

                // If the node has a group guid and no group has been copied, reset the group guid.
                // Check if the node is inside a group
                if (abstractMaterialNode.groupGuid != Guid.Empty)
                {
                    if (groupGuidMap.ContainsKey(abstractMaterialNode.groupGuid))
                    {
                        var absNode = pastedNode as AbstractMaterialNode;
                        absNode.groupGuid = groupGuidMap[abstractMaterialNode.groupGuid];
                        pastedNode = absNode;
                    }
                    else
                    {
                        pastedNode.groupGuid = Guid.Empty;
                    }
                }

                remappedNodes.Add(pastedNode);
                AddNode(pastedNode);

                // add the node to the pasted node list
                m_PastedNodes.Add(pastedNode);

                // Check if the keyword nodes need to have their keywords copied.
                if (node is KeywordNode keywordNode)
                {
                    // If the keyword is not in the current graph and is in the serialized paste graph copy it.
                    if (!keywords.Select(x => x.guid).Contains(keywordNode.keywordGuid))
                    {
                        var pastedGraphMetaKeywords = graphToPaste.metaKeywords.Where(x => x.guid == keywordNode.keywordGuid);
                        if (pastedGraphMetaKeywords.Any())
                        {
                            var keyword = pastedGraphMetaKeywords.FirstOrDefault(x => x.guid == keywordNode.keywordGuid);
                            SanitizeGraphInputName(keyword);
                            SanitizeGraphInputReferenceName(keyword, keyword.overrideReferenceName);
                            AddShaderInputToDefaultCategory(keyword);
                        }
                    }

                    // Always update Keyword nodes to handle any collisions resolved on the Keyword
                    keywordNode.UpdateNode();
                }
            }

            // only connect edges within pasted elements, discard
            // external edges.
            foreach (var edge in graphToPaste.edges)
            {
                var outputSlot = edge.outputSlot;
                var inputSlot = edge.inputSlot;

                Guid remappedOutputNodeGuid;
                Guid remappedInputNodeGuid;
                if (nodeGuidMap.TryGetValue(outputSlot.nodeGuid, out remappedOutputNodeGuid)
                    && nodeGuidMap.TryGetValue(inputSlot.nodeGuid, out remappedInputNodeGuid))
                {
                    var outputSlotRef = new SlotReference(remappedOutputNodeGuid, outputSlot.slotId);
                    var inputSlotRef = new SlotReference(remappedInputNodeGuid, inputSlot.slotId);
                    remappedEdges.Add(Connect(outputSlotRef, inputSlotRef));
                }
            }

            ValidateGraph();
        }

        public void OnBeforeSerialize()
        {
            var nodes = GetNodes<AbstractMaterialNode>().ToList();
            nodes.Sort((x1, x2) => x1.guid.CompareTo(x2.guid));
            m_SerializableNodes = SerializationHelper.Serialize(nodes.AsEnumerable());
            m_Edges.Sort();
            m_SerializableEdges = SerializationHelper.Serialize<Edge>(m_Edges);

            foreach (InputCategory category in m_Categories)
            {
                category.OnBeforeSerialize();
            }
            m_SerializedCategories = SerializationHelper.Serialize<InputCategory>(m_Categories);

            m_ActiveOutputNodeGuidSerialized = m_ActiveOutputNodeGuid == Guid.Empty ? null : m_ActiveOutputNodeGuid.ToString();
        }

        public void OnAfterDeserialize()
        {
            // Have to deserialize 'globals' before nodes
            m_Categories = SerializationHelper.Deserialize<InputCategory>(m_SerializedCategories, GraphUtil.GetLegacyTypeRemapping());
            foreach (InputCategory category in m_Categories)
            {
                category.OnAfterDeserialize();
            }

            var nodes = SerializationHelper.Deserialize<AbstractMaterialNode>(m_SerializableNodes, GraphUtil.GetLegacyTypeRemapping());

            m_Nodes = new List<AbstractMaterialNode>(nodes.Count);
            m_NodeDictionary = new Dictionary<Guid, AbstractMaterialNode>(nodes.Count);

            foreach (var group in m_Groups)
            {
                m_GroupItems.Add(group.guid, new List<IGroupItem>());
            }

            foreach (var node in nodes)
            {
                node.owner = this;
                node.UpdateNodeAfterDeserialization();
                m_Nodes.Add(node);
                m_NodeDictionary.Add(node.guid, node);
                m_GroupItems[node.groupGuid].Add(node);
            }

            foreach (var stickyNote in m_StickyNotes)
            {
                m_GroupItems[stickyNote.groupGuid].Add(stickyNote);
            }

            m_SerializableNodes = null;

            m_Edges = SerializationHelper.Deserialize<Edge>(m_SerializableEdges, GraphUtil.GetLegacyTypeRemapping());
            m_SerializableEdges = null;
            foreach (var edge in m_Edges)
                AddEdgeToNodeEdges(edge);

            m_OutputNode = null;

            if (!isSubGraph)
            {
                if (string.IsNullOrEmpty(m_ActiveOutputNodeGuidSerialized))
                {
                    var node = (AbstractMaterialNode)GetNodes<IMasterNode>().FirstOrDefault();
                    if (node != null)
                    {
                        m_ActiveOutputNodeGuid = node.guid;
                    }
                }
                else
                {
                    m_ActiveOutputNodeGuid = new Guid(m_ActiveOutputNodeGuidSerialized);
                }
            }
        }

        public void OnEnable()
        {
            foreach (var node in GetNodes<AbstractMaterialNode>().OfType<IOnAssetEnabled>())
            {
                node.OnEnable();
            }

            UpdateTargets();

            ShaderGraphPreferences.onVariantLimitChanged += OnKeywordChanged;
        }

        public void OnDisable()
        {
            ShaderGraphPreferences.onVariantLimitChanged -= OnKeywordChanged;
        }

        public void UpdateTargets()
        {
            if(outputNode == null)
                return;

            // First get all valid TargetImplementations that are valid with the current graph
            List<ITargetImplementation> foundImplementations = new List<ITargetImplementation>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypesOrNothing())
                {
                    var isImplementation = !type.IsAbstract && !type.IsGenericType && type.IsClass && typeof(ITargetImplementation).IsAssignableFrom(type);
                    //for subgraph output nodes, preview target is the only valid target
                    if (outputNode is SubGraphOutputNode && isImplementation && typeof(DefaultPreviewTarget).IsAssignableFrom(type))
                    {
                        var implementation = (DefaultPreviewTarget)Activator.CreateInstance(type);
                        foundImplementations.Add(implementation);
                    }
                    else if (isImplementation && !foundImplementations.Any(s => s.GetType() == type))
                    {
                        var masterNode = GetNodeFromGuid(m_ActiveOutputNodeGuid) as IMasterNode;
                        var implementation = (ITargetImplementation)Activator.CreateInstance(type);
                        if(implementation.IsValid(masterNode))
                        {
                            foundImplementations.Add(implementation);
                        }
                    }
                }
            }

            // Next we get all Targets that have valid TargetImplementations
            List<ITarget> foundTargets = new List<ITarget>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypesOrNothing())
                {
                    var isTarget = !type.IsAbstract && !type.IsGenericType && type.IsClass && typeof(ITarget).IsAssignableFrom(type);
                    if (isTarget && !foundTargets.Any(s => s.GetType() == type))
                    {
                        var target = (ITarget)Activator.CreateInstance(type);
                        if(foundImplementations.Where(s => s.targetType == type).Any())
                            foundTargets.Add(target);
                    }
                }
            }

            m_ValidTargets = foundTargets;
            m_ValidImplementations = foundImplementations.Where(s => s.targetType == foundTargets[0].GetType()).ToList();
        }
    }

    [Serializable]
    class InspectorPreviewData
    {
        public SerializableMesh serializedMesh = new SerializableMesh();

        [NonSerialized]
        public Quaternion rotation = Quaternion.identity;

        [NonSerialized]
        public float scale = 1f;
    }
}
