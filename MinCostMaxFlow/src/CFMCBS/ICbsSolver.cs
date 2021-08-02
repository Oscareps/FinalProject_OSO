using System;
using System.IO;
using System.Collections.Generic;

namespace CPF_experiment
{
    public interface MAPF_ISolver : CFMCBS_IStatisticsCsvWriter
    {
        /// <summary>
        /// Return the name of the solver, useful for outputing results.
        /// </summary>
        /// <returns>The name of the solver</returns>
        String GetName();

        /// <summary>
        /// Solves the instance that was set by a call to Setup()
        /// </summary>
        /// <returns></returns>
        bool Solve();

        /// <summary>
        /// Setup the relevant data structures for a run.
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="runner"></param>
        void Setup(ProblemInstance problemInstance, MAM_Run runner);

        /// <summary>
        /// Clears the relevant data structures and variables to free memory usage.
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns the found plan, or null if no plan was found.
        /// </summary>
        /// <returns></returns>
        String GetPlan();

        /// <summary>
        /// Returns the cost of the solution found, or error codes otherwise.
        /// </summary>
        int GetSolutionCost();

        /// <summary>
        /// Gets the delta of (actual solution cost - first state heuristics)
        /// </summary>
        int GetSolutionDepth();

        long GetMemoryUsed();
        int GetMaxGroupSize();

        int GetExpanded();
        int GetGenerated();

        bool isSolved();
    }

    public interface ICbsSolver : MAPF_ISolver, IAccumulatingStatisticsCsvWriter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="minDepth">!@# Shoud be more generally called minTimeStep? Because for CBS the depth isn't the time step</param>
        /// <param name="runner"></param>
        void Setup(ProblemInstance problemInstance, int minDepth, MAM_Run runner, int minCost);
        int[] GetSingleCosts();
        Dictionary<int, int> GetExternalConflictCounts();
        Dictionary<int, List<int>> GetConflictTimes();

        int GetAccumulatedExpanded();
        int GetAccumulatedGenerated();
    }
}
