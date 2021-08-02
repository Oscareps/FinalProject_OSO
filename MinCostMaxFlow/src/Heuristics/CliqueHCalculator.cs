using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    class CliqueHCalculator : MAM_HeuristicCalculator
    {
        ProblemInstance instance;
        double initialH;
        public void init
        (
            ProblemInstance instance
        )
        {
            this.instance = instance;
            this.initialH = -1;
        }

        public void preprocessing() { }

        public double h
        (
            MAM_AgentState state,
            MAM_AgentState parent
        )
        {
            if (parent == null)
                state.h = CalculateInitialH();
            else
            {
                state.h = parent.h * (instance.m_vAgents.Count() - 1);
                MAM_AgentState[] startStates = instance.m_vAgents;
                foreach (MAM_AgentState startState in startStates)
                {
                    if (startState.agentIndex == state.agentIndex)
                        continue;
                    int mdParent = ManhattanDistance(parent.lastMove, startState.lastMove);
                    int mdChild = ManhattanDistance(state.lastMove, startState.lastMove);
                    state.h = state.h - mdParent + mdChild;
                }
                state.h = Math.Max(state.h / (instance.m_vAgents.Count() - 1), 0);
            }
            return state.h;
        }

        private double CalculateInitialH()
        {
            if (initialH != -1)
                return initialH;
            int sumOfDistances = 0;
            MAM_AgentState[] startStates = instance.m_vAgents;
            for (int agentIndex1 = 0; agentIndex1 < startStates.Length; agentIndex1++)
                for (int agentIndex2 = agentIndex1 + 1; agentIndex2 < startStates.Length; agentIndex2++)
                {
                    MAM_AgentState agent1 = startStates[agentIndex1];
                    MAM_AgentState agent2 = startStates[agentIndex2];
                    sumOfDistances += ManhattanDistance(agent1.lastMove, agent2.lastMove);
                }
            initialH = (double)sumOfDistances / (double)(startStates.Length - 1);
            return initialH;
        }



        private int ManhattanDistance
        (
            Move move1,
            Move move2
        )
        {
            return (Math.Abs(move1.x - move2.x) + Math.Abs(move1.y - move2.y));
        }

        public double GetInitialH()
        {
            return initialH;
        }

        public string GetName()
        {
            return "Clique H";
        }

        public int GetNumberOfAgents()
        {
            return instance.GetNumOfAgents();
        }

        public MAM_HeuristicCalculator copyHeuristicCalculator()
        {
            CliqueHCalculator newCliqueHCalculator = new CliqueHCalculator();
            newCliqueHCalculator.instance = this.instance;
            newCliqueHCalculator.initialH = this.initialH;
            return newCliqueHCalculator;
        }
    }
}
