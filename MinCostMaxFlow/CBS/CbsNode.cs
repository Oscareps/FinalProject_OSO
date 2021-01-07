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

        private int binaryHeapIndex;
        public CbsConflict conflict;
        public CbsConstraint constraint;

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

        private bool isGoal = false;

        protected CFM_CBS cbs;

        /// <summary>
        /// Root of CBS tree
        /// </summary>
        /// <param name="cbs"></param>
        public CbsNode(CFM_CBS cbs)
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
        public CbsNode(CbsNode father, CbsConstraint newConstraint)
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
        /// Solves the entire node
        /// </summary>
        /// <returns></returns>
        public void Solve()
        {
            this.totalCost = 0;
            MAM_ProblemInstance problem = this.cbs.GetProblemInstance();
            HashSet<CbsConstraint> newConstraints = this.GetConstraints();

            var constraints = new HashSet_U<CbsConstraint>();
            // Constraints initiated with the problem instance
            if (problem.parameters.ContainsKey(CFM_CBS.CONSTRAINTS))
                constraints = (HashSet_U<CbsConstraint>)problem.parameters[CFM_CBS.CONSTRAINTS];

            constraints.Join(newConstraints);

            // Solve using MMMStar

            HashSet<MMStarConstraint> mConstraints = importCBSConstraintsToMMStarConstraints(constraints);

            this.cbs.runner.SolveGivenProblem(problem, mConstraints);
            this.mamPlan = this.cbs.runner.plan;
            this.mamCost = this.cbs.runner.solutionCost;

            // Gather conflicts

            this.nodeConflicts = gatherConflicts();

            this.isGoal = this.nodeConflicts.Count == 0;
        }

        /// <summary>
        /// Find conflicts between agents in MMStar solution
        /// </summary>
        /// <returns>List of conflicts of the node</returns>
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

        /// <summary>
        /// Import CBS constraints to a MMStar constraints
        /// </summary>
        /// <param name="constraints">HashSet of CBS constraints</param>
        /// <returns>HashSet of MMStar constraints</returns>
        private HashSet<MMStarConstraint> importCBSConstraintsToMMStarConstraints(HashSet_U<CbsConstraint> constraints)
        {
            HashSet<MMStarConstraint> mConstraints = new HashSet<MMStarConstraint>();
            foreach (CbsConstraint constraint in constraints)
                mConstraints.Add(new MMStarConstraint(constraint));
            return mConstraints;
        }

        /// <summary>
        /// Chooses an internal conflict to work on.
        /// </summary>
        public void ChooseConflict()
        {
            if(this.nodeConflicts.Count != 0)
                this.conflict = this.nodeConflicts[0];
            else
                this.conflict = null;
        }

        /// <summary>
        /// Conflict getter
        /// </summary>
        /// <returns>The selected conflict</returns>
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

        /// <summary>
        /// Goes up on the conflicts tree and collects all of the conflicts of the node
        /// </summary>
        /// <returns>List of constraints of the node</returns>
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

        public bool GoalTest() {
            return isGoal;
        }

    }
}
