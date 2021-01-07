﻿using System;
using System.IO;
using System.Collections.Generic;

namespace CPF_experiment
{
    public interface MAPF_ISolver : MAPF_IStatisticsCsvWriter
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
        void Setup(MAM_ProblemInstance problemInstance, MAM_Run runner);

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

    public interface ICbsSolver : MAPF_ISolver
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="problemInstance"></param>
        /// <param name="minDepth">!@# Shoud be more generally called minTimeStep? Because for CBS the depth isn't the time step</param>
        /// <param name="runner"></param>
        void Setup(MAM_ProblemInstance problemInstance, MAM_Run runner);

        int GetAccumulatedExpanded();
        int GetAccumulatedGenerated();
    }
}
