using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.GraphToolsFoundation.Overdrive.Bridge;
using UnityEditor.GraphToolsFoundation.Overdrive.Tests.TestModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.GraphToolsFoundation.Overdrive.Tests.GraphElements
{
    class State : Overdrive.State
    {
        IGraphModel m_GraphModel;
        public override IGraphModel CurrentGraphModel => m_GraphModel;

        public State(IGraphModel graphModel) : base(null)
        {
            m_GraphModel = graphModel;
        }
    }

    class GraphViewTester
    {
        static readonly Rect k_WindowRect = new Rect(Vector2.zero, new Vector2(SelectionDragger.k_PanAreaWidth * 8, SelectionDragger.k_PanAreaWidth * 6));

        bool m_SnapToPortEnabled;
        bool m_SnapToBorderEnabled;
        bool m_SnapToGridEnabled;
        bool m_SnapToSpacingEnabled;
        float m_SpacingMarginValue;

        protected TestGraphViewWindow window { get; private set; }
        protected TestGraphView graphView { get; private set; }
        protected TestEventHelpers helpers { get; private set; }
        protected IGraphModel GraphModel => window.GraphModel;
        protected Store Store => window.Store;

        bool m_EnablePersistence;

        public GraphViewTester(bool enablePersistence = false)
        {
            m_EnablePersistence = enablePersistence;
        }

        bool m_SavedUseNewStylesheets;
        [SetUp]
        public virtual void SetUp()
        {
            m_SnapToPortEnabled = GraphViewSettings.UserSettings.EnableSnapToPort;
            m_SnapToBorderEnabled = GraphViewSettings.UserSettings.EnableSnapToBorders;
            m_SnapToGridEnabled = GraphViewSettings.UserSettings.EnableSnapToGrid;
            m_SnapToSpacingEnabled = GraphViewSettings.UserSettings.EnableSnapToSpacing;
            m_SpacingMarginValue = GraphViewSettings.UserSettings.SpacingMarginValue;

            GraphViewSettings.UserSettings.EnableSnapToPort = false;
            GraphViewSettings.UserSettings.EnableSnapToBorders = false;
            GraphViewSettings.UserSettings.EnableSnapToGrid = false;
            GraphViewSettings.UserSettings.EnableSnapToSpacing = false;

            m_SavedUseNewStylesheets = GraphElementHelper.UseNewStylesheets;
            GraphElementHelper.UseNewStylesheets = true;

            window = EditorWindow.GetWindowWithRect<TestGraphViewWindow>(k_WindowRect);

            if (!m_EnablePersistence)
                window.DisableViewDataPersistence();
            else
                window.ClearPersistentViewData();

            graphView = window.GraphView as TestGraphView;
            StylesheetsHelper.AddTestStylesheet(graphView, "Tests.uss");

            helpers = new TestEventHelpers(window);
        }

        [TearDown]
        public virtual void TearDown()
        {
            GraphElementHelper.UseNewStylesheets = m_SavedUseNewStylesheets;
            GraphElementFactory.RemoveAll(graphView);

            if (m_EnablePersistence)
                window.ClearPersistentViewData();

            Clear();

            GraphViewSettings.UserSettings.EnableSnapToPort = m_SnapToPortEnabled;
            GraphViewSettings.UserSettings.EnableSnapToBorders = m_SnapToBorderEnabled;
            GraphViewSettings.UserSettings.EnableSnapToGrid = m_SnapToGridEnabled;
            GraphViewSettings.UserSettings.EnableSnapToSpacing = m_SnapToSpacingEnabled;
            GraphViewSettings.UserSettings.SpacingMarginValue = m_SpacingMarginValue;
        }

        protected void Clear()
        {
            // See case: https://fogbugz.unity3d.com/f/cases/998343/
            // Clearing the capture needs to happen before closing the window
            MouseCaptureController.ReleaseMouse();
            if (window != null)
            {
                window.Close();
            }
        }

        protected IONodeModel CreateNode(string title = "", Vector2 position = default, int inCount = 0, int outCount = 0, int exeInCount = 0, int exeOutCount = 0, Orientation orientation = Orientation.Horizontal)
        {
            return CreateNode<IONodeModel>(title, position, inCount, outCount, exeInCount, exeOutCount, orientation);
        }

        protected TNodeModel CreateNode<TNodeModel>(string title, Vector2 position, int inCount = 0, int outCount = 0, int exeInCount = 0, int exeOutCount = 0, Orientation orientation = Orientation.Horizontal) where TNodeModel : IONodeModel, new()
        {
            var node = GraphModel.CreateNode<TNodeModel>(title);
            node.Position = position;
            node.InputCount = inCount;
            node.OuputCount = outCount;
            node.ExeInputCount = exeInCount;
            node.ExeOuputCount = exeOutCount;

            node.DefineNode();

            foreach (var portModel in node.Ports.Cast<PortModel>())
            {
                portModel.Orientation = orientation;
            }

            return node;
        }

        protected IEnumerator ConnectPorts(IPortModel fromPort, IPortModel toPort)
        {
            var originalEdgeCount = GraphModel.EdgeModels.Count;
            var fromPortUI = fromPort.GetUI<Port>(graphView);
            var toPortUI = toPort.GetUI<Port>(graphView);

            Assert.IsNotNull(fromPortUI);
            Assert.IsNotNull(toPortUI);

            // Drag an edge between the two ports
            helpers.DragTo(fromPortUI.GetGlobalCenter(), toPortUI.GetGlobalCenter());
            yield return null;

            graphView.RebuildUI(GraphModel, Store);
            yield return null;

            Assert.AreEqual(originalEdgeCount + 1, GraphModel.EdgeModels.Count, "Edge has not been created");
        }

        protected IPlacematModel CreatePlacemat(Rect posAndDim, string title = "", int zOrder = 0)
        {
            var pm = GraphModel.CreatePlacemat(title, posAndDim);
            pm.ZOrder = zOrder;
            return pm;
        }

        protected IStickyNoteModel CreateSticky(string title = "", string contents = "", Rect stickyRect = default)
        {
            var sticky = GraphModel.CreateStickyNote(stickyRect);
            sticky.Contents = contents;
            sticky.Title = title;
            return sticky;
        }

        public static void AssertVector2AreEqualWithinDelta(Vector2 expected, Vector2 actual, float withinDelta, string message = null)
        {
            Assert.AreEqual(expected.x, actual.x, withinDelta, message);
            Assert.AreEqual(expected.y, actual.y, withinDelta, message);
        }
    }
}
