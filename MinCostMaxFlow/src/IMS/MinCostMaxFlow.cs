using Google.OrTools.Graph;
using System;


namespace CPF_experiment
{
    class MinCostMaxFlow
    {
        private int numNodes;
        private int numArcs;
        private int[] startNodes;
        private int[] endNodes;
        private int[] unitCosts;
        private int[] capacities;
        private int[] supplies;

        public MinCostMaxFlow(NF_ProblemInstance problem)
        {
            this.numNodes = problem.numNodes;
            this.numArcs = problem.numArcs;
            this.startNodes = problem.startNodes;
            this.endNodes = problem.endNodes;
            this.unitCosts = problem.unitCosts;
            this.capacities = problem.capacities;
            this.supplies = problem.supplies;
        }

        public MinCostFlow SolveMinCostFlow()
        {
            // Instantiate a SimpleMinCostFlow solver.
            MinCostFlow minCostFlow = new MinCostFlow();

            // Add each arc.
            for (int i = 0; i < numArcs; ++i)
            {
                int arc = minCostFlow.AddArcWithCapacityAndUnitCost(startNodes[i], endNodes[i],
                                                     capacities[i], unitCosts[i]);
                if (arc != i) throw new Exception("Internal error");
            }

            // Add node supplies.
            for (int i = 0; i < numNodes; ++i)
            {
                minCostFlow.SetNodeSupply(i, supplies[i]);
            }

            // Find the min cost flow.
            int solveStatus = (int)minCostFlow.SolveMaxFlowWithMinCost();
            if (solveStatus == (int)MinCostFlow.Status.OPTIMAL)
            {
                //PrintNetworkFlowSolution(minCostFlow);
                return minCostFlow;
            }
            else
            {
                Console.WriteLine("Solving the min cost flow problem failed. Solver status: " +
                                  solveStatus);
                return null;
            }
        }

        public void PrintNetworkFlowSolution(MinCostFlow networkFlowSolution)
        {
            long optimalCost = networkFlowSolution.OptimalCost();
            Console.WriteLine("Minimum cost: " + optimalCost);
            Console.WriteLine("");
            Console.WriteLine(" Edge   Flow / Capacity  Cost");
            for (int i = 0; i < numArcs; ++i)
            {
                long cost = networkFlowSolution.Flow(i) * networkFlowSolution.UnitCost(i);
                Console.WriteLine(networkFlowSolution.Tail(i) + " -> " +
                                  networkFlowSolution.Head(i) + "  " +
                                  string.Format("{0,3}", networkFlowSolution.Flow(i)) + "  / " +
                                  string.Format("{0,3}", networkFlowSolution.Capacity(i)) + "       " +
                                  string.Format("{0,3}", cost));
            }
        }
    }
}