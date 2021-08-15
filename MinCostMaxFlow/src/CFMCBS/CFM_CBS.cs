using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;


namespace CPF_experiment
{
    public class CFM_CBS : ICbsSolver
    {
        /// <summary>
        /// The key of the constraints list used for each CBS node
        /// </summary>
        public static readonly string CONSTRAINTS = "constraints";
        /// <summary>
        /// The key of the must constraints list used for each CBS node
        /// </summary>        /// <summary>
        /// The key of the internal CAT for CBS, used to favor A* nodes that have fewer conflicts with other routes during tie-breaking.
        /// Also used to indicate that CBS is running.
        /// </summary>
        public static readonly string CAT = "CBS CAT";

        protected ProblemInstance instance;
        public CFMCBS_OpenList openList;
        /// <summary>
        /// Might as well be a HashSet. We don't need to retrive from it.
        /// </summary>
        public Dictionary<CFMCbsNode, CFMCbsNode> closedList;
        protected int highLevelExpanded;
        protected int highLevelGenerated;
        protected int closedListHits;
        protected int pruningSuccesses;
        protected int pruningFailures;
        protected int nodesExpandedWithGoalCost;
        protected int nodesPushedBack;
        protected int accHLExpanded;
        protected int accHLGenerated;
        protected int accClosedListHits;
        protected int accPartialExpansions;
        protected int accBypasses;
        protected int accPruningSuccesses;
        protected int accPruningFailures;
        protected int accNodesExpandedWithGoalCost;
        protected int accNodesPushedBack;

        public int totalCost;
        protected int solutionDepth;
        public MAM_Run runner;
        protected CFMCbsNode goalNode;
        protected MAM_Plan solution;
        /// <summary>
        /// Nodes with with a higher cost aren't generated
        /// </summary>
        protected int maxCost;
        /// <summary>
        /// Search is stopped when the minimum cost passes the target
        /// </summary>
        public int targetCost {set; get;}
        /// <summary>
        /// Search is stopped when the low level generated nodes count exceeds the cap
        /// </summary>
        public int lowLevelGeneratedCap { set; get; }
        /// <summary>
        /// Search is stopped when the millisecond count exceeds the cap
        /// </summary>
        public int milliCap { set; get; }

        /// <summary>
        /// TODO: Shouldn't this be called minTimeStep?
        /// </summary>
        protected int minDepth;
        protected int maxSizeGroup;
        protected int accMaxSizeGroup;
        /// <summary>
        /// Used to know when to clear problem parameters.
        /// </summary>
        public bool topMost;

        public bool solved;

        /// <summary>
        /// Indicates the starting time in ms for timing the different algorithms.
        /// </summary>
        private double startTime;

        public double elapsedTime;

        public bool isSolved()
        {
            return this.solved;
        }


        public CFM_CBS()
        {
            this.closedList = new Dictionary<CFMCbsNode, CFMCbsNode>();
            this.openList = new CFMCBS_OpenList(this);
            this.solved = false;
            this.watch = Stopwatch.StartNew();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="minDepth"></param>
        /// <param name="runner"></param>
        /// <param name="minCost">Not taken into account</param>
        public virtual void Setup(ProblemInstance problemInstance, int minDepth, MAM_Run runner, int minCost = -1)
        {
            this.instance = problemInstance;
            this.runner = runner;
            this.ClearPrivateStatistics();
            this.totalCost = 0;
            this.solutionDepth = -1;
            this.targetCost = int.MaxValue;
            this.lowLevelGeneratedCap = int.MaxValue;
            this.milliCap = int.MaxValue;
            this.goalNode = null;
            this.solution = null;

            this.maxCost = int.MaxValue;
            //this.topMost = this.SetGlobals();

            this.minDepth = minDepth;
            CFMCbsNode root = new CFMCbsNode(instance.m_vAgents.Length, this); // Problem instance and various strategy data is all passed under 'this'.
            // Solve the root node - Solve with MMStar, and find conflicts
            bool solved = root.Solve();
            
            if (solved && root.totalCost <= this.maxCost)
            {
                this.openList.Add(root);
                this.highLevelGenerated++;
                this.closedList.Add(root, root);
            }
        }

        public virtual void Setup(ProblemInstance problemInstance, MAM_Run runner)
        {
            this.Setup(problemInstance, 0, runner);
        }


        public Dictionary<int, int> GetExternalConflictCounts()
        {
            throw new NotImplementedException(); // For now. Also need to take care of generalised goal nodes!
        }

        public Dictionary<int, List<int>> GetConflictTimes()
        {
            throw new NotImplementedException(); // For now. Also need to take care of generalised goal nodes!
        }



        public ProblemInstance GetProblemInstance()
        {
            return this.instance;
        }

        public void Clear()
        {
            this.openList.Clear();
            this.closedList.Clear();
            this.solved = false;
            //this.solver.Clear();
            // Statistics are reset on Setup.
        }

        public virtual string GetName() 
        {        
            return "CBSMMStar";
        }

        public override string ToString()
        {
            return GetName();
        }

        public int GetSolutionCost() { return this.totalCost; }

        protected void ClearPrivateStatistics()
        {
            this.highLevelExpanded = 0;
            this.highLevelGenerated = 0;
            this.closedListHits = 0;
            this.pruningSuccesses = 0;
            this.pruningFailures = 0;
            this.nodesExpandedWithGoalCost = 0;
            this.nodesPushedBack = 0;
            this.maxSizeGroup = 1;
        }

        public virtual void OutputStatisticsHeader(TextWriter output)
        {
            output.Write(this.ToString() + " Expanded (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Generated (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Closed List Hits (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Pruning Successes (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Pruning Failures (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Nodes Expanded With Goal Cost (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Nodes Pushed Back (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Max Group Size (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);

            this.openList.OutputStatisticsHeader(output);
        }

        public virtual void OutputStatistics(TextWriter output)
        {
            Console.WriteLine("Total Expanded Nodes (High-Level): {0}", this.GetHighLevelExpanded());
            Console.WriteLine("Total Generated Nodes (High-Level): {0}", this.GetHighLevelGenerated());
            Console.WriteLine("Closed List Hits (High-Level): {0}", this.closedListHits);
            Console.WriteLine("Pruning successes (High-Level): {0}", this.pruningSuccesses);
            Console.WriteLine("Pruning failures (High-Level): {0}", this.pruningFailures);
            Console.WriteLine("Nodes expanded with goal cost (High-Level): {0}", this.nodesExpandedWithGoalCost);
            Console.WriteLine("Nodes Pushed Back (High-Level): {0}", this.nodesPushedBack);
            Console.WriteLine("Max Group Size (High-Level): {0}", this.maxSizeGroup);

            output.Write(this.highLevelExpanded + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.highLevelGenerated + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.closedListHits + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.pruningSuccesses + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.pruningFailures + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.nodesExpandedWithGoalCost + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.nodesPushedBack + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.maxSizeGroup + MAM_Run.RESULTS_DELIMITER);

            this.openList.OutputStatistics(output);
        }

        public virtual void ClearStatistics()
        {
            this.ClearPrivateStatistics();
            this.openList.ClearStatistics();
        }

        public virtual void ClearAccumulatedStatistics()
        {
            this.accHLExpanded = 0;
            this.accHLGenerated = 0;
            this.accClosedListHits = 0;
            this.accPruningSuccesses = 0;
            this.accPruningFailures = 0;
            this.accNodesExpandedWithGoalCost = 0;
            this.accNodesPushedBack = 0;
            this.accMaxSizeGroup = 1;

            this.openList.ClearAccumulatedStatistics();
        }

        public virtual void AccumulateStatistics()
        {
            this.accHLExpanded += this.highLevelExpanded;
            this.accHLGenerated += this.highLevelGenerated;
            this.accClosedListHits += this.closedListHits;
            this.accPruningSuccesses += this.pruningSuccesses;
            this.accPruningFailures += this.pruningFailures;
            this.accNodesExpandedWithGoalCost += this.nodesExpandedWithGoalCost;
            this.accNodesPushedBack += this.nodesPushedBack;
            this.accMaxSizeGroup = Math.Max(this.accMaxSizeGroup, this.maxSizeGroup);

            // this.solver statistics are accumulated every time it's used.

            this.openList.AccumulateStatistics();
        }

        public virtual void OutputAccumulatedStatistics(TextWriter output)
        {
            Console.WriteLine("{0} Accumulated Expanded Nodes (High-Level): {1}", this, this.accHLExpanded);
            Console.WriteLine("{0} Accumulated Generated Nodes (High-Level): {1}", this, this.accHLGenerated);
            Console.WriteLine("{0} Accumulated Closed List Hits (High-Level): {1}", this, this.accClosedListHits);
            Console.WriteLine("{0} Accumulated Pruning Successes (High-Level): {1}", this, this.accPruningSuccesses);
            Console.WriteLine("{0} Accumulated Pruning Failures (High-Level): {1}", this, this.accPruningFailures);
            Console.WriteLine("{0} Accumulated Nodes Expanded With Goal Cost (High-Level): {1}", this, this.accNodesExpandedWithGoalCost);
            Console.WriteLine("{0} Accumulated Nodes Pushed Back (High-Level): {1}", this.accNodesPushedBack);
            Console.WriteLine("{0} Max Group Size (High-Level): {1}", this, this.accMaxSizeGroup);

            output.Write(this.accHLExpanded + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accHLGenerated + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accClosedListHits + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accPruningSuccesses + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accPruningFailures + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accNodesExpandedWithGoalCost + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accNodesPushedBack + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.accMaxSizeGroup + MAM_Run.RESULTS_DELIMITER);

            this.openList.OutputAccumulatedStatistics(output);
        }

        public bool debug = false;
        private bool equivalenceWasOn;

        public bool Solve()
        {
            //this.SetGlobals(); // Again, because we might be resuming a search that was stopped.

            int initialEstimate = 0;
            if (openList.Count > 0)
                initialEstimate = ((CFMCbsNode)openList.Peek()).totalCost;

            int currentCost = -1;
            Console.WriteLine("maxTime: "+Constants.MAX_TIME);
            this.startTime = this.ElapsedMillisecondsTotal();

            while (openList.Count > 0)
            {
                //Console.WriteLine(this.ElapsedMilliseconds() / 10);
                //Console.WriteLine(openList.Count);
                //Console.WriteLine(closedList.Count);

                // Check if max time has been exceeded
                if (this.ElapsedMilliseconds() > Constants.MAX_TIME)
                {
                    this.totalCost = Constants.TIMEOUT_COST;
                    Console.WriteLine("Out of time");
                    this.solutionDepth = ((CFMCbsNode)openList.Peek()).totalCost - initialEstimate; // A minimum estimate
                    this.Clear(); // Total search time exceeded - we're not going to resume this search.
                    //this.CleanGlobals();
                    return false;
                }

                var currentNode = (CFMCbsNode)openList.Remove();


                this.addToGlobalConflictCount(currentNode.GetConflict()); // TODO: Make CBS_GlobalConflicts use nodes that do this automatically after choosing a conflict

                if (debug)
                    currentNode.Print();

                if (currentNode.totalCost > currentCost) // Needs to be here because the goal may have a cost unseen before
                {
                    currentCost = currentNode.totalCost;
                    this.nodesExpandedWithGoalCost = 0;
                }
                else if (currentNode.totalCost == currentCost) // check needed because macbs node cost isn't exactly monotonous
                {
                    this.nodesExpandedWithGoalCost++;
                }

                // Check if node is the goal
                if (currentNode.GoalTest())
                {
                    //Debug.Assert(currentNode.totalCost >= maxExpandedNodeCostPlusH, "CBS goal node found with lower cost than the max cost node ever expanded: " + currentNode.totalCost + " < " + maxExpandedNodeCostPlusH);
                    // This is subtle, but MA-CBS may expand nodes in a non non-decreasing order:
                    // If a node with a non-optimal constraint is expanded and we decide to merge the agents,
                    // the resulting node can have a lower cost than before, since we ignore the non-optimal constraint
                    // because the conflict it addresses is between merged nodes.
                    // The resulting lower-cost node will have other constraints, that will raise the cost of its children back to at least its original cost,
                    // since the node with the non-optimal constraint was only expanded because its competitors that had an optimal
                    // constraint to deal with the same conflict apparently found the other conflict that I promise will be found,
                    // and so their cost was not smaller than this sub-optimal node.
                    // To make MA-CBS costs non-decreasing, we can choose not to ignore constraints that deal with conflicts between merged nodes.
                    // That way, the sub-optimal node will find a sub-optimal merged solution and get a high cost that will push it deep into the open list.
                    // But the cost would be to create a possibly sub-optimal merged solution where an optimal solution could be found instead, and faster,
                    // since constraints make the low-level heuristic perform worse.
                    // For an example for this subtle case happening, see problem instance 63 of the random grid with 4 agents,
                    // 55 grid cells and 9 obstacles.

                    if (debug)
                        Debug.WriteLine("-----------------");
                    this.totalCost = (int)currentNode.mamCost;
                    this.solution = currentNode.CalculateJointPlan();
                    this.solutionDepth = this.totalCost - initialEstimate;
                    this.goalNode = currentNode; // Saves the single agent plans and costs
                    // The joint plan is calculated on demand.
                    this.Clear(); // Goal found - we're not going to resume this search
                    //this.CleanGlobals();
                    this.solved = true;
                    return true;
                }

                currentNode.ChooseConflict();

                // Expand
                bool wasUnexpandedNode = (currentNode.agentAExpansion == CFMCbsNode.ExpansionState.NOT_EXPANDED &&
                                         currentNode.agentBExpansion == CFMCbsNode.ExpansionState.NOT_EXPANDED);
                Expand(currentNode);
                if (wasUnexpandedNode)
                    highLevelExpanded++;
                // Consider moving the following into Expand()
                if (currentNode.agentAExpansion == CFMCbsNode.ExpansionState.EXPANDED &&
                    currentNode.agentBExpansion == CFMCbsNode.ExpansionState.EXPANDED) // Fully expanded
                    currentNode.Clear();
            }

            this.totalCost = Constants.NO_SOLUTION_COST;
            this.Clear(); // unsolvable problem - we're not going to resume it
            //this.CleanGlobals();
            return false;
        }

        private void printSolution()
        {
            Console.WriteLine("Finished!");
            List<List<Move>> pathes = this.goalNode.mamPlan.listOfLocations;
            Move goalState = pathes[0].Last();
            Console.WriteLine("Meeting Point: (" + goalState.x + "," + goalState.y + ")");
            Console.WriteLine("Cost: " + goalNode.mamCost);
            Console.WriteLine("Paths to meeting point: ");
            int index = 0;
            foreach(List<Move> agentPath in pathes)
            {
                Console.Write("s" + index + ": ");
                foreach(Move move in agentPath)
                {
                    Console.Write("(" + move.x + "," + move.y + ")");
                    if (move.x != goalState.x || move.y != goalState.y)
                        Console.Write("->");
                }
                index++;
                Console.WriteLine();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="children"></param>
        /// <param name="adoptBy">If not given, adoption is done by expanded node</param>
        /// <returns>true if adopted - need to rerun this method, ignoring the returned children from this call, bacause adoption was performed</returns>
        protected bool ExpandImpl(CFMCbsNode node, out IList<CFMCbsNode> children, out bool reinsertParent)
        {
            CFMCbsConflict conflict = node.GetConflict();
            children = new List<CFMCbsNode>();

            CFMCbsNode child;
            reinsertParent = false;
            int closedListHitChildCost;
            bool leftSameCost = false; // To quiet the compiler
            bool rightSameCost = false;
          

            // Generate left child:
            child = ConstraintExpand(node, true, out closedListHitChildCost);
            if (child != null)
            {
                if (child == node) // Expansion deferred
                    reinsertParent = true;
                else // New child
                {
                    children.Add(child);
                    leftSameCost = child.totalCost == node.totalCost;
                }
            }
            else  // A timeout occured, or the child was already in the closed list.
            {
                if (closedListHitChildCost != -1)
                    leftSameCost = closedListHitChildCost == node.totalCost;
            }

            if (runner.ElapsedMilliseconds() > Constants.MAX_TIME)
                return false;
            
            // Generate right child:
            child = ConstraintExpand(node, false, out closedListHitChildCost);
            if (child != null)
            {
                if (child == node) // Expansion deferred
                    reinsertParent = true;
                else // New child
                {
                    children.Add(child);
                    rightSameCost = child.totalCost == node.totalCost;
                }
            }
            else  // A timeout occured, or the child was already in the closed list.
            {
                if (closedListHitChildCost != -1)
                    rightSameCost = closedListHitChildCost == node.totalCost;
            }
            

            return false;
        }

        public virtual void Expand(CFMCbsNode node)
        {
            ushort parentCost = node.totalCost;
            ushort parentH = node.h;
            IList<CFMCbsNode> children = null; // To quiet the compiler
            bool reinsertParent = false; // To quiet the compiler

 
            this.ExpandImpl(node, out children, out reinsertParent);

            foreach (var child in children)
            {

                closedList.Add(child, child);
                this.highLevelGenerated++;
                openList.Add(child);

            }
            
            
        }

        
        /// <summary>
        /// Create Constraints from conflict
        /// </summary>
        /// <param name="node"></param>
        /// <param name="doLeftChild"></param>
        /// <param name="closedListHitChildCost"></param>
        /// <returns></returns>
        protected CFMCbsNode ConstraintExpand(CFMCbsNode node, bool doLeftChild, out int closedListHitChildCost)
        {
            CFMCbsConflict conflict = node.GetConflict();
            int conflictingAgentIndex = doLeftChild? conflict.agentAIndex : conflict.agentBIndex;
            CFMCbsNode.ExpansionState expansionsState = doLeftChild ? node.agentAExpansion : node.agentBExpansion;
            CFMCbsNode.ExpansionState otherChildExpansionsState = doLeftChild ? node.agentBExpansion : node.agentAExpansion;
            string agentSide = doLeftChild? "left" : "right";
            int groupSize = node.GetGroupSize(conflictingAgentIndex);
            closedListHitChildCost = -1;

            if (expansionsState != CFMCbsNode.ExpansionState.EXPANDED)
            // Agent expansion already skipped in the past or not forcing it from its goal - finally generate the child:
            {
                if (debug)
                    Debug.WriteLine("Generating " + agentSide +" child");

                if (doLeftChild)
                    node.agentAExpansion = CFMCbsNode.ExpansionState.EXPANDED;
                else
                    node.agentBExpansion = CFMCbsNode.ExpansionState.EXPANDED;
                
                var newConstraint = new CFMCbsConstraint(conflict, instance, doLeftChild);
                CFMCbsNode child = new CFMCbsNode(node, newConstraint, conflictingAgentIndex);

                if (closedList.ContainsKey(child) == false)
                {

                    bool success = child.Solve();

                    if (success == false)
                        return null; // A timeout probably occured

                    return child;
                }
                else
                {
                    this.closedListHits++;
                    closedListHitChildCost = this.closedList[child].totalCost;
                    if (debug)
                        Debug.WriteLine("Child already in closed list!");
                }
            }
            else
            {
                if (debug)
                    Debug.WriteLine("Child already generated before");
            }

            return null;
        }


        protected virtual void addToGlobalConflictCount(CFMCbsConflict conflict) { }

        public virtual String GetPlan()
        {
            String res = "";
            List<List<Move>> pathes = this.goalNode.mamPlan.listOfLocations;
            Move goalState = pathes[0].Last();
            res += "Meeting Point: (" + goalState.x + "," + goalState.y + ")" + "\nCost: " + goalNode.mamCost + "\n\n";
            int index = 0;
            foreach (List<Move> agentPath in pathes)
            {
                res += "s" + index + ": ";
                foreach (Move move in agentPath)
                {
                    res += "(" + move.x + "," + move.y + ")";
                    if (move.x != goalState.x || move.y != goalState.y)
                        res += "->";
                }
                index++;
                res += "\n";
            }
            return res;
        }

        public int GetSolutionDepth() { return this.solutionDepth; }
        
        public long GetMemoryUsed() { return Process.GetCurrentProcess().VirtualMemorySize64; }

        public virtual int[] GetSingleCosts()
        {
            return goalNode.allSingleAgentCosts;
        }

        public int GetHighLevelExpanded() { return highLevelExpanded; }
        public int GetHighLevelGenerated() { return highLevelGenerated; }
        public int GetExpanded() { return highLevelExpanded; }
        public int GetGenerated() { return highLevelGenerated; }
        public int GetAccumulatedExpanded() { return accHLExpanded; }
        public int GetAccumulatedGenerated() { return accHLGenerated; }
        public int GetMaxGroupSize() { return this.maxSizeGroup; }

        private Stopwatch watch;
        private double ElapsedMillisecondsTotal()
        {
            return this.watch.Elapsed.TotalMilliseconds;
        }

        public double ElapsedMilliseconds()
        {
            return ElapsedMillisecondsTotal() - this.startTime;
        }
    }
    
}
