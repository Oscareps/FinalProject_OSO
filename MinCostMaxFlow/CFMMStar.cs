using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace CPF_experiment
{
    /// <summary>
    /// Merges agents if they conflict more times than the given threshold in the CT nodes from the root to the current CT nodes only.
    /// </summary>
    public class CFMMStar : MAM_ISolver
    {
        public enum CostFunction { MakeSpan, SOC };
        public CostFunction costFunction;
        protected MAM_ProblemInstance instance;
        public MAM_OpenList openList;
        public HashSet<MMStarConstraint> constraints;
        public HashSet<IBinaryHeapItem> expandedNodes;
        /// <summary>
        /// Might as well be a HashSet. We don't need to retrive from it.
        /// </summary>
        public Dictionary<Move, Dictionary<int, Dictionary<int, MAM_AgentState>>> closedList; // Move -> AgentIndex -> time -> states
        protected int nodesExpanded;
        protected int nodesGenerated;
        protected int closedListHits;
        protected int nodesPushedBack;
        protected List<List<MAM_HeuristicCalculator>> hCalculatorList;

        public int totalCost;
        protected int solutionDepth;
        public MAM_Run runner;
        protected Move goalLocation;
        protected MAM_Plan solution;
        private int bestMakeSpanCost; // The best makespan and soc of the best location
        private int bestSOCCost;
        private Move bestCostLocation;
        private bool meetFlag;
        public bool success;


        /// <summary>
        /// Search is stopped when the millisecond count exceeds the cap
        /// </summary>
        public int milliCap { set; get; }



        public CFMMStar
        (
            CostFunction costFunction = CostFunction.SOC
        )
        {
            this.costFunction = costFunction;
            this.closedList = new Dictionary<Move, Dictionary<int, Dictionary<int, MAM_AgentState>>>();
            this.expandedNodes = new HashSet<IBinaryHeapItem>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="minDepth"></param>
        /// <param name="runner"></param>
        /// <param name="minCost">Not taken into account</param>
        public virtual void Setup
        (
            MAM_ProblemInstance problemInstance,
            int minDepth,
            MAM_Run runner,
            int minCost = -1,
            HashSet<MMStarConstraint> constraints = null
        )
        {
            this.instance = problemInstance;
            this.runner = runner;
            this.ClearPrivateStatistics();
            this.totalCost = 0;
            this.solutionDepth = -1;
            this.milliCap = int.MaxValue;
            this.goalLocation = null;
            this.solution = null;
            this.bestMakeSpanCost = int.MaxValue;
            this.bestSOCCost = int.MaxValue;
            this.bestCostLocation = null;
            this.meetFlag = false;
            this.success = false;
            this.openList = new MAM_OpenList(this);
            if (constraints != null)
                this.constraints = constraints;
            else
                this.constraints = new HashSet<MMStarConstraint>();

            // caculate lowest centrality 
            MAM_AgentState agent = getLowestCentralityAgent();
            openList.Add(agent);
          
        }

        private MAM_AgentState getLowestCentralityAgent()
        {
            int minCentralityValue = int.MaxValue;
            int minAgentIndex = 0;
            for (int currentAgentIndex = 0; currentAgentIndex < this.instance.m_vAgents.Length; currentAgentIndex++)
            {
                int currentCentralityValue = 0;
                MAM_AgentState currentAgent = this.instance.m_vAgents[currentAgentIndex];
                for (int comparedAgentIndex = 0; comparedAgentIndex < this.instance.m_vAgents.Length; comparedAgentIndex++)
                {
                    MAM_AgentState comparedAgent = this.instance.m_vAgents[comparedAgentIndex];
                    if (currentAgentIndex == comparedAgentIndex) continue;
                    currentCentralityValue += calculateTwoAgentsManhattanDistance(currentAgent, comparedAgent);
                }
                if(currentCentralityValue < minCentralityValue)
                {
                    minCentralityValue = currentCentralityValue;
                    minAgentIndex = currentAgentIndex;
                }
            }
            return this.instance.m_vAgents[minAgentIndex];
        }

        private int calculateTwoAgentsManhattanDistance(MAM_AgentState currentAgent, MAM_AgentState comparedAgent)
        {
            return Math.Abs(currentAgent.lastMove.x - comparedAgent.lastMove.x) +
                    Math.Abs(currentAgent.lastMove.y - comparedAgent.lastMove.y);
        }

        public void AddConstraints
        (
            HashSet<MMStarConstraint> constraints
        )
        {
        }

        private void CalculateH
        (
            MAM_AgentState state,
            MAM_AgentState prev
        )
        {
            state.h = 0;
            if (hCalculatorList[0][0] is ZeroHCalculator)
                return;
            else if (costFunction == CostFunction.SOC)
            {
                state.h = hCalculatorList[0][0].h(state, prev);
            }
        }

        private void CalculateF
        (
            MAM_AgentState state
        )
        {
            if (costFunction == CostFunction.SOC)
            {
                state.f = state.g + state.h;
                if (state.prev != null)
                    state.f = Math.Max(state.f, state.prev.f);
            }
        }

        public virtual void Setup
        (
            MAM_ProblemInstance problemInstance,
            MAM_Run runner
        )
        {
            this.Setup(problemInstance, 0, runner);
        }

        public void SetHeuristic
        (
            MAM_HeuristicCalculator hCalculator     // this heuristic contains all agents
        )
        {
            this.hCalculatorList = new List<List<MAM_HeuristicCalculator>>();
            this.hCalculatorList.Add(new List<MAM_HeuristicCalculator>());
            this.hCalculatorList[0].Add(hCalculator);
        }

        public MAM_HeuristicCalculator GetHeuristicCalculator()
        {
            return hCalculatorList[0][0];
        }

        public Tuple<double, int> GetHeuristicCalculatorInitialH()
        {
            double bestH = 0;
            int bestNumberOfAgents = 0;
            for (int index = 0; index < hCalculatorList.Count(); index++)
            {
                List<MAM_HeuristicCalculator> currenctList = hCalculatorList[index];
                foreach (MAM_HeuristicCalculator currectHeuristic in currenctList)
                {
                    if (bestH < currectHeuristic.GetInitialH())
                    {
                        bestH = currectHeuristic.GetInitialH();
                        bestNumberOfAgents = currectHeuristic.GetNumberOfAgents();
                    }
                }
            }
            return new Tuple<double, int>(bestH, bestNumberOfAgents);
        }


        public MAM_ProblemInstance GetProblemInstance()
        {
            return this.instance;
        }

        public void Clear()
        {
            this.openList.Clear();
            this.closedList.Clear();
            this.expandedNodes.Clear();
        }

        public virtual string GetName()
        {
            return "CFMM_Star";
        }

        public override string ToString()
        {
            return GetName();
        }

        public int GetSolutionMakeSpanCost()
        {
            return this.bestMakeSpanCost;
        }

        public int GetSolutionSOCCost()
        {
            return this.bestSOCCost;
        }

        protected void ClearPrivateStatistics()
        {
            this.nodesExpanded = 0;
            this.nodesGenerated = 0;
            this.closedListHits = 0;
            this.nodesPushedBack = 0;
        }

        public virtual void OutputStatisticsHeader
        (
            TextWriter output
        )
        {
            output.Write(this.ToString() + " Expanded");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Generated");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Closed List Hits (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
            output.Write(this.ToString() + " Nodes Pushed Back (HL)");
            output.Write(MAM_Run.RESULTS_DELIMITER);
        }

        public virtual void OutputStatistics
        (
            TextWriter output
        )
        {
            Console.WriteLine("Total Expanded Nodes (High-Level): {0}", this.nodesExpanded);
            Console.WriteLine("Total Generated Nodes (High-Level): {0}", this.nodesGenerated);
            Console.WriteLine("Closed List Hits (High-Level): {0}", this.closedListHits);
            Console.WriteLine("Nodes Pushed Back (High-Level): {0}", this.nodesPushedBack);

            output.Write(this.nodesExpanded + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.nodesGenerated + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.closedListHits + MAM_Run.RESULTS_DELIMITER);
            output.Write(this.nodesPushedBack + MAM_Run.RESULTS_DELIMITER);
        }

        public virtual void ClearStatistics()
        {
            this.ClearPrivateStatistics();
        }


        public bool debug = false;
        private bool equivalenceWasOn;


        public bool Solve()
        {
            int currentCost = -1;
            while (!isEmpty())
            {
                // Check if max time has been exceeded
                if (runner.ElapsedMilliseconds() > Constants.MAX_TIME)
                {
                    this.totalCost = Constants.TIMEOUT_COST;
                    Console.WriteLine("Out of time");
                    this.Clear(); // Total search time exceeded - we're not going to resume this search.
                    return false;
                }

                var currentNode = removeFromOpen();

                if (currentNode.f >= getBestCost() || isEmpty()) // Goal test
                {
                    success = true;
                    Console.WriteLine("Meeting point found! In: " + bestCostLocation);
                    
                    // TODO: ToString in OGAM solution
                    GetPlan().ToString();  // Print plan

                    this.Clear(); // Goal found - we're not going to resume this search
                    return true;
                }
                
                // Expand
                bool expanded = Expand(currentNode);
                nodesExpanded++;

            }
            this.totalCost = Constants.NO_SOLUTION_COST;
            this.Clear(); // unsolvable problem - we're not going to resume it
            return false;
        }

        private bool isEmpty()
        {
            if (openList.Count > 0)
                return false;
            return true;
        }

        private MAM_AgentState removeFromOpen()
        {
            return (MAM_AgentState)(openList.Remove());
        }

        public bool IsSolved()
        {
            return success;
        }

        private bool closed
        (
            MAM_AgentState child
        )
        {
            TimedMove timedChildMove = child.lastMove;
            Move childMove = new Move(timedChildMove.x, timedChildMove.y, Move.Direction.NO_DIRECTION);
            if (!closedList.ContainsKey(childMove)) // Child is not in the closed list
            {
                closedList.Add(childMove, new Dictionary<int, Dictionary<int, MAM_AgentState>>());
                Dictionary<int, MAM_AgentState> moveAgentDiffTimes = new Dictionary<int, MAM_AgentState>();
                moveAgentDiffTimes.Add(timedChildMove.time, child);
                closedList[childMove].Add(child.agentIndex, moveAgentDiffTimes);
                return false;
            }
            else if (!closedList[childMove].Keys.Contains(child.agentIndex)) // Child is not in the closed list for this agent
            {
                Dictionary<int, MAM_AgentState> moveAgentDiffTimes = new Dictionary<int, MAM_AgentState>();
                moveAgentDiffTimes.Add(timedChildMove.time, child);
                closedList[childMove].Add(child.agentIndex, moveAgentDiffTimes);
                return false;
            }
            else if (!closedList[childMove][child.agentIndex].Keys.Contains(timedChildMove.time)) // Child is in the closed list for this agent, but not for this time
            {
                Dictionary<int, MAM_AgentState> moveAgentDiffTimes = closedList[childMove][child.agentIndex];
                moveAgentDiffTimes.Add(timedChildMove.time, child);
                //closedList[childMove].Add(child.agentIndex, moveAgentDiffTimes);
                return false;
            }
            return true;
        }

        private int getBestCost()
        {
            if (costFunction == CostFunction.SOC)
                return bestSOCCost;
            return 0;
        }

        private void UpdateBestCost
        (
            MAM_AgentState currentNode
        )
        {
            Move move = new Move(currentNode.lastMove);
            long nodeCost = OGAM_Run.solve(this.GetProblemInstance(), move);
            if (nodeCost < this.getBestCost())
                this.bestSOCCost = (int)nodeCost;
        }

        private bool Expand
        (
            MAM_AgentState node
        )
        {
            UpdateBestCost(node);

            if (expandedNodes.Contains(node))
            {
                nodesExpanded--;
                return false;
            }
            expandedNodes.Add(node);
            foreach (MAM_AgentState child in node.GetChildrenStates())
            {
                Generate(node, child);
            }
            return true;
        }

        private void Generate
        (
            MAM_AgentState node,
            MAM_AgentState child
        )
        {
            if (expandedNodes.Contains(child))
            {
                return;
            }
            Move childMove = child.lastMove;
            if (!instance.IsValid(childMove))                               // Not a valid location
                return;
            if (!closed(child))
            {
                if (child.f >= getBestCost())
                    return;
                child.numOfAgentsInBestHeuristic = instance.m_vAgents.Length;
                if (openList.Contains(child))
                {
                    child.h = ((MAM_AgentState)openList.Get(child)).h;
                    child.heuristics = ((MAM_AgentState)openList.Get(child)).heuristics;
                }
                else
                {
                    CalculateH(child, node);
                }
                CalculateF(child);
                openList.Add(child);

                nodesGenerated++;
            }
        }


        public virtual MAM_Plan GetPlan()
        {
            // TODO: Get CFMAM plan
            if (this.solution == null)
            {
                if (bestCostLocation == null)
                    return null;
                Dictionary<int, Dictionary<int, MAM_AgentState>> solutionAgentsStatesDictionaries = closedList[bestCostLocation];
                Dictionary<int, MAM_AgentState> solutionAgentsStates = new Dictionary<int, MAM_AgentState>();
                foreach (int agent in solutionAgentsStatesDictionaries.Keys)
                {
                    int bestTimeForGivenAgent = solutionAgentsStatesDictionaries[agent].Keys.Min();
                    MAM_AgentState bestStateForGivenAgent = solutionAgentsStatesDictionaries[agent][bestTimeForGivenAgent];
                    solutionAgentsStates.Add(agent, bestStateForGivenAgent);
                }
                this.solution = new MAM_Plan(solutionAgentsStates.Values.ToList());
            }
            return this.solution;
        }

        public int GetSolutionDepth()
        {
            // Not necessary
            return this.solutionDepth;
        }

        public long GetMemoryUsed()
        {
            return Process.GetCurrentProcess().VirtualMemorySize64;
        }


        public int GetExpanded()
        {
            return nodesExpanded;
        }

        public int GetGenerated()
        {
            return nodesGenerated;
        }

        public MAM_Run.CostFunction GetCostFunction()
        {
            return (MAM_Run.CostFunction)costFunction;
        }


    }

}
