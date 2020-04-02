using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.Searcher;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.ShaderGraph.UnitTests
{


    internal class GraphCreationUtils
    {

        private static readonly string TestGraphLocation = "Assets/Testing/CreatedTestGraphs/";
        private static readonly string TestPrefix = "_Test_";
        private const float CompilationTimeout = 30f;
        private static bool verbose = false;


        public static void CloseAllOpenShaderGraphWindows()
        {
            foreach (MaterialGraphEditWindow graphWindow in Resources.FindObjectsOfTypeAll<MaterialGraphEditWindow>())
            {
                graphWindow.Close();
            }
        }

        private static IEnumerator UntilGraphIsDoneCompiling(MaterialGraphEditWindow window, (string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            if(verbose)  Debug.Log($"CreationTests : Begin waiting until graph is done compiling at {Time.realtimeSinceStartup}");
            GraphEditorView graphEditorView = window.GetPrivateProperty<GraphEditorView>("graphEditorView");
            PreviewManager previewManager = graphEditorView.GetPrivateProperty<PreviewManager>("previewManager");
            Dictionary<Guid, PreviewRenderData> renderDatas = previewManager.GetPrivateField<Dictionary<Guid, PreviewRenderData>>("m_RenderDatas");
            bool allCompiled;
            float startTime = Time.realtimeSinceStartup;
            do
            {
                allCompiled = true;
                foreach (var renderDataKV in renderDatas)
                {
                    var renderData = renderDataKV.Value;
                    if (renderData != null && renderData.shaderData.isCompiling)
                    {
                        var isCompiled = true;
                        for (var i = 0; i < renderData.shaderData.mat.passCount; i++)
                        {
                            if (!ShaderUtil.IsPassCompiled(renderData.shaderData.mat, i))
                            {
                                isCompiled = false;
                                break;
                            }
                        }

                        if (!isCompiled)
                        {
                            allCompiled = false;
                            break;
                        }
                    }
                }
                if (Time.realtimeSinceStartup - startTime > CompilationTimeout)
                {
                    TrySaveWindows(debugData);
                    throw new TimeoutException("Graph took to long to compile");
                }
                yield return null;
            } while (!allCompiled);
            if(verbose)  Debug.Log($"CreationTests : Graph finished compiling in {Time.realtimeSinceStartup - startTime} seconds");
        }

        public static MaterialGraphEditWindow OpenShaderGraphWindowForAsset(string assetPath)
        {
            var window = EditorWindow.CreateWindow<MaterialGraphEditWindow>(typeof(MaterialGraphEditWindow), typeof(SceneView));
            window.Initialize(AssetDatabase.AssetPathToGUID(assetPath));
            return window;
        }

        public static void CreateEmptyTestGraph(string basedOnGraph)
        {
            if(verbose)  Debug.Log($"CreationTests : Begin CreateEmptyTestGraph on {basedOnGraph}");
            var window = OpenShaderGraphWindowForAsset(basedOnGraph);
            GraphObject graphObject = window.GetPrivateProperty<GraphObject>("graphObject");
            GraphData graphToCopy = graphObject.graph;

            GraphData graphData = new GraphData();
            var rootNode = Activator.CreateInstance(graphToCopy.outputNode.GetType()) as AbstractMaterialNode;
            rootNode.drawState = new DrawState
            {
                position = graphToCopy.outputNode.drawState.position,
                expanded = true
            };

            if (graphToCopy.isSubGraph)
            {
                if(verbose) Debug.Log("CreationTests : Graph was a subgraph - creating subgraph asset");
                graphData.isSubGraph = true;
                SubGraphOutputNode rootOutput = rootNode as SubGraphOutputNode;
                graphData.AddNode(rootOutput);
                SubGraphOutputNode outputNode = graphToCopy.outputNode as SubGraphOutputNode;
                List<MaterialSlot> tempSlots = new List<MaterialSlot>();
                outputNode.GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    rootOutput.AddSlot(slot.concreteValueType);
                }

                graphData.path = "Sub Graphs";
                string outputPath = TestGraphLocation + TestPrefix + Path.GetFileNameWithoutExtension(basedOnGraph)
                                  + '.' + ShaderSubGraphImporter.Extension;
                FileUtilities.WriteShaderGraphToDisk(outputPath, graphData);
                AssetDatabase.Refresh();
                window.UpdateAsset();
                window.Close();
            }
            else
            {
                if(verbose) Debug.Log("CreationTests : Graph was not a subgraph - creating shadergraph asset");
                graphData.AddNode(rootNode);
                graphData.path = "Shader Graphs";
                string outputPath = TestGraphLocation + TestPrefix + Path.GetFileNameWithoutExtension(basedOnGraph)
                                  + '.' + ShaderGraphImporter.Extension;
                FileUtilities.WriteShaderGraphToDisk(outputPath, graphData);
                AssetDatabase.Refresh();
                window.UpdateAsset();
                window.Close();
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Shader>(outputPath));
            }

        }

        private static void TrySaveWindows((string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            (string assetPath, bool printShader, MaterialGraphEditWindow copyWindow, MaterialGraphEditWindow testGraphWindow) = debugData;
            if (copyWindow != null)
            {
                copyWindow.UpdateAsset();
                copyWindow.Close();
            }
            if (testGraphWindow != null)
            {
                testGraphWindow.UpdateAsset();
                if (printShader)
                {
                    string outputPath = TestGraphLocation + TestPrefix + Path.GetFileNameWithoutExtension(assetPath)
                                      + '.' + ShaderGraphImporter.Extension;
                    if(verbose) Debug.Log("CreationTests : " + ShaderGraphImporter.GetShaderText(outputPath, out List<PropertyCollector.TextureInfo> configuredTextures));
                }

                testGraphWindow.Close();
            }

        }

        private const float userTime = 0.1f;
        public static IEnumerator UserlikeGraphCreation(string assetPath, Action afterUserAddNode, Action afterUserAddEdge)
        {
            if(verbose)  Debug.Log($"CreationTests : Begining UserlikeGraphCreation on {assetPath}");
            MaterialGraphEditWindow copyWindow = null;
            MaterialGraphEditWindow testGraphWindow = null;
            GraphObject graphObjectToCopy = null;
            GraphObject testGraphObject = null;

            try
            {
                if(verbose) Debug.Log("CreationTests : Attempting to open MaterialGraphEditWindows and grab needed GraphObjects");
                copyWindow = OpenShaderGraphWindowForAsset(assetPath);
                graphObjectToCopy = copyWindow.GetPrivateProperty<GraphObject>("graphObject");
                string testGraphPath;
                if (graphObjectToCopy.graph.isSubGraph)
                {
                    testGraphPath = TestGraphLocation + TestPrefix + Path.GetFileNameWithoutExtension(assetPath)
                                  + '.' + ShaderSubGraphImporter.Extension;
                }
                else
                {
                    testGraphPath = TestGraphLocation + TestPrefix + Path.GetFileNameWithoutExtension(assetPath)
                                  + '.' + ShaderGraphImporter.Extension;
                }

                testGraphWindow = OpenShaderGraphWindowForAsset(testGraphPath);
                testGraphWindow.Focus();
                testGraphObject = testGraphWindow.GetPrivateProperty<GraphObject>("graphObject");
                if(verbose) Debug.Log("CreationTests : Succesfully opened MaterialGraphEditWindows and grabbed needed GraphObjects");
            }
            catch (Exception cantOpenWindowsOrAccessPrivatePropertyException)
            {
                try
                {
                    TrySaveWindows((assetPath, false, copyWindow, testGraphWindow));
                }
                catch (Exception cantSaveOrCloseWindowsException)
                {
                    Debug.LogError(cantSaveOrCloseWindowsException);
                }
                throw cantOpenWindowsOrAccessPrivatePropertyException;
            }
            var debugData = (assetPath, !testGraphObject.graph.isSubGraph, copyWindow, testGraphWindow);


            yield return UntilGraphIsDoneCompiling(testGraphWindow, debugData);

            var nodeLookup = new Dictionary<AbstractMaterialNode, AbstractMaterialNode>();
            var temporaryMarks = ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Get();
            var permanentMarks = ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Get();
            var slots = ListPool<MaterialSlot>.Get();
            var stack = StackPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Get();

            nodeLookup.Add(graphObjectToCopy.graph.outputNode, testGraphObject.graph.outputNode);

            if(verbose)  Debug.Log($"CreationTests : Begin main algorithm");
            foreach (var node in graphObjectToCopy.graph.GetNodes<AbstractMaterialNode>())
            {
                stack.Push((node, null, null));
            }
            while (stack.Count > 0)
            {
                if(verbose) Debug.Log("CreationTests : Start loop");
                if(verbose) Debug.Log("CreationTests : Current stack:");
                foreach(var item in stack)
                {
                    if(verbose)  Debug.Log($"CreationTests : -- node : {item.Item1.ToString()} slot to : {item.Item2?.ToString()} slot from : {item.Item3?.ToString()}");
                }
                (AbstractMaterialNode node, SlotReference? to, SlotReference? from) = stack.Pop();
                if(verbose)  Debug.Log($"CreationTests :  popped node : {node.ToString()} slot to : {to?.ToString()} slot from : {from?.ToString()}");
                if (permanentMarks.Contains((node, to, from)))
                {
                    if(verbose) Debug.Log("CreationTests : Permanent marks contains entry, continuing");
                    continue;
                }

                if (temporaryMarks.Contains((node, to, from)))
                {
                    if(verbose) Debug.Log("CreationTests : Temporary marks contains enty, checking if add is needed");
                    if (!nodeLookup.ContainsKey(node))
                    {
                        if(verbose) Debug.Log("CreationTests : Add needed");
                        yield return UserlikeAddNode(node, testGraphWindow, nodeLookup, debugData);
                        afterUserAddNode?.Invoke();
                    }

                    if (to.HasValue)
                    {
                        if(verbose) Debug.Log("CreationTests : To slot has a value, checking if add is needed");
                        AbstractMaterialNode toNode = graphObjectToCopy.graph.GetNodeFromGuid(to.Value.nodeGuid);
                        if (toNode != null && !nodeLookup.ContainsKey(toNode))
                        {
                            if(verbose) Debug.Log("CreationTests : Add needed");
                            yield return UserlikeAddNode(toNode, testGraphWindow, nodeLookup, debugData);
                            afterUserAddNode?.Invoke();
                            permanentMarks.Add((toNode, null, null));
                        }
                        Assert.IsTrue(from.HasValue);
                        try
                        {
                            if(verbose) Debug.Log("CreationTests : Adding edge");
                            UserlikeAddEdge(to.Value, from.Value, ref graphObjectToCopy, ref testGraphObject, ref nodeLookup);
                            afterUserAddEdge?.Invoke();
                        }
                        catch (Exception cantAddEdgeException)
                        {
                            StackPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(stack);
                            ListPool<MaterialSlot>.Release(slots);
                            ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(temporaryMarks);
                            ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(permanentMarks);
                            try
                            {
                                TrySaveWindows(debugData);
                            }
                            catch (Exception cantSaveOrCloseWindowsException)
                            {
                                Debug.LogError(cantSaveOrCloseWindowsException);
                            }
                            throw cantAddEdgeException;
                        }

                        yield return UntilGraphIsDoneCompiling(testGraphWindow, debugData);
                    }
                    permanentMarks.Add((node, to, from));
                }
                else
                {
                    if(verbose) Debug.Log("CreationTests : Neither permanent nor temporary marks contained entry, add entry and grab children");
                    temporaryMarks.Add((node, to, from));
                    stack.Push((node, to, from));
                    node.GetInputSlots(slots);
                    foreach (MaterialSlot inputSlot in slots)
                    {
                        var nodeEdges = graphObjectToCopy.graph.GetEdges(inputSlot.slotReference);
                        foreach (IEdge edge in nodeEdges)
                        {
                            var fromSocketRef = edge.outputSlot;
                            var childNode = graphObjectToCopy.graph.GetNodeFromGuid(fromSocketRef.nodeGuid);
                            if (childNode != null)
                            {
                                stack.Push((childNode, inputSlot.slotReference, fromSocketRef));
                            }
                        }
                    }
                    slots.Clear();
                }
            }
            if(verbose)  Debug.Log($"CreationTests : End main algorithm");
            yield return UntilGraphIsDoneCompiling(testGraphWindow, debugData);

            try
            {
                TrySaveWindows(debugData);
            }
            catch (Exception cantSaveAndCloseException)
            {
                throw cantSaveAndCloseException;
            }
            finally
            {
                StackPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(stack);
                ListPool<MaterialSlot>.Release(slots);
                ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(temporaryMarks);
                ListPool<(AbstractMaterialNode, SlotReference?, SlotReference?)>.Release(permanentMarks);
            }
        }



        public static IEnumerator UserlikeGraphCreation(string assetPath, Action afterUserAction = null)
        {
            return UserlikeGraphCreation(assetPath, afterUserAction, afterUserAction);
        }

        public static void UserlikeAddEdge(SlotReference originalTo,
                                           SlotReference originalFrom,
                                           ref GraphObject originalGraph,
                                           ref GraphObject copyGraph,
                                           ref Dictionary<AbstractMaterialNode, AbstractMaterialNode> nodeLookup)
        {
            AbstractMaterialNode toNodeOriginal = originalGraph.graph.GetNodeFromGuid(originalTo.nodeGuid);
            AbstractMaterialNode fromNodeOriginal = originalGraph.graph.GetNodeFromGuid(originalFrom.nodeGuid);

            Assert.IsNotNull(toNodeOriginal);
            Assert.IsNotNull(fromNodeOriginal);

            Assert.IsTrue(nodeLookup.ContainsKey(toNodeOriginal));
            Assert.IsTrue(nodeLookup.ContainsKey(fromNodeOriginal));

            AbstractMaterialNode toNodeCopy = nodeLookup[toNodeOriginal];
            AbstractMaterialNode fromNodeCopy = nodeLookup[fromNodeOriginal];



            SlotReference copyTo = new SlotReference(toNodeCopy.guid, originalTo.slotId);
            SlotReference copyFrom = new SlotReference(fromNodeCopy.guid, originalFrom.slotId);

            copyGraph.graph.Connect(copyFrom, copyTo);
        }

        /// <summary>
        /// Calls <see cref="UserlikeAddNodeUsingSearcherAndCopyValues(AbstractMaterialNode, GraphObject, MaterialGraphEditWindow, GraphEditorView)"/>
        /// and invokes <see cref="MaterialGraphView"/>.ConvertToProperty through reflection
        /// </summary>
        /// <param name="propertyNode"></param>
        /// <param name="graphObject"></param>
        /// <param name="graphEditWindow"></param>
        /// <param name="graphEditorView"></param>
        /// <param name="nodeLookup"></param>
        /// <returns></returns>
        private static IEnumerator UserlikeAddInlineNodeAndConvertToProperty(PropertyNode propertyNode,
                                                                             GraphObject graphObject,
                                                                             MaterialGraphEditWindow graphEditWindow,
                                                                             GraphEditorView graphEditorView,
                                                                             Dictionary<AbstractMaterialNode, AbstractMaterialNode> nodeLookup,
                                                                             (string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            if(verbose) Debug.Log("CreationTests : Begin UserlikeAddInlineNodeAndConvertToProperty");
            //find coresponding shader property
            AbstractShaderProperty property = propertyNode.owner.properties.FirstOrDefault(x => x.guid == propertyNode.propertyGuid);
            Assert.IsNotNull(property);

            //ToConcrete does not usually copy over drawstate, so create template node ourselves
            AbstractMaterialNode concrete = property.ToConcreteNode();
            concrete.drawState = propertyNode.drawState;
            concrete.owner = propertyNode.owner;
            yield return UserlikeAddNodeUsingSearcherAndCopyValues(concrete, graphObject, graphEditWindow, graphEditorView, debugData);
            Assert.IsNotNull(searcherAddedNode);

            yield return UntilGraphIsDoneCompiling(graphEditWindow, debugData);

            MaterialGraphView materialGraphView = graphEditorView.graphView;
            graphEditorView.HandleGraphChanges();
            materialGraphView.selection.Clear();
            materialGraphView.selection.Add(materialGraphView.nodes.ToList().OfType<MaterialNodeView>()
                                       .Where(n => n.node == searcherAddedNode).First());

            void convertSelectionToProperty() => materialGraphView.InvokePrivateAction("ConvertToProperty", new DropdownMenuAction[] { null });

            searcherAddedNode = AddNodeIndirect(graphObject.graph, convertSelectionToProperty, typeof(PropertyNode));
            Assert.IsNotNull(searcherAddedNode);

            nodeLookup.Add(propertyNode, searcherAddedNode);
            searcherAddedNode = null;
            if(verbose) Debug.Log("CreationTests : End UserlikeAddInlineNodeAndConvertToProperty");
        }

        private static AbstractMaterialNode searcherAddedNode;
        /// <summary>
        ///
        /// Try and add a node to the graph based on a template node in the most user way possible. This loads up the searcher and
        /// searches it for an entry that matches the <see cref="TitleAttribute"/> on the class if is not a <see cref="SubGraphNode"/>,
        /// otherwise searches with the <see cref="SubGraphNode"/>'s <see cref="SubGraphAsset.hlslName"/>. When it finds an entry, it
        /// invokes <see cref="SearcherProvider.OnSearcherSelectEntry(SearcherItem, Vector2)"/> with the found <see cref="SearcherItem"/>.
        /// Finally, it copies over all <see cref="MaterialSlot"/> values from <see cref="AbstractMaterialNode.GetInputSlots{T}(List{T})"/>
        /// as well as a reflection search of any Properties with the <see cref="IControlAttribute"/> decorator.
        ///
        /// </summary>
        /// <param name="node">Template <see cref="AbstractMaterialNode"/> to add to the test graph</param>
        /// <param name="graphObject"></param>
        /// <param name="graphEditWindow"></param>
        /// <param name="graphEditorView"></param>
        /// <returns></returns>
        private static IEnumerator UserlikeAddNodeUsingSearcherAndCopyValues(AbstractMaterialNode node,
                                                                             GraphObject graphObject,
                                                                             MaterialGraphEditWindow graphEditWindow,
                                                                             GraphEditorView graphEditorView,
                                                                             (string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            if(verbose) Debug.Log("CreationTests : Begin UserlikeAddNodeUsingSearcherAndCopyValues");

            //Need to access the searcherprovider to call OnSearcherSelectEntry
            SearchWindowProvider searchWindowProvider = graphEditorView.GetPrivateField<SearchWindowProvider>("m_SearchWindowProvider");
            searchWindowProvider.connectedPort = null;
            Searcher.Searcher searcher = (searchWindowProvider as SearcherProvider).LoadSearchWindow();
            SearcherWindow.Show(graphEditWindow, searcher,
                item => (searchWindowProvider as SearcherProvider).OnSearcherSelectEntry(item, graphEditWindow.position.center),
                graphEditWindow.position.center, null);

            yield return UntilGraphIsDoneCompiling(graphEditWindow, debugData);

            //get searcher entries
            Type newNodeType = node.GetType();
            IEnumerable<SearcherItem> results;
            if (node is SubGraphNode subGraphNode)
            {
                results = searcher.Search(subGraphNode.asset.hlslName);
            }
            else
            {
                TitleAttribute title = newNodeType.GetCustomAttributes(typeof(TitleAttribute), true).First() as TitleAttribute;
                results = searcher.Search(title.title.Last());
            }

            //find correct entry and invoke OnSearcherSelectEntry
            foreach (SearchNodeItem searchEntry in results)
            {
                if (searchEntry.NodeGUID.node != null && searchEntry.NodeGUID.node.GetType() == newNodeType)
                {
                    void searcherSelect() => ((SearcherProvider)searchWindowProvider)
                                             .OnSearcherSelectEntry(searchEntry, graphEditorView.graphView
                                                                                 .contentViewContainer
                                                                                 .LocalToWorld(node.drawState.position.position));
                    searcherAddedNode = AddNodeIndirect(graphObject.graph, searcherSelect, newNodeType);
                    break;
                }
            }
            Assert.IsNotNull(searcherAddedNode);
            searcherAddedNode.previewExpanded = false;

            var inputs = node.GetInputsWithNoConnection();
            foreach (var input in inputs)
            {
                MaterialSlot materialSlot = input as MaterialSlot;
                MaterialSlot newSlot = searcherAddedNode.FindInputSlot<MaterialSlot>(materialSlot.id);
                newSlot.CopyValuesFrom(materialSlot);
            }

            //Apply control values from the template node to our new node
            foreach (var property in node.GetType().GetProperties(System.Reflection.BindingFlags.Public
                                                                | System.Reflection.BindingFlags.NonPublic
                                                                | System.Reflection.BindingFlags.Instance
                                                                | System.Reflection.BindingFlags.FlattenHierarchy))
            {
                var enumControlsCheck = property.GetCustomAttributes(typeof(IControlAttribute), true);
                if (enumControlsCheck.Length > 0)
                {
                    property.SetValue(searcherAddedNode, property.GetValue(node));
                }
            }

            //Make sure the searcher window gets closed
            foreach (SearcherWindow searcherWindow in Resources.FindObjectsOfTypeAll<SearcherWindow>())
            {
                searcherWindow.Close();
            }
            if(verbose) Debug.Log("CreationTests : End UserlikeAddNodeUsingSearcherAndCopyValues");
        }

        private static IEnumerator DefaultAddNode(AbstractMaterialNode node,
                                                  GraphObject graphObject,
                                                  MaterialGraphEditWindow graphEditWindow,
                                                  GraphEditorView graphEditorView,
                                                  Dictionary<AbstractMaterialNode, AbstractMaterialNode> nodeLookup,
                                                  (string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            yield return UserlikeAddNodeUsingSearcherAndCopyValues(node, graphObject, graphEditWindow, graphEditorView, debugData);
            nodeLookup.Add(node, searcherAddedNode);
            searcherAddedNode = null;
        }

        private static AbstractMaterialNode AddNodeIndirect(GraphData graph, Action addAction, Type expectedNodeType)
        {
            var previouslyAdded = graph.addedNodes.ToList();
            addAction.Invoke();
            var newlyAdded = graph.addedNodes.Where(newlyAddedNode => previouslyAdded.Contains(newlyAddedNode) == false).ToList();
            Assert.IsTrue(newlyAdded.Count > 0);
            AbstractMaterialNode addedNode = newlyAdded.Where(newlyAddedNode => newlyAddedNode.GetType() == expectedNodeType).First();
            Assert.IsNotNull(addedNode);
            return addedNode;
        }
        /// <summary>
        /// Evaluates and performs the correct add funciton, and passes reflected refs where needed
        /// </summary>
        /// <param name="node"></param>
        /// <param name="testGraphWindow"></param>
        /// <param name="nodeLookup"></param>
        /// <returns></returns>
        public static IEnumerator UserlikeAddNode(AbstractMaterialNode node,
                                                  MaterialGraphEditWindow testGraphWindow,
                                                  Dictionary<AbstractMaterialNode, AbstractMaterialNode> nodeLookup,
                                                  (string, bool, MaterialGraphEditWindow, MaterialGraphEditWindow) debugData)
        {
            if(verbose)  Debug.Log($"CreationTests : Begin UserlikeAddNode");
            GraphEditorView graphEditorView = testGraphWindow.GetPrivateProperty<GraphEditorView>("graphEditorView");
            GraphObject graphObject = testGraphWindow.GetPrivateProperty<GraphObject>("graphObject");

            if (node is PropertyNode propertyNode)
            {
                if(verbose) Debug.Log("CreationTests : Node was a property node");
                yield return UserlikeAddInlineNodeAndConvertToProperty(propertyNode, graphObject, testGraphWindow, graphEditorView, nodeLookup, debugData);
            }
            else
            {
                if(verbose) Debug.Log("CreationTests : Node was not a property node");
                yield return DefaultAddNode(node, graphObject, testGraphWindow, graphEditorView, nodeLookup, debugData);
            }

            yield return UntilGraphIsDoneCompiling(testGraphWindow, debugData);
        }

    }

    [TestFixture]
    public class GraphCreationTests
    {
        class SmokeTestGraphCases
        {
            private static string[] graphLocation = { "Assets/CommonAssets/Graphs/BuildGraphTests" };
            public static IEnumerator TestCases
            {
                get
                {
                    string[] guids = AssetDatabase.FindAssets("", graphLocation);
                    return guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)) //Get Paths
                                .Where(assetPath => Path.GetExtension(assetPath).ToLower() == "." + ShaderGraphImporter.Extension
                                                 || Path.GetExtension(assetPath).ToLower() == "." + ShaderSubGraphImporter.Extension) //Only Shadergraphs
                                .Select(assetPath => new TestCaseData(new object[] { assetPath }).Returns(null)) //Setup data as expected by TestCaseSource
                                .GetEnumerator();
                }
            }
        }

        [OneTimeSetUp]
        public void Setup()
        {
            GraphCreationUtils.CloseAllOpenShaderGraphWindows();
        }


        [UnityTest, TestCaseSource(typeof(SmokeTestGraphCases), "TestCases")]
        public IEnumerator SmokeTests(string assetPath)
        {
            Debug.Log($"CreationTests : ===========Begin smoke test on {assetPath}");
            GraphCreationUtils.CreateEmptyTestGraph(assetPath);
            return GraphCreationUtils.UserlikeGraphCreation(assetPath);
        }

        [UnityTest, TestCase("Assets/CommonAssets/Graphs/BuildGraphTests/Shader_IslandWater.ShaderGraph", ExpectedResult = null)]
        public IEnumerator TestUndoRedo(string assetPath)
        {
            int undoCount = 0;
            int undoFrequency = 4;
            GraphCreationUtils.CreateEmptyTestGraph(assetPath);
            return GraphCreationUtils.UserlikeGraphCreation(assetPath, afterUserAction: () =>
            {
                undoCount = (undoCount + 1) % undoFrequency;
                if (undoCount % undoFrequency == undoFrequency - 1)
                {
                    Undo.PerformUndo();
                    Undo.PerformRedo();
                }
            });
        }
    }
}
