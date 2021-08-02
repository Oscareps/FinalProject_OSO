using Google.OrTools.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CPF_experiment
{
    class OGAM_Run
    {

        CFMAM_MCMF_Reducer reducer;
        Stopwatch timer;
        MinCostFlow solution;
        Move goalState;
        ProblemInstance problemInstance;
        List<List<TimedMove>> plan;
        long mcmfTime;
        int solutionCost;

        public OGAM_Run(ProblemInstance instance, Move goalstate)
        {
            this.goalState = goalstate;
            this.problemInstance = instance;
            this.solution = null;
            this.timer = null;
            this.mcmfTime = -1;
            this.plan = null;
            this.solutionCost = -1;
        }

        public long solve(CFMAStar.CostFunction costFunction)
        {
            // Independence Detection
            List<List<TimedMove>> nonConflictsPaths = null;
            IndependentDetection id = new IndependentDetection(this.problemInstance, this.goalState);
            MAM_AgentState[] newStartPositions = id.Detect(out nonConflictsPaths);
            if (newStartPositions.Length != 0)
            {
                this.problemInstance = problemInstance.ReplanProblem(newStartPositions);
                this.reducer = new CFMAM_MCMF_Reducer(this.problemInstance, this.goalState);
                reducer.reduce(costFunction);
                if (reducer.outputProblem == null)
                    return -1;
                MinCostMaxFlow mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
                timer = Stopwatch.StartNew();
                solution = mcmfSolver.SolveMinCostFlow();
                List<TimedMove>[] partialPlan = this.reducer.GetCFMAMSolution(this.solution, this.mcmfTime, true);
                if (costFunction == CFMAStar.CostFunction.MakeSpan)
                {
                    while(!isPathForEachAgent(partialPlan))
                    {
                        this.reducer.addNetworkLayer();
                        mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
                        solution = mcmfSolver.SolveMinCostFlow();
                        partialPlan = this.reducer.GetCFMAMSolution(this.solution, this.mcmfTime, true);
                    }
                }
                timer.Stop();
                this.plan = mergePlans(partialPlan, nonConflictsPaths);
                this.mcmfTime = timer.ElapsedMilliseconds;
                this.solutionCost = calculateCost(this.plan, costFunction);
            }
            else
            {
                this.plan = nonConflictsPaths;
                this.solutionCost = calculateCost(this.plan, costFunction);
            }

            return this.solutionCost;
       }
        
       private bool isPathForEachAgent(List<TimedMove>[] partialPlan)
       {
            for (int i = 0; i < partialPlan.Length; i++)
                if (partialPlan[i].Count == 0)
                    return false;
            return true;
       }

        private int calculateCost(List<List<TimedMove>> plan, CFMAStar.CostFunction costFunction)
        {
            if(costFunction == CFMAStar.CostFunction.SOC)
                return sumSolutionCost(this.plan);
            plan.Sort((x, y) => y.Count - x.Count);
            return plan[0].Count-1;
        }

        private int sumSolutionCost(List<List<TimedMove>> nonConflictsPaths)
        {
            int cost = 0;
            foreach (List<TimedMove> path in nonConflictsPaths)
                cost += path.Count - 1;
            return cost;
        }

        private List<List<TimedMove>> mergePlans(List<TimedMove>[] partialPlan, List<List<TimedMove>> nonConflictsPaths)
        {
            nonConflictsPaths.AddRange(partialPlan);
            return nonConflictsPaths;
        }

        public String getPlan()
        {
            // TODO: implement to string of plan. check implemntation
            List<List<TimedMove>> pathList = new List<List<TimedMove>>();
            int agentIndex = 0;
            String res = "";

            res += "Meeting Point: " + this.goalState.ToString();
            res += "\nCost: " + this.solutionCost + "\n";

            this.plan.ForEach(path => {
            res += "s" + agentIndex + ": " + getAgentPath(path) + "\n";
                agentIndex++;
            });
            return res;
        }

        private string getAgentPath(List<TimedMove> path)
        {
            string agentPath = "";
            for(int i=0; i<path.Count; i++)
            {
                agentPath += path[i];
                if (i != path.Count - 1)
                    agentPath += "->";
            }

            return agentPath;
        }
    }
}
