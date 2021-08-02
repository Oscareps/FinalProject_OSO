using System.Collections.Generic;
using System.IO;

namespace CPF_experiment
{
    public interface MAM_HeuristicCalculator
    {

        /// <summary>Returns the heuristic estimate.</summary>
        /// <param name="s">The current state.</param>
        double h(MAM_AgentState state, MAM_AgentState parent);

        /// <summary>
        /// Initializes the pattern database by storing references to the
        /// problem instance and also the subset of agents that the pattern
        /// database pertains to.
        /// </summary>
        /// <param name="pi">The problem instance.</param>
        void init(ProblemInstance instance);

        double GetInitialH();

        void preprocessing();

        string GetName();

        int GetNumberOfAgents();         // for makespan subsets

        MAM_HeuristicCalculator copyHeuristicCalculator();
    }


}
