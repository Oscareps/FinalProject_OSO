using System;
using Google.OrTools.Graph;


namespace MinCostMaxFlow
{
    class Program
    {
        private static void SolveMinCostFlow()
        {
            // Define four parallel arrays: sources, destinations, capacities, and unit costs
            // between each pair. For instance, the arc from node 0 to node 1 has a
            // capacity of 15.
            // Problem taken From Taha's 'Introduction to Operations Research',
            // example 6.4-2.

            int numNodes = 5;
            int numArcs = 9;
            int[] startNodes = { 0, 0, 1, 1, 1, 2, 2, 3, 4 };
            int[] endNodes = { 1, 2, 2, 3, 4, 3, 4, 4, 2 };
            int[] capacities = { 15, 8, 20, 4, 10, 15, 4, 20, 5 };
            int[] unitCosts = { 4, 4, 2, 2, 6, 1, 3, 2, 3 };

            // Define an array of supplies at each node.

            int[] supplies = { 20, 0, 0, -5, -15 };



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


            //Console.WriteLine("Solving min cost flow with " + numNodes + " nodes, and " +
            //                  numArcs + " arcs, source=" + source + ", sink=" + sink);

            // Find the min cost flow.
            int solveStatus = (int)minCostFlow.Solve();
            if (solveStatus == (int)MinCostFlow.Status.OPTIMAL)
            {
                long optimalCost = minCostFlow.OptimalCost();
                Console.WriteLine("Minimum cost: " + optimalCost);
                Console.WriteLine("");
                Console.WriteLine(" Edge   Flow / Capacity  Cost");
                for (int i = 0; i < numArcs; ++i)
                {
                    long cost = minCostFlow.Flow(i) * minCostFlow.UnitCost(i);
                    Console.WriteLine(minCostFlow.Tail(i) + " -> " +
                                      minCostFlow.Head(i) + "  " +
                                      string.Format("{0,3}", minCostFlow.Flow(i)) + "  / " +
                                      string.Format("{0,3}", minCostFlow.Capacity(i)) + "       " +
                                      string.Format("{0,3}", cost));
                }
            }
            else
            {
                Console.WriteLine("Solving the min cost flow problem failed. Solver status: " +
                                  solveStatus);
            }
        }

        static void Main()
        {
            SolveMinCostFlow();
        }
    }
}
