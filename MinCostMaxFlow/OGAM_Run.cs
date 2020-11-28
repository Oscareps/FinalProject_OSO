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
        long mcmfTime;

        public OGAM_Run(MAM_ProblemInstance instance, Move goalstate)
        {
            this.goalState = goalstate;
            this.reducer = new CFMAM_MCMF_Reducer(instance, goalstate);
            this.solution = null;
            this.timer = null;
            this.mcmfTime = -1;
        }

        public long solve()
        {
            reducer.reduce();
            if (reducer.outputProblem == null)
                return -1;
            MinCostMaxFlow mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
            timer = Stopwatch.StartNew();
            solution = mcmfSolver.SolveMinCostFlow();
            timer.Stop();
            this.mcmfTime = timer.ElapsedMilliseconds;
            return solution.OptimalCost();
        }

        internal string getPlan(bool printPath = true)
        {
            return this.reducer.GetCFMAMSolution(this.solution, this.mcmfTime, printPath);
        }
    }
}
