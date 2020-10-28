using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.GraphToolsFoundation.Overdrive.VisualScripting
{
    public class Blackboard : Overdrive.Blackboard
    {
        new GtfoGraphView GraphView => base.GraphView as GtfoGraphView;

        public const string k_PersistenceKey = "Blackboard";

        Button m_AddButton;

        public Blackboard(Store store, GraphView graphView, bool windowed) : base(store, graphView)
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageTransitionHelper.VSTemplatePath + "Blackboard.uss"));

            AddToClassList("blackboard");

            scrollable = true;
            title = k_ClassLibraryTitle;
            subTitle = "";

            viewDataKey = string.Empty;

            addItemRequested = OnAddItemRequested;
            moveItemRequested = OnMoveItemRequested;

            // TODO 0.5: hack - we have two conflicting renaming systems
            // the blackboard one seems to win
            // for 0.4, just rewire it to dispatch the same action as ours
            editTextRequested = OnEditTextRequested;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            GraphView.OnSelectionChangedCallback += s =>
            {
                IGraphModel currentGraphModel = Store.GetState().CurrentGraphModel;
                if (currentGraphModel == null)
                    return;

                if (currentGraphModel == GraphView.LastGraphModel &&
                    (GraphView.Selection.LastOrDefault() is BlackboardField ||
                     GraphView.Selection.LastOrDefault() is IVisualScriptingField))
                {
                    currentGraphModel.LastChanges.RequiresRebuild = true;
                    return;
                }

                RebuildSections();
            };

            var header = this.Query("header").First();
            m_AddButton = header?.Query<Button>("addButton").First();
            if (m_AddButton != null)
                m_AddButton.style.visibility = Visibility.Hidden;

            this.windowed = windowed;
        }

        static void OnEditTextRequested(Overdrive.Blackboard blackboard, VisualElement blackboardField, string newName)
        {
            if (blackboardField is BlackboardVariableField field)
            {
                field.Store.Dispatch(new RenameElementAction((IRenamable)field.Model, newName));
                field.UpdateTitleFromModel();
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            UnityEditor.Selection.selectionChanged += OnSelectionChange;

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // ReSharper disable once DelegateSubtraction
            UnityEditor.Selection.selectionChanged -= OnSelectionChange;

            UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        void OnDragUpdated(DragUpdatedEvent e)
        {
            IGraphModel currentGraphModel = Store.GetState().CurrentGraphModel;
            if (currentGraphModel == null)
                return;
            var stencil = currentGraphModel.Stencil;
            var dragNDropHandler = stencil.DragNDropHandler;
            dragNDropHandler?.HandleDragUpdated(e, DragNDropContext.Blackboard);
            e.StopPropagation();
        }

        void OnDragPerform(DragPerformEvent e)
        {
            IGraphModel currentGraphModel = Store.GetState().CurrentGraphModel;
            if (currentGraphModel == null)
                return;
            var stencil = currentGraphModel.Stencil;
            var dragNDropHandler = stencil.DragNDropHandler;
            dragNDropHandler?.HandleDragPerform(e, Store, DragNDropContext.Blackboard, this);
            e.StopPropagation();
        }

        void OnSelectionChange()
        {
            IGraphModel currentGraphModel = Store.GetState().CurrentGraphModel;
            if (currentGraphModel == null || !(currentGraphModel.AssetModel as Object))
                return;

            if (currentGraphModel == GraphView.LastGraphModel &&
                (GraphView.Selection.LastOrDefault() is BlackboardField ||
                 GraphView.Selection.LastOrDefault() is IVisualScriptingField))
            {
                currentGraphModel.LastChanges.RequiresRebuild = true;
                return;
            }

            RebuildBlackboard();
        }

        void OnAddItemRequested(Overdrive.Blackboard blackboard)
        {
            var currentGraphModel = Store.GetState().CurrentGraphModel;
            currentGraphModel.Stencil.GetBlackboardProvider().AddItemRequested(Store, (BaseAction)null);
        }

        void OnMoveItemRequested(Overdrive.Blackboard blackboard, int index, VisualElement field)
        {
            var currentGraphModel = Store.GetState().CurrentGraphModel;
            currentGraphModel.Stencil.GetBlackboardProvider().MoveItemRequested(Store, index, field);
        }

        protected override void RebuildBlackboard()
        {
            base.RebuildBlackboard();

            IGraphElementModel elementModelToRename = Store.GetState().EditorDataModel?.ElementModelToRename;
            if (elementModelToRename != null)
            {
                IRenamableGraphElement elementToRename = GraphVariables.OfType<IRenamableGraphElement>()
                    .FirstOrDefault(x => ReferenceEquals(x.Model, elementModelToRename));
                if (elementToRename != null)
                    GraphView.ElementToRename = elementToRename;
            }

            GraphView.HighlightGraphElements();
        }

        protected override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var selectedModels = Selection.OfType<IGraphElement>().Select(e => e.Model).ToArray();
            if (selectedModels.Length > 0)
            {
                evt.menu.AppendAction("Delete", menuAction =>
                {
                    Store.Dispatch(new DeleteElementsAction(selectedModels));
                }, eventBase => DropdownMenuAction.Status.Normal);
            }
        }

        public void NotifyTopologyChange(IGraphModel graphModel)
        {
            SetPersistenceKeyFromGraphModel(graphModel);
        }

        void SetPersistenceKeyFromGraphModel(IGraphModel graphModel)
        {
            viewDataKey = graphModel?.GetAssetPath() + "__" + k_PersistenceKey;
        }
    }
}
