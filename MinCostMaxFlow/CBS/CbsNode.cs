using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using System.Text;
using System.IO;

namespace CPF_experiment
{
    [DebuggerDisplay("hash = {GetHashCode()}, f = {f}")]
    public class CbsNode : IComparable<IBinaryHeapItem>, IBinaryHeapItem
    {
        public ushort totalCost;
        public MAM_Plan mamPlan; // List of plans
        public long mamCost;

        public List<CbsConflict> nodeConflicts;

        /// For each agent in the problem instance, maps agent _nums_ of agents it collides with to the time bias of their first collision. (for range conflict)
        /// </summary>
        private int binaryHeapIndex;
        public CbsConflict conflict;
        public CbsConstraint constraint;
        /// <summary>
        /// Forcing an agent to be at a certain place at a certain time
        /// </summary>
        public CbsNode prev;
        public ushort depth;

        public enum ExpansionState: byte
        {
            NOT_EXPANDED = 0,
            DEFERRED,
            EXPANDED
        }
        /// <summary>
        /// For partial expansion
        /// </summary>
        public ExpansionState agentAExpansion;
        /// <summary>
        /// For partial expansion
        /// </summary>
        public ExpansionState agentBExpansion;

        protected CFM_CBS cbs;

        public CbsNode(int numberOfAgents, CFM_CBS cbs, ushort[] agentsGroupAssignment = null)
        {
            this.cbs = cbs;
            mamPlan = null;
            mamCost = -1;
            this.nodeConflicts = null;

            depth = 0;
            agentAExpansion = ExpansionState.NOT_EXPANDED;
            agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.prev = null;
            this.constraint = null;
        }

        /// <summary>
        /// Child from branch action constructor
        /// </summary>
        /// <param name="father"></param>
        /// <param name="newConstraint"></param>
        /// <param name="agentToReplan"></param>
        public CbsNode(CbsNode father, CbsConstraint newConstraint, int agentToReplan)
        {
            mamPlan = null;
            mamCost = -1;
            this.nodeConflicts = null;
            
            this.prev = father;
            this.constraint = newConstraint;
            this.depth = (ushort)(this.prev.depth + 1);
            this.agentAExpansion = ExpansionState.NOT_EXPANDED;
            this.agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.cbs = father.cbs;
        }

        /// <summary>
        /// Child from merge action constructor. FIXME: Code dup with previous constructor.
        /// </summary>
        /// <param name="father"></param>
        /// <param name="mergeGroupA"></param>
        /// <param name="mergeGroupB"></param>
        public CbsNode(CbsNode father, int mergeGroupA, int mergeGroupB)
        {
            mamPlan = null;
            mamCost = -1;
            this.nodeConflicts = null;
           
            this.prev = father;
            this.constraint = null;
            this.depth = (ushort)(this.prev.depth + 1);
            this.agentAExpansion = ExpansionState.NOT_EXPANDED;
            this.agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.cbs = father.cbs;

        }

        /// <summary>
        /// Solves the entire node - finds a plan for every agent group.
        /// Since this method is only called for the root of the constraint tree, every agent is in its own group.
        /// </summary>
        /// <returns></returns>
        public bool Solve()
        {
            this.totalCost = 0;
            MAM_ProblemInstance problem = this.cbs.GetProblemInstance();
            HashSet<CbsConstraint> newConstraints = this.GetConstraints(); // Probably empty as this is probably the root of the CT.

            // Constraints initiated with the problem instance
            //var constraints = (HashSet_U<CbsConstraint>)problem.parameters[MAPF_CBS.CONSTRAINTS];

            var constraints = new HashSet_U<CbsConstraint>();


            Dictionary<int, int> agentsWithConstraints = null;
            if (constraints.Count != 0)
            {
                int maxConstraintTimeStep = constraints.Max<CbsConstraint>(constraint => constraint.time);
                agentsWithConstraints = constraints.Select<CbsConstraint, int>(constraint => constraint.agentNum).Distinct().ToDictionary<int, int>(x => x); // ToDictionary because there's no ToSet...
            }


            constraints.Join(newConstraints);

            // This mechanism of adding the constraints to the possibly pre-existing constraints allows having
            // layers of CBS solvers, each one adding its own constraints and respecting those of the solvers above it.

            // Solve using MMMStar

            HashSet<MMStarConstraint> mConstraints = importCBSConstraintsToMMStarConstraints(constraints);

            this.cbs.runner.SolveGivenProblem(problem, mConstraints);
            this.mamPlan = this.cbs.runner.plan;
            this.mamCost = this.cbs.runner.solutionCost;

            // Gather conflicts

            this.nodeConflicts = gatherConflicts();



            //if(MAM_Run.toPrint)
            //    printConflicts(allSingleAgentPlans);


            this.isGoal = this.nodeConflicts.Count == 0;
            return true;
        }

        private List<CbsConflict> gatherConflicts()
        {
            nodeConflicts = new List<CbsConflict>();
            List<List<Move>> locationsList = this.mamPlan.listOfLocations;
            int maxPathLength = locationsList.Max(list => list.Count);
            for (int timeStamp = 0; timeStamp < maxPathLength - 1; timeStamp++)
            {
                Dictionary<Move, int> agentLocationsInTimeStamp = new Dictionary<Move, int>();
                for(int agentMoveIndex=0; agentMoveIndex < locationsList.Count; agentMoveIndex++)
                {
                    if (timeStamp < this.mamPlan.listOfLocations[agentMoveIndex].Count - 1)
                    {
                        Move agentMove = locationsList[agentMoveIndex][timeStamp];
                        if (agentLocationsInTimeStamp.ContainsKey(agentMove))
                            nodeConflicts.Add(new CbsConflict(agentMoveIndex, agentLocationsInTimeStamp[agentMove], agentMove, agentMove, timeStamp, timeStamp, timeStamp));
                        else
                            agentLocationsInTimeStamp[agentMove] = agentMoveIndex;
                    }
                }
            }
            return nodeConflicts;
        }

        private HashSet<MMStarConstraint> importCBSConstraintsToMMStarConstraints(HashSet_U<CbsConstraint> constraints)
        {
            HashSet<MMStarConstraint> mConstraints = new HashSet<MMStarConstraint>();
            foreach (CbsConstraint constraint in constraints)
                mConstraints.Add(new MMStarConstraint(constraint));
            return mConstraints;
        }

        private void printLinkedList(LinkedList<List<Move>> toPrint, bool writeToFile = false)
        {
            if (toPrint.Count == 0)
                return;
            PrintLine(writeToFile);
            LinkedListNode<List<Move>> node = toPrint.First;
            string[] columns = new string[node.Value.Count + 1];
            columns[0] = "";
            for (int agentNumber = 1; agentNumber < node.Value.Count + 1; agentNumber++)
            {
                columns[agentNumber] = (agentNumber - 1).ToString();

            }
            node = toPrint.First;
            PrintRow(writeToFile, columns);
            PrintLine(writeToFile);

            int time = 0;
            while (node != null)
            {
                columns = new string[node.Value.Count + 1];
                columns[0] = time.ToString();
                time++;
                List<Move> currentMoves = node.Value;
                for (int i = 0; i < currentMoves.Count; i++)
                {
                    Move currentMove = currentMoves[i];
                    columns[i + 1] = currentMove.x + "," + currentMove.y;
                }
                PrintRow(writeToFile, columns);
                node = node.Next;
            }
            PrintLine(writeToFile);
        }
        static int tableWidth = 200;

        static void PrintLine(bool writeToFile)
        {
            if (!writeToFile)
                Console.WriteLine(new string('-', tableWidth));
            else
            {
                string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = pathDesktop + "\\RobustLog.txt";
                using (StreamWriter file = File.AppendText(filePath))
                //using (System.IO.StreamWriter file = new System.IO.StreamWriter(filePath))
                {
                    file.WriteLine(new string('-', tableWidth));
                }
            }

        }

        static void PrintRow(bool writeToFile, params string[] columns)
        {
            int width = (tableWidth - columns.Length) / columns.Length;
            string row = "|";

            foreach (string column in columns)
            {
                row += AlignCentre(column, width) + "|";
            }
            if (!writeToFile)
                Console.WriteLine(row);
            else
            {
                string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = pathDesktop + "\\RobustLog.txt";
                using (StreamWriter file = File.AppendText(filePath))
                {
                    file.WriteLine(row);
                }
            }

        }

        static string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        }

        private bool listContainsZeros(Dictionary<int, List<int>> list)
        {
            foreach (KeyValuePair<int, List<int>> item in list)
                foreach(int singleItem in item.Value)
                    if (singleItem == 0)
                        return true;
            return false;
        }

        /// <summary>
        /// Used to preserve state of conflict iteration.
        /// </summary>
        private IEnumerator<CbsConflict> nextConflicts;

        /// <summary>
        /// The iterator holds the state of the generator, with all the different queues etc - a lot of memory.
        /// We also clear the MDDs that were built - if no child uses them, they'll be garbage-collected.
        /// </summary>
        public void ClearConflictChoiceData()
        {
            this.nextConflicts = null;
        }

        /// Returns whether another conflict was found
        public bool ChooseNextConflict()
        {
            bool hasNext = this.nextConflicts.MoveNext();
            if (hasNext)
                this.conflict = this.nextConflicts.Current;
            return hasNext;
        }

        /// <summary>
        /// Chooses an internal conflict to work on.
        /// Resets conflicts iteration if it's used.
        /// </summary>
        public void ChooseConflict()
        {
            if(this.nodeConflicts.Count != 0)
                this.conflict = this.nodeConflicts[0];
            else
                this.conflict = null;
        }

        /// <summary>
        /// Assuming the groups conflict, find the specific agents that conflict.
        /// Also sets largerConflictingGroupSize.
        /// </summary>
        /// <param name="aConflictingGroupMemberIndex"></param>
        /// <param name="bConflictingGroupMemberIndex"></param>
        /// <param name="time"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private void FindConflicting(int aConflictingGroupMemberIndex, int bConflictingGroupMemberIndex, int time, out int a, out int b,
                                     ISet<int>[] groups = null)
        {
            a = aConflictingGroupMemberIndex;
            b = bConflictingGroupMemberIndex;
        }

        public CbsConflict GetConflict()
        {
            return this.conflict;
        }

        
        /// <summary>
        /// Uses the group assignments and the constraints.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int ans = 0;

                HashSet<CbsConstraint> constraints = this.GetConstraints();

                foreach (CbsConstraint constraint in constraints)
                {
                    ans += constraint.GetHashCode();
                }

                return ans;
            }
        }

        /// <summary>
        /// Checks the group assignment and the constraints
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) 
        {
            CbsNode other = (CbsNode)obj;

            CbsNode current = this;
            HashSet<CbsConstraint> other_constraints = other.GetConstraints();
            HashSet<CbsConstraint> constraints = this.GetConstraints();

            foreach (CbsConstraint constraint in constraints)
            {
                if (other_constraints.Contains(constraint) == false)
                    return false;
                //current = current.prev;    dor comment
            }
            return constraints.Count == other_constraints.Count;
        }

        /// <summary>
        /// Worth doing because the node may always be in the closed list
        /// </summary>
        public void Clear()
        {
            this.mamPlan = null;
        }

        

        public int CompareTo(IBinaryHeapItem item)
        {
            CbsNode other = (CbsNode)item;


            return (int)(this.mamCost - other.mamCost);
        }

        public HashSet<CbsConstraint> GetConstraints()
        {
            var constraints = new HashSet<CbsConstraint>();
            CbsNode current = this;
            CbsConstraint currentConstraint = null;

            while (current.depth > 0) // The root has no constraints
            {

                if (current.constraint != null && // Next check not enough if "surprise merges" happen (merges taken from adopted child)
                    current.prev.conflict != null) // Can only happen for temporary lookahead nodes the were created and then later the parent adopted a goal node

                    currentConstraint = current.constraint;
                    TimedMove     currentMove       = current.constraint.move;
                    CbsConstraint newConstraint = new CbsConstraint(currentConstraint.agentNum, currentMove.x, currentMove.y, currentMove.direction, currentMove.time);
                    constraints.Add(newConstraint);
                    
                current = current.prev;
            }
            return constraints;
        }

        /// <summary>
        /// IBinaryHeapItem implementation
        /// </summary>
        /// <returns></returns>
        public int GetIndexInHeap() { return binaryHeapIndex; }

        /// <summary>
        /// IBinaryHeapItem implementation
        /// </summary>
        /// <returns></returns>
        public void SetIndexInHeap(int index) { binaryHeapIndex = index; }

        public MAM_Plan CalculateJointPlan()
        {
            return this.mamPlan;
        }          

        private bool isGoal = false;

        public bool GoalTest() {
            return isGoal;
        }

    }
}
