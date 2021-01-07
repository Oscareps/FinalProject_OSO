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

        protected MAM_ProblemInstance instance;
        public MAPF_OpenList openList;
        /// <summary>
        /// Might as well be a HashSet. We don't need to retrive from it.
        /// </summary>
        public Dictionary<CbsNode, CbsNode> closedList;
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
        protected CbsNode goalNode;
        protected MAM_Plan solution;
        /// <summary>
        /// Nodes with with a higher cost aren't generated
        /// </summary>
        protected int maxCost;

        protected int maxSizeGroup;
        protected int accMaxSizeGroup;
        public bool solved;

        public bool isSolved()
        {
            return this.solved;
        }


        public CFM_CBS()
        {
            this.closedList = new Dictionary<CbsNode, CbsNode>();
            this.openList = new MAPF_OpenList(this);
            this.solved = false;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="runner"></param>
        public virtual void Setup(MAM_ProblemInstance problemInstance, MAM_Run runner)
        {
            this.instance = problemInstance;
            this.runner = runner;
            this.ClearPrivateStatistics();
            this.totalCost = 0;
            this.solutionDepth = -1;

            this.goalNode = null;
            this.solution = null;

            this.maxCost = int.MaxValue;

            CbsNode root = new CbsNode(this); // Problem instance and various strategy data is all passed under 'this'.
            root.Solve(); // Solve the root node - Solve with MMStar, and find conflicts

            if (root.totalCost <= this.maxCost)
            {
                this.openList.Add(root);
                this.highLevelGenerated++;
                this.closedList.Add(root, root);
            }
        }


        public MAM_ProblemInstance GetProblemInstance()
        {
            return this.instance;
        }

        public void Clear()
        {
            this.openList.Clear();
            this.closedList.Clear();
        }

        public virtual string GetName() 
        {        
            return "CFM_CBS";
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


        public virtual void OutputStatistics(TextWriter output)
        {
            Console.WriteLine("Total Expanded Nodes (High-Level): {0}", this.GetHighLevelExpanded());
            Console.WriteLine("Total Generated Nodes (High-Level): {0}", this.GetHighLevelGenerated());
            Console.WriteLine("Closed List Hits (High-Level): {0}", this.closedListHits);
            Console.WriteLine("Nodes Pushed Back (High-Level): {0}", this.nodesPushedBack);

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


        public bool debug = false;

        public bool Solve()
        {

            int initialEstimate = 0;
            if (openList.Count > 0)
                initialEstimate = ((CbsNode)openList.Peek()).totalCost;

            int currentCost = -1;

            while (openList.Count > 0)
            {
                // Check if max time has been exceeded
                if (runner.ElapsedMilliseconds() > Constants.MAX_TIME)
                {
                    this.totalCost = Constants.TIMEOUT_COST;
                    Console.WriteLine("Out of time");
                    this.solutionDepth = ((CbsNode)openList.Peek()).totalCost - initialEstimate; // A minimum estimate
                    this.Clear(); // Total search time exceeded - we're not going to resume this search.
                    //this.CleanGlobals();
                    return false;
                }

                var currentNode = (CbsNode)openList.Remove();


                this.addToGlobalConflictCount(currentNode.GetConflict()); // TODO: Make CBS_GlobalConflicts use nodes that do this automatically after choosing a conflict

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
                bool wasUnexpandedNode = (currentNode.agentAExpansion == CbsNode.ExpansionState.NOT_EXPANDED &&
                                         currentNode.agentBExpansion == CbsNode.ExpansionState.NOT_EXPANDED);
                Expand(currentNode);
                if (wasUnexpandedNode)
                    highLevelExpanded++;
                // Consider moving the following into Expand()
                if (currentNode.agentAExpansion == CbsNode.ExpansionState.EXPANDED &&
                    currentNode.agentBExpansion == CbsNode.ExpansionState.EXPANDED) // Fully expanded
                    currentNode.Clear();
            }

            this.totalCost = Constants.NO_SOLUTION_COST;
            this.Clear(); // unsolvable problem - we're not going to resume it
            //this.CleanGlobals();
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="children"></param>
        /// <param name="adoptBy">If not given, adoption is done by expanded node</param>
        /// <returns>true if adopted - need to rerun this method, ignoring the returned children from this call, bacause adoption was performed</returns>
        protected bool ExpandImpl(CbsNode node, out IList<CbsNode> children, out bool reinsertParent)
        {
            CbsConflict conflict = node.GetConflict();
            children = new List<CbsNode>();

            CbsNode child;
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

        public virtual void Expand(CbsNode node)
        {
            ushort parentCost = node.totalCost;
            IList<CbsNode> children = null; // To quiet the compiler
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
        protected CbsNode ConstraintExpand(CbsNode node, bool doLeftChild, out int closedListHitChildCost)
        {
            CbsConflict conflict = node.GetConflict();
            int conflictingAgentIndex = doLeftChild? conflict.agentAIndex : conflict.agentBIndex;
            CbsNode.ExpansionState expansionsState = doLeftChild ? node.agentAExpansion : node.agentBExpansion;
            CbsNode.ExpansionState otherChildExpansionsState = doLeftChild ? node.agentBExpansion : node.agentAExpansion;
            string agentSide = doLeftChild? "left" : "right";
            closedListHitChildCost = -1;

            if (expansionsState != CbsNode.ExpansionState.EXPANDED)
            // Agent expansion already skipped in the past or not forcing it from its goal - finally generate the child:
            {
                if (debug)
                    Debug.WriteLine("Generating " + agentSide +" child");

                if (doLeftChild)
                    node.agentAExpansion = CbsNode.ExpansionState.EXPANDED;
                else
                    node.agentBExpansion = CbsNode.ExpansionState.EXPANDED;
                
                var newConstraint = new CbsConstraint(conflict, instance, doLeftChild);
                CbsNode child = new CbsNode(node, newConstraint);

                if (closedList.ContainsKey(child) == false)
                {

                    child.Solve();
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


        protected virtual void addToGlobalConflictCount(CbsConflict conflict) { }

        public virtual string GetPlan()
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

        public int GetHighLevelExpanded() { return highLevelExpanded; }
        public int GetHighLevelGenerated() { return highLevelGenerated; }
        public int GetExpanded() { return highLevelExpanded; }
        public int GetGenerated() { return highLevelGenerated; }
        public int GetAccumulatedExpanded() { return accHLExpanded; }
        public int GetAccumulatedGenerated() { return accHLGenerated; }
        public int GetMaxGroupSize() { return this.maxSizeGroup; }
    }
    
}
