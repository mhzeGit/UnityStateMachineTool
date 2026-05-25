using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    [Serializable]
    public class BreakpointData
    {
        public int StateIndex;
        public List<int> ParentPath = new List<int>();
    }
}
