using System;
using System.IO;
using System.Collections.Generic;

namespace CPF_experiment
{
    public interface MAM_ISolver : MAM_IStatisticsCsvWriter
    {
        MAM_Run.CostFunction GetCostFunction();
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
        /// Add constraints to the problem instance.
        /// </summary>
        /// <param name="constraints"></param>
        void AddConstraints(HashSet<MMStarConstraint> constraints);


        /// <summary>
        /// Set the heuristic
        /// </summary>
        /// <param name="heuristic"></param>
        void SetHeuristic(MAM_HeuristicCalculator hCalculator);

        MAM_HeuristicCalculator GetHeuristicCalculator();

        /// <summary>
        /// Clears the relevant data structures and variables to free memory usage.
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns the found plan, or null if no plan was found.
        /// </summary>
        /// <returns></returns>
        MAM_Plan GetPlan();

        Tuple<double, int> GetHeuristicCalculatorInitialH();
        /// <summary>
        /// Returns the cost of the solution found, or error codes otherwise.
        /// </summary>
        int GetSolutionMakeSpanCost();
        int GetSolutionSOCCost();
        /// <summary>
        /// Gets the delta of (actual solution cost - first state heuristics)
        /// </summary>
        int GetSolutionDepth();
        long GetMemoryUsed();
        int GetExpanded();
        int GetGenerated();

        bool IsSolved();
    }


}
