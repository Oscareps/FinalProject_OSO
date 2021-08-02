using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using System.Text;
using System.IO;

namespace CPF_experiment
{
    [DebuggerDisplay("hash = {GetHashCode()}, f = {f}")]
    public class CFMCbsNode : IComparable<IBinaryHeapItem>, IBinaryHeapItem
    {
        public ushort totalCost;
        public ushort h;
        public MAM_Plan mamPlan; // List of plans
        public long mamCost;
        public int[] allSingleAgentCosts;
        /// <summary>
        /// A lower estimate of the number of operations (replanning or merging) needed to solve the node.
        /// Used for tie-breaking.
        /// </summary>
        public int minOpsToSolve;
        /// <summary>
        /// For each agent in the problem instance, saves the number of agents from the problem instance that it conflicts with.
        /// Used for choosing the next conflict to resolve by replanning/merging/shuffling, and for tie-breaking.
        /// </summary>
        public int[] countsOfInternalAgentsThatConflict;
        /// <summary>
        /// Counts the number of external agents this node conflicts with.
        /// Used for tie-breaking.
        /// </summary>
        public int totalExternalAgentsThatConflict;
        /// <summary>
        /// Used for tie-breaking.
        /// </summary>
        public int totalConflictsWithExternalAgents;

        public List<CFMCbsConflict> nodeConflicts;

        /// For each agent in the problem instance, maps agent _nums_ of agents it collides with to the time bias of their first collision. (for range conflict)
        /// </summary>
        private int binaryHeapIndex;
        public CFMCbsConflict conflict;
        public CFMCbsConstraint constraint;
        /// <summary>
        /// Forcing an agent to be at a certain place at a certain time
        /// </summary>
        public CFMCbsNode prev;
        public ushort depth;
        public ushort[] agentsGroupAssignment;
        public ushort replanSize;
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
        //public ProblemInstance problem;
        protected ICbsSolver solver;
        protected ICbsSolver singleAgentSolver;
        protected CFM_CBS cbs;
        public Dictionary<int, int> agentNumToIndex;
        public bool parentAlreadyLookedAheadOf;
        /// <summary>
        /// For tie-breaking
        /// </summary>
        public int totalInternalAgentsThatConflict;
        /// <summary>
        /// For tie-breaking
        /// </summary>
        public int largerConflictingGroupSize;
        /// <summary>
        /// For tie-breaking
        /// </summary>
        public int totalConflictsBetweenInternalAgents;

        public CFMCbsNode(int numberOfAgents, CFM_CBS cbs, ushort[] agentsGroupAssignment = null)
        {
            this.cbs = cbs;
            mamPlan = null;
            mamCost = -1;
            allSingleAgentCosts = new int[numberOfAgents];
            countsOfInternalAgentsThatConflict = new int[numberOfAgents];
            this.nodeConflicts = null;
            if (agentsGroupAssignment == null)
            {
                this.agentsGroupAssignment = new ushort[numberOfAgents];
                for (ushort i = 0; i < numberOfAgents; i++)
                    this.agentsGroupAssignment[i] = i;
            }
            else
                this.agentsGroupAssignment = agentsGroupAssignment.ToArray<ushort>();
            agentNumToIndex = new Dictionary<int, int>();
            for (int i = 0; i < numberOfAgents; i++)
            {
                agentNumToIndex[this.cbs.GetProblemInstance().m_vAgents[i].agentIndex] = i;
            }
            depth = 0;
            replanSize = 1;
            agentAExpansion = ExpansionState.NOT_EXPANDED;
            agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.prev = null;
            this.constraint = null;
            this.solver = solver;
            this.singleAgentSolver = singleAgentSolver;
        }

        public int agentToReplan;

        /// <summary>
        /// Child from branch action constructor
        /// </summary>
        /// <param name="father"></param>
        /// <param name="newConstraint"></param>
        /// <param name="agentToReplan"></param>
        public CFMCbsNode(CFMCbsNode father, CFMCbsConstraint newConstraint, int agentToReplan)
        {
            this.agentToReplan = agentToReplan;
            mamPlan = null;
            mamCost = -1;
            this.allSingleAgentCosts = father.allSingleAgentCosts.ToArray<int>();
            this.countsOfInternalAgentsThatConflict = father.countsOfInternalAgentsThatConflict.ToArray<int>();
            this.nodeConflicts = null;
           
            this.agentsGroupAssignment = father.agentsGroupAssignment.ToArray<ushort>();
            this.agentNumToIndex = father.agentNumToIndex;
            this.prev = father;
            this.constraint = newConstraint;
            this.depth = (ushort)(this.prev.depth + 1);
            this.agentAExpansion = ExpansionState.NOT_EXPANDED;
            this.agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.replanSize = 1;
            this.solver = father.solver;
            this.singleAgentSolver = father.singleAgentSolver;
            this.cbs = father.cbs;
        }

        /// <summary>
        /// Child from merge action constructor. FIXME: Code dup with previous constructor.
        /// </summary>
        /// <param name="father"></param>
        /// <param name="mergeGroupA"></param>
        /// <param name="mergeGroupB"></param>
        public CFMCbsNode(CFMCbsNode father, int mergeGroupA, int mergeGroupB)
        {
            mamPlan = null;
            mamCost = -1;
            this.allSingleAgentCosts = father.allSingleAgentCosts.ToArray<int>();
            this.countsOfInternalAgentsThatConflict = father.countsOfInternalAgentsThatConflict.ToArray<int>();
            this.nodeConflicts = null;
           
            this.agentsGroupAssignment = father.agentsGroupAssignment.ToArray<ushort>();
            this.agentNumToIndex = father.agentNumToIndex;
            this.prev = father;
            this.constraint = null;
            this.depth = (ushort)(this.prev.depth + 1);
            this.agentAExpansion = ExpansionState.NOT_EXPANDED;
            this.agentBExpansion = ExpansionState.NOT_EXPANDED;
            this.replanSize = 1;
            this.solver = father.solver;
            this.singleAgentSolver = father.singleAgentSolver;
            this.cbs = father.cbs;

        }

        public int f
        {
            get { return this.totalCost + this.h; }
        }

        /// <summary>
        /// Solves the entire node - finds a plan for every agent group.
        /// Since this method is only called for the root of the constraint tree, every agent is in its own group.
        /// </summary>
        /// <param name="depthToReplan"></param>
        /// <returns></returns>
        public bool Solve()
        {
            this.totalCost = 0;
            ProblemInstance problem = this.cbs.GetProblemInstance();
            HashSet<CFMCbsConstraint> newConstraints = this.GetConstraints(); // Probably empty as this is probably the root of the CT.

            // Constraints initiated with the problem instance
            //var constraints = (HashSet_U<CbsConstraint>)problem.parameters[MAPF_CBS.CONSTRAINTS];

            var constraints = new HashSet_U<CFMCbsConstraint>();


            Dictionary<int, int> agentsWithConstraints = null;
            if (constraints.Count != 0)
            {
                int maxConstraintTimeStep = constraints.Max<CFMCbsConstraint>(constraint => constraint.time);
                agentsWithConstraints = constraints.Select<CFMCbsConstraint, int>(constraint => constraint.agentNum).Distinct().ToDictionary<int, int>(x => x); // ToDictionary because there's no ToSet...
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

        private List<CFMCbsConflict> gatherConflicts()
        {
            nodeConflicts = new List<CFMCbsConflict>();
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
                            nodeConflicts.Add(new CFMCbsConflict(agentMoveIndex, agentLocationsInTimeStamp[agentMove], agentMove, agentMove, timeStamp, timeStamp, timeStamp));
                        else
                            agentLocationsInTimeStamp[agentMove] = agentMoveIndex;
                    }
                }
            }
            return nodeConflicts;
        }

        private HashSet<MMStarConstraint> importCBSConstraintsToMMStarConstraints(HashSet_U<CFMCbsConstraint> constraints)
        {
            HashSet<MMStarConstraint> mConstraints = new HashSet<MMStarConstraint>();
            foreach (CFMCbsConstraint constraint in constraints)
                mConstraints.Add(new MMStarConstraint(constraint));
            return mConstraints;
        }

        //private void printConflicts(SinglePlan[] allSingleAgentPlans)
        //{
        //    for (int agentDictionaryIndex = 0; agentDictionaryIndex < conflictCountsPerAgent.Count(); agentDictionaryIndex ++ )
        //    {
        //        Dictionary<int,int> agentCountDictionary                    = conflictCountsPerAgent[agentDictionaryIndex];
        //        Dictionary<int, List<int>> agentTimesDictionary             = conflictTimesPerAgent[agentDictionaryIndex];

        //        foreach(int key in agentTimesDictionary.Keys)
        //        {
        //            List<int> agentTimesDictionaryList       = agentTimesDictionary[key];
        //            for(int i = 0; i < agentTimesDictionaryList.Count; i++)
        //            {
        //                Move move;
        //                if (agentTimesDictionaryList[i] >= allSingleAgentPlans[agentDictionaryIndex].locationAtTimes.Count)
        //                    move = allSingleAgentPlans[agentDictionaryIndex].locationAtTimes[allSingleAgentPlans[agentDictionaryIndex].locationAtTimes.Count - 1];
        //                else
        //                    move = allSingleAgentPlans[agentDictionaryIndex].locationAtTimes[agentTimesDictionaryList[i]];
        //                Console.WriteLine("Agent " + agentDictionaryIndex + " Collinding Agent " + key + " At Time " + agentTimesDictionaryList[i] + " Location " + move);
        //            }
        //        }
        //    }
        //}


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

        public void Print()
        {
            //Debug.WriteLine("");
            //Debug.WriteLine("");
            //Debug.WriteLine("Node hash: " + this.GetHashCode());
            //Debug.WriteLine("Total cost so far: " + this.totalCost);
            //Debug.WriteLine("h: " + this.h);
            //Debug.WriteLine("Min estimated ops needed: " + this.minOpsToSolve);
            //Debug.WriteLine("Expansion state: " + this.agentAExpansion + ", " + this.agentBExpansion);
            //Debug.WriteLine("Num of external agents that conflict: " + totalExternalAgentsThatConflict);
            //Debug.WriteLine("Num of internal agents that conflict: " + totalInternalAgentsThatConflict);
            //Debug.WriteLine("Num of conflicts between internal agents: " + totalConflictsBetweenInternalAgents);
            //Debug.WriteLine("Node depth: " + this.depth);
            //if (this.prev != null)
            //    Debug.WriteLine("Parent hash: " + this.prev.GetHashCode());
            //IList<CbsConstraint> constraints = this.GetConstraintsOrdered();
            //Debug.WriteLine(constraints.Count.ToString() + " relevant internal constraints so far: ");
            //foreach (CbsConstraint constraint in constraints)
            //{
            //    Debug.WriteLine(constraint);
            //}
            //MAM_ProblemInstance problem = this.cbs.GetProblemInstance();
            //var externalConstraints = (HashSet_U<CbsConstraint>)problem.parameters[MAPF_CBS.CONSTRAINTS];
            //Debug.WriteLine(externalConstraints.Count.ToString() + " external constraints: ");
            //foreach (CbsConstraint constraint in externalConstraints)
            //{
            //    Debug.WriteLine(constraint);
            //}
            //Debug.WriteLine("Conflict: " + this.GetConflict());
            //Debug.Write("Agent group assignments: ");
            //for (int j = 0; j < this.agentsGroupAssignment.Length; j++)
            //{
            //    Debug.Write(" " + this.agentsGroupAssignment[j]);
            //}
            //Debug.WriteLine("");
            //Debug.Write("Single agent costs: ");
            //for (int j = 0; j < this.allSingleAgentCosts.Length; j++)
            //{
            //    Debug.Write(" " + this.allSingleAgentCosts[j]);
            //}
            //Debug.WriteLine("");
            //Debug.Write("Internal agents that conflict with each agent: ");
            //for (int j = 0; j < this.countsOfInternalAgentsThatConflict.Length; j++)
            //{
            //    Debug.Write(" " + this.countsOfInternalAgentsThatConflict[j]);
            //}
            //Debug.WriteLine("");
            //for (int j = 0; j < this.conflictCountsPerAgent.Length; j++)
            //{
            //    //if (this.conflictCountsPerAgent[j].Count != 0)
            //    {
            //        Debug.Write("Agent " + problem.m_vAgents[j].agentIndex + " conflict counts: ");
            //        foreach (var pair in this.conflictCountsPerAgent[j])
	           //     {
            //            Debug.Write(pair.Key.ToString() + ":" + pair.Value.ToString() + " ");
	           //     }
            //        Debug.WriteLine("");

            //    }
            //}
            //for (int j = 0; j < this.conflictTimesPerAgent.Length; j++)
            //{
            //    //if (this.conflictCountsPerAgent[j].Count != 0)
            //    {
            //        Debug.Write("Agent " + problem.m_vAgents[j].agentIndex + " conflict times: ");
            //        foreach (var pair in this.conflictTimesPerAgent[j])
            //        {
            //            Debug.Write(pair.Key.ToString() + ":[" + String.Join(",", pair.Value) + "], ");
            //        }
            //        Debug.WriteLine("");

            //    }
            //}
           
            //var plan = this.CalculateJointPlan();
            //plan.ToString();
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
        private IEnumerator<CFMCbsConflict> nextConflicts;

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
        /// Assuming the groups conflict, return their conflict.
        /// </summary>
        /// <param name="aConflictingGroupMemberIndex"></param>
        /// <param name="bConflictingGroupMemberIndex"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        private CFMCbsConflict FindConflict(int aConflictingGroupMemberIndex, int bConflictingGroupMemberIndex, int time,
                                         ISet<int>[] groups = null, int time2 = -1)
        {
            // TODO: Reimplement
            //if (time2 == -1)
            //    time2 = time;
            //int specificConflictingAgentA, specificConflictingAgentB;
            //this.FindConflicting(aConflictingGroupMemberIndex, bConflictingGroupMemberIndex, time,
            //                     out specificConflictingAgentA, out specificConflictingAgentB,
            //                     groups);
            //MAM_ProblemInstance problem = this.cbs.GetProblemInstance();
            //int initialTimeStep = problem.m_vAgents[0].lastMove.time; // To account for solving partially solved problems.
            //// This assumes the makespan of all the agents is the same.
            //Move first = allSingleAgentPlans[specificConflictingAgentA].GetLocationAt(time);
            //Move second = allSingleAgentPlans[specificConflictingAgentB].GetLocationAt(time2);
            //return new CbsConflict(specificConflictingAgentA, specificConflictingAgentB, first, second, time + initialTimeStep, time, time2);
            return null;
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

        public CFMCbsConflict GetConflict()
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
                for (int i = 0; i < agentsGroupAssignment.Length; i++)
                {
                    ans += Constants.PRIMES_FOR_HASHING[i % Constants.PRIMES_FOR_HASHING.Length] * agentsGroupAssignment[i];
                }

                HashSet<CFMCbsConstraint> constraints = this.GetConstraints();

                foreach (CFMCbsConstraint constraint in constraints)
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
            CFMCbsNode other = (CFMCbsNode)obj;

            if (this.agentsGroupAssignment.SequenceEqual<ushort>(other.agentsGroupAssignment) == false)
                return false;

            CFMCbsNode current = this;
            HashSet<CFMCbsConstraint> other_constraints = other.GetConstraints();
            HashSet<CFMCbsConstraint> constraints = this.GetConstraints();

            foreach (CFMCbsConstraint constraint in constraints)
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
            this.allSingleAgentCosts = null;
        }

        

        public int CompareTo(IBinaryHeapItem item)
        {
            CFMCbsNode other = (CFMCbsNode)item;


            return (int)(this.mamCost - other.mamCost);
        }


        public int CompareToIgnoreH(CFMCbsNode other, bool ignorePartialExpansion = false)
        {
            // Tie breaking:

            // Prefer larger cost - higher h usually means more work needs to be done
            if (this.totalCost > other.totalCost)
                return -1;
            if (this.totalCost < other.totalCost)
                return 1;

            // Prefer less external conflicts, even over goal nodes, as goal nodes with less external conflicts are better.
            // External conflicts are also taken into account by the low level solver to prefer less conflicts between fewer agents.
            // This only helps when this CBS is used as a low level solver, of course.
            if (this.totalConflictsWithExternalAgents < other.totalConflictsWithExternalAgents)
                return -1;
            if (this.totalConflictsWithExternalAgents > other.totalConflictsWithExternalAgents)
                return 1;

            if (this.totalExternalAgentsThatConflict < other.totalExternalAgentsThatConflict)
                return -1;
            if (this.totalExternalAgentsThatConflict > other.totalExternalAgentsThatConflict)
                return 1;
            
            // Prefer goal nodes. The elaborate form is to keep the comparison consistent. Without it goalA<goalB and also goalB<goalA.
            if (this.GoalTest() == true && other.GoalTest() == false)
                return -1;
            if (other.GoalTest() == true && this.GoalTest() == false)
                return 1;

            if (this.minOpsToSolve < other.minOpsToSolve)
                return -1;
            if (this.minOpsToSolve > other.minOpsToSolve)
                return 1;
            
            return 0;
        }

        /// <summary>
        /// Not used.
        /// </summary>
        /// <returns></returns>
        public CFMCbsConstraint GetLastConstraint()
        {
            return this.constraint;
        }

        public HashSet<CFMCbsConstraint> GetConstraints()
        {
            var constraints = new HashSet<CFMCbsConstraint>();
            CFMCbsNode current = this;
            CFMCbsConstraint currentConstraint = null;

            while (current.depth > 0) // The root has no constraints
            {

                if (current.constraint != null && // Next check not enough if "surprise merges" happen (merges taken from adopted child)
                    current.prev.conflict != null && // Can only happen for temporary lookahead nodes the were created and then later the parent adopted a goal node
                    this.agentsGroupAssignment[current.prev.conflict.agentAIndex] !=
                    this.agentsGroupAssignment[current.prev.conflict.agentBIndex]) // Ignore constraints that deal with conflicts between
                    // agents that were later merged. They're irrelevant
                    // since merging fixes all conflicts between merged agents.
                    // Nodes that only differ in such irrelevant conflicts will have the same single agent paths.
                    // Dereferencing current.prev is safe because current isn't the root.
                    // Also, merging creates a non-root node with a null constraint, and this helps avoid adding the null to the answer.
                    
                    currentConstraint = current.constraint;
                    TimedMove     currentMove       = current.constraint.move;
                    CFMCbsConstraint newConstraint = new CFMCbsConstraint(currentConstraint.agentNum, currentMove.x, currentMove.y, currentMove.direction, currentMove.time);
                    constraints.Add(newConstraint);
                    
                current = current.prev;
            }
            return constraints;
        }

        private bool isLeftNode(CFMCbsNode node)
        {
            if (node.prev == null || node.agentToReplan == node.prev.conflict.agentAIndex)
                return true;
            return false;
        }

        /// <summary>
        /// For printing
        /// </summary>
        /// <returns></returns>
        public List<CFMCbsConstraint> GetConstraintsOrdered()
        {
            var constraints = new List<CFMCbsConstraint>();
            CFMCbsNode current = this;
            while (current.depth > 0) // The root has no constraints
            {
                if (current.constraint != null && // Next check not enough if "surprise merges" happen (merges taken from adopted child)
                    current.prev.conflict != null && // Can only happen for temporary lookahead nodes the were created and then later the parent adopted a goal node
                    this.agentsGroupAssignment[current.prev.conflict.agentAIndex] !=
                    this.agentsGroupAssignment[current.prev.conflict.agentBIndex]) // Ignore constraints that deal with conflicts between
                    // agents that were later merged. They're irrelevant
                    // since merging fixes all conflicts between merged agents.
                    // Nodes that only differ in such irrelevant conflicts will have the same single agent paths.
                    // Dereferencing current.prev is safe because current isn't the root.
                    // Also, merging creates a non-root node with a null constraint, and this helps avoid adding the null to the answer.
                    constraints.Add(current.constraint);
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

       

        /// <summary>
        /// Returns a list of indices of agents in the group
        /// </summary>
        /// <param name="agentIndex"></param>
        /// <returns></returns>
        public ISet<int> GetGroup(int agentIndex)
        {
            int groupNumber = this.agentsGroupAssignment[agentIndex];
            ISet<int> group = new SortedSet<int>();

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
            {
                if (agentsGroupAssignment[i] == groupNumber)
                    group.Add(i);
            }
            return group;
        }

        /// <summary>
        /// Currently unused.
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public int GetGroupCost(int groupNumber)
        {
            int cost = 0;

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
            {
                if (agentsGroupAssignment[i] == groupNumber)
                    cost += this.allSingleAgentCosts[i];
            }
            return cost;
        }

        /// <summary>
        /// A bit cheaper than GetGroup(n).Count. Still O(n).
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public int GetGroupSize(int agentIndex)
        {
            int groupNumber = this.agentsGroupAssignment[agentIndex];
            int count = 0;

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
            {
                if (agentsGroupAssignment[i] == groupNumber)
                    count += 1;
            }
            return count;
        }

        /// <summary>
        /// In O(n)
        /// </summary>
        /// <returns></returns>
        public int[] GetGroupSizes()
        {
            int[] counts = new int[this.agentsGroupAssignment.Length];

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
                counts[this.agentsGroupAssignment[i]]++;

            int[] groupSizes = new int[this.agentsGroupAssignment.Length];

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
                groupSizes[i] = counts[this.agentsGroupAssignment[i]];
            
            return groupSizes;
        }

        /// <summary>
        /// In O(n)
        /// </summary>
        /// <returns></returns>
        public ISet<int>[] GetGroups()
        {
            Dictionary<int, ISet<int>> repsToGroups = new Dictionary<int, ISet<int>>();

            for (int i = 0; i < agentsGroupAssignment.Length; i++)
            {
                int groupRep = this.agentsGroupAssignment[i];
                if (repsToGroups.ContainsKey(groupRep))
                    repsToGroups[groupRep].Add(i);
                else
                {
                    var newGroup = new HashSet<int>();
                    newGroup.Add(i);
                    repsToGroups[groupRep] = newGroup;

                }
            }

            ISet<int>[] res = new HashSet<int>[this.agentsGroupAssignment.Length];
            for (int i = 0; i < res.Length; i++)
			    res[i] = repsToGroups[this.agentsGroupAssignment[i]];

            return res;
        }

      

        public void PrintConflict()
        {
            if (conflict != null)
            {
                Debug.WriteLine("Conflict:");
                Debug.WriteLine("Agents:({0},{1})", conflict.agentAIndex, conflict.agentBIndex);
                Debug.WriteLine("Location:({0},{1})", conflict.agentAmove.x, conflict.agentAmove.y);
                Debug.WriteLine("Time:{0}", conflict.timeStep);
            }
            Debug.WriteLine("");
        }

       

        private bool isGoal = false;

        public bool GoalTest() {
            return isGoal;
        }

       
    }

    /// <summary>
    /// Because the default tuple comparison compares the first element only :(.
    /// </summary>
    public class AgentToCheckForCardinalConflicts : IBinaryHeapItem
    {
        //public bool hasMDD;
        //int conflictingAgentsWithMDD;
        int groupSize;
        int degree;
        int planCost;
        public int index;
 

        public int CompareTo(IBinaryHeapItem item)
        {
            AgentToCheckForCardinalConflicts other = (AgentToCheckForCardinalConflicts)item;


            if (this.groupSize < other.groupSize)
                return -1;
            else if (this.groupSize > other.groupSize)
                return 1;

            if (this.degree < other.degree)
                return -1;
            else if (this.degree > other.degree)
                return 1;

            if (this.planCost < other.planCost)
                return -1;
            else if (this.planCost > other.planCost)
                return 1;

            if (this.index < other.index)
                return -1;
            else if (this.index > other.index)
                return 1;
            else
                return 0;
        }

        int binaryHeapIndex;

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
    }
}
