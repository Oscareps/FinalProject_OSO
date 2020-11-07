using Google.OrTools.Graph;
using System;


namespace MinCostMaxFlow
{
    class ProgramMinCostMaxFlowExample
    {
        private static void SolveMinCostFlow()
        {
            // Define four parallel arrays: sources, destinations, capacities, and unit costs
            // between each pair. For instance, the arc from node 0 to node 1 has a
            // capacity of 15.
            // Problem taken From Taha's 'Introduction to Operations Research',
            // example 6.4-2.

            int numNodes = 29;
            int numArcs = 49;
            //int[] startNodes1 = { 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 15, 16, 17, 18, 19, 20, 40, 50, 60, 70, 80, 90, 1, 15, 3, 17, 5, 19, 8, 15, 10, 17, 12, 19, 15, 40, 17, 60, 19, 80, 40, 50, 60, 70, 80, 90, 100 };
            int[] startNodes =  { 0, 1, 2, 3, 4, 5, 7, 8,  9,  10, 11,  12, 14, 15, 16, 17, 18, 19, 21, 22, 23, 24, 25, 26, 0, 14, 2  , 16, 4, 18,  7, 14, 9, 16 , 11, 18, 14, 21, 16, 23, 18, 25, 21, 22, 23, 24, 25, 26, 27 };
            
            //int[] endNodes1 = { 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 16, 17, 18, 19, 20, 30, 50, 60, 70, 80, 90, 100, 16, 2, 18, 4, 20, 6, 16, 9, 18, 11, 20, 13, 50, 16, 70, 18, 90, 20, 200, 200, 200, 200, 200, 200, 200 };
            int[] endNodes = {    1, 2, 3, 4, 5, 6, 8, 9,  10, 11, 12, 13, 15, 16, 17,  18, 19, 20, 22, 23, 24, 25, 26, 27, 15, 1, 17 , 3, 19, 5,  15, 8, 17, 10,  19, 12, 22, 15, 24, 17, 26, 19, 28, 28, 28, 28, 28, 28, 28 };

            int[] unitCosts = {   1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
            int[] capacities = {  1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2 };

            // Define an array of supplies at each node.

            int[] supplies = { 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -2 };



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
