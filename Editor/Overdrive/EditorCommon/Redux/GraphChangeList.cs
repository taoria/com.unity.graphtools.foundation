using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    public class GraphChangeList
    {
        public List<IEdgeModel> DeletedEdges { get; set; } = new List<IEdgeModel>();
        public List<IGraphElementModel> ChangedElements { get; } = new List<IGraphElementModel>();
        public List<IGraphElementModel> ElementsToAutoAlign { get; } = new List<IGraphElementModel>();
        public int DeletedElements { get; set; }
        public bool BlackBoardChanged { get; set; }
        public bool RequiresRebuild { get; set; }

        public bool HasAnyTopologyChange()
        {
            return BlackBoardChanged || DeletedElements > 0 || ChangedElements.Any();
        }
    }
}
