using System;
using System.Collections.Generic;

namespace CPF_experiment
{
    [Serializable]
    public class MAM_AgentState : IComparable<IBinaryHeapItem>, IBinaryHeapItem
    {
        public int agentIndex;
        /// <summary>
        /// The last move's time is the agent's G
        /// </summary>
        public TimedMove lastMove;
        private int binaryHeapIndex;
        public int g;
        public double f = 0;
        public double h;
        public List<double> heuristics;
        public int numOfAgentsInBestHeuristic; // for makespan
        public MAM_AgentState prev;
        public double hToMeeting;


        public MAM_AgentState
        (
            int pos_X,
            int pos_Y,
            int agentIndex,
            int time
        )
        {
            this.lastMove = new TimedMove(pos_X, pos_Y, Move.Direction.NO_DIRECTION, time);
            this.agentIndex = agentIndex;
            this.heuristics = new List<double>();
        }

        public MAM_AgentState
        (
            MAM_AgentState copy
        )
        {
            this.agentIndex = copy.agentIndex;
            this.lastMove = copy.lastMove;
            this.g = copy.g;
            this.h = copy.h;
            this.f = copy.f;
            this.prev = copy.prev;
            this.heuristics = new List<double>(copy.heuristics);
            this.numOfAgentsInBestHeuristic = copy.numOfAgentsInBestHeuristic;
        }



        /// <summary>
        /// BH_Item implementation
        /// </summary>
        public int GetIndexInHeap() { return binaryHeapIndex; }

        /// <summary>
        /// BH_Item implementation
        /// </summary>
        public void SetIndexInHeap(int index) { binaryHeapIndex = index; }

        /// <summary>
        /// When equivalence over different times is necessary,
        /// checks this.agent and last position only,
        /// ignoring data that would make this state different to other equivalent states:
        /// It doesn't matter from which direction the agent got to its current location.
        /// It's also necessary to ignore the agents' move time - we want the same positions
        /// in any time to be equivalent.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals
        (
            object obj
        )
        {
            MAM_AgentState that = (MAM_AgentState)obj;

            return this.agentIndex == that.agentIndex &&
                   this.lastMove.x == that.lastMove.x &&
                   this.lastMove.y == that.lastMove.y; // Ignoring the time and the direction
        }

        /// <summary>
        /// When equivalence over different times is necessary,
        /// uses this.agent and last position only, ignoring direction and time.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return 3 * this.agentIndex + 5 * this.lastMove.GetHashCode();
            }
        }

        /// <summary>
        /// Used when AgentState objects are put in the open list priority queue - mainly in AStarForSingleAgent, I think.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo
        (
            IBinaryHeapItem other
        )
        {
            MAM_AgentState that = (MAM_AgentState)other;
            if (this.f < that.f)
                return -1;
            if (this.f > that.f)
                return 1;

            // Prefer larger g:
            if (this.agentIndex == that.agentIndex)
            {
                if (this.g < that.g)
                    return 1;
                if (this.g > that.g)
                    return -1;

                if (this.h > that.h)
                    return 1;
                if (this.h < that.h)
                    return -1;
            }
            else
            {
                if (this.g > that.g)
                    return 1;
                if (this.g < that.g)
                    return -1;

                if (this.h < that.h)
                    return 1;
                if (this.h > that.h)
                    return -1;
            }
            return 0;
        }

        public override string ToString()
        {
            return " Agent: " + agentIndex + " time-" + g + " move " + this.lastMove + " f = " + this.f + ", g = " + this.g + ", h = " + this.h;
        }

        public List<MAM_AgentState> GetChildrenStates()
        {
            List<MAM_AgentState> children = new List<MAM_AgentState>();
            foreach (TimedMove nextMove in this.lastMove.GetNextMoves())
            {
                MAM_AgentState child = new MAM_AgentState(nextMove.x, nextMove.y, this.agentIndex, nextMove.time);
                child.g = this.g + 1;
                child.prev = this;
                children.Add(child);
            }
            return children;
        }
    }
}
