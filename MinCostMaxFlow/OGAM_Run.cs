using Google.OrTools.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CPF_experiment
{
    class OGAM_Run
    {
        public static long solve(MAM_ProblemInstance instance, Move goalstate)
        {
            CFMAM_MCMF_Reducer reducer = new CFMAM_MCMF_Reducer(instance, goalstate);
            reducer.reduce();
            MinCostMaxFlow mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
            Stopwatch timer = Stopwatch.StartNew();
            MinCostFlow solution = mcmfSolver.SolveMinCostFlow();
            timer.Stop();
            if (solution != null)
            {
                reducer.GetCFMAMSolution(solution, timer.ElapsedMilliseconds);
                return solution.OptimalCost();
            }
            return -1;
        }
    }
}
