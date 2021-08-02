using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    class ZeroHCalculator : MAM_HeuristicCalculator
    {

        public void init
        (
            ProblemInstance instance
        )
        {
        }

        public void preprocessing() { }

        public double h
        (
            MAM_AgentState state,
            MAM_AgentState parent
        )
        {
            return 0;
        }

        public double GetInitialH()
        {
            return 0;
        }

        public string GetName()
        {
            return "Zero H";
        }

        public int GetNumberOfAgents()
        {
            return 0;
        }

        public MAM_HeuristicCalculator copyHeuristicCalculator()
        {
            return new ZeroHCalculator();
        }
    }
}
