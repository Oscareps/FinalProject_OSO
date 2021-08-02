using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    // This heuristics only work for 2D grid
    class MedianHCalculator : MAM_HeuristicCalculator
    {
        ProblemInstance instance;
        double initialH;
        int[][] medians;
        int[] h0;

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
            int dim = 2;
            int[] curr = { state.lastMove.x, state.lastMove.y };

            if (parent == null)
            {
                state.h = CalculateInitialH();
                state.hToMeeting = 0;
                for (int i = 0; i < dim; i++)
                {
                    state.hToMeeting += Math.Abs(curr[i] - medians[i][1]);
                }
                return state.h;
            }

            MAM_AgentState[] startStates = instance.m_vAgents;
            state.h = 0;
            state.hToMeeting = 0;
            int[] start = { startStates[instance.GetAgentIndexInArray(state.agentIndex)].lastMove.x, startStates[instance.GetAgentIndexInArray(state.agentIndex)].lastMove.y };

            for (int i = 0; i < dim; i++)
            {
                if (medians[i].Length == 2) // even number of agents
                {
                    if (medians[i][0] < start[i] && medians[i][0] < curr[i])
                    {
                        state.h += h0[i] - start[i] + curr[i];
                        state.hToMeeting += curr[i] - medians[i][0];
                    }
                    else if (start[i] < medians[i][1] && curr[i] < medians[i][1])
                    {
                        state.h += h0[i] + start[i] - curr[i];
                        state.hToMeeting += medians[i][1] - curr[i];

                    }
                    else if (start[i] <= medians[i][0] && medians[i][1] <= curr[i])
                    {
                        state.h += h0[i] + start[i] + curr[i] - 2 * medians[i][1];
                        state.hToMeeting += curr[i] - medians[i][1];
                    }
                    else // i.e., curr[i] <= medians[i][0] && medians[i][1] <= start[i] 
                    {
                        state.h += h0[i] - start[i] - curr[i] + 2 * medians[i][0];
                        state.hToMeeting += medians[i][0] - curr[i];
                    }
                }
                else // odd number of agents
                {
                    if (medians[i][1] < start[i] && medians[i][1] < curr[i])
                    {
                        state.h += h0[i] - start[i] + curr[i];
                        state.hToMeeting += curr[i] - medians[i][1];
                    }
                    else if (start[i] < medians[i][1] && curr[i] < medians[i][1])
                    {
                        state.h += h0[i] + start[i] - curr[i];
                        state.hToMeeting += medians[i][1] - curr[i];
                    }
                    else if (start[i] <= medians[i][1] && medians[i][1] <= curr[i])
                    {
                        state.h += h0[i] + start[i] - medians[i][1] + Math.Max(curr[i] - medians[i][2], 0);
                        state.hToMeeting += Math.Max(curr[i] - medians[i][2], 0);
                    }
                    else // i.e., curr[i] <= medians[i][1] && medians[i][1] <= start[i] 
                    {
                        state.h += h0[i] - start[i] + medians[i][1] + Math.Max(medians[i][0] - curr[i], 0);
                        state.hToMeeting += Math.Max(medians[i][0] - curr[i], 0);
                    }
                }
            }
            // for debug
            /*AgentState temp = startStates[state.agentIndex];
            startStates[state.agentIndex] = state;
            double debug = CalculateH(startStates);
            if (debug != state.h)
                Console.WriteLine("ERROR!!!");
            startStates[state.agentIndex] = temp;*/
            return state.h;
        }

        private double CalculateInitialH()
        {
            if (initialH != -1)
                return initialH;

            int dim = 2;
            initialH = 0;
            int[][] X = new int[2][];
            X[0] = new int[instance.m_vAgents.Count()];
            X[1] = new int[instance.m_vAgents.Count()];
            foreach (MAM_AgentState startState in instance.m_vAgents)
            {
                X[0][instance.GetAgentIndexInArray(startState.agentIndex)] = startState.lastMove.x;
                X[1][instance.GetAgentIndexInArray(startState.agentIndex)] = startState.lastMove.y;
            }

            medians = new int[dim][];
            h0 = new int[dim];
            for (int i = 0; i < dim; i++)
            {
                if (instance.m_vAgents.Count() % 2 == 0) // even number of agents, i.e., n = 2k
                {
                    medians[i] = new int[2]; // store the kth and (k+1)th largest coordinates over all start locations
                    // note that the indices of an array starts from 0. 
                    medians[i][0] = QuickSelect(X[i], 0, X[i].Length - 1, X[i].Length / 2 - 1);
                    medians[i][1] = QuickSelect(X[i], 0, X[i].Length - 1, X[i].Length / 2);
                }
                else // odd number of agents, i.e., n = 2k + 1 
                {
                    medians[i] = new int[3]; // stote the (k-1)th, kth and (k+1)th largest coordinates over all start locations
                    medians[i][0] = QuickSelect(X[i], 0, X[i].Length - 1, X[i].Length / 2 - 1);
                    medians[i][1] = QuickSelect(X[i], 0, X[i].Length - 1, X[i].Length / 2);
                    medians[i][2] = QuickSelect(X[i], 0, X[i].Length - 1, X[i].Length / 2 + 1);
                }

                h0[i] = 0;
                foreach (int x in X[i])
                {
                    h0[i] += Math.Abs(x - medians[i][1]);
                }
                initialH += h0[i];
            }
            return initialH;
        }

        private double CalculateH
        (
            MAM_AgentState[] startStates
        )
        {
            int[][] X = new int[2][];
            X[0] = new int[instance.m_vAgents.Count()];
            X[1] = new int[instance.m_vAgents.Count()];
            foreach (MAM_AgentState startState in startStates)
            {
                X[0][instance.GetAgentIndexInArray(startState.agentIndex)] = startState.lastMove.x;
                X[1][instance.GetAgentIndexInArray(startState.agentIndex)] = startState.lastMove.y;
            }
            int dim = 2;
            int[] mid = new int[dim];
            for (int i = 0; i < dim; i++)
            {
                mid[i] = FindMedian(X[i]);
            }
            double h = 0;
            for (int i = 0; i < dim; i++)
            {
                foreach (int x in X[i])
                {
                    h += Math.Abs(x - mid[i]);
                }
            }
            return h;
        }

        private int FindMedian
        (
            int[] x
         )
        {
            return QuickSelect(x, 0, x.Length - 1, x.Length / 2);
        }

        private int QuickSelect
        (
            int[] x,
            int low,
            int high,
            int k
         )
        {
            int temp = 0;
            int it = low, pivot = x[high];
            if (x[low] < x[high])
                it++;
            for (int j = low + 1; j < high; j++)
            {
                if (x[j] <= pivot)
                {
                    temp = x[j];
                    x[j] = x[it];
                    x[it] = temp;
                    it++;
                }
            }
            if (it < high)
            {
                x[high] = x[it];
                x[it] = pivot;
            }
            if (it == k)
                return x[it];
            else if (it > k)
                return QuickSelect(x, low, it - 1, k);
            else
                return QuickSelect(x, it + 1, high, k);
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
            return "Median H";
        }

        public int GetNumberOfAgents()
        {
            return instance.GetNumOfAgents();
        }

        public MAM_HeuristicCalculator copyHeuristicCalculator()
        {
            MedianHCalculator newMedianHCalculator = new MedianHCalculator();
            newMedianHCalculator.instance = this.instance;
            newMedianHCalculator.initialH = this.initialH;
            newMedianHCalculator.medians = this.medians;
            newMedianHCalculator.h0 = this.h0;
            return newMedianHCalculator;
        }
    }
}
