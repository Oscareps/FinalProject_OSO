using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    class NF_ProblemInstance
    {
        public int numNodes;
        public int numArcs;

        /// <summary>
        /// List of input nodes of each edge in output problem
        /// </summary>
        public int[] startNodes;

        /// <summary>
        /// List of output nodes of each edge in output problem
        /// </summary>
        public int[] endNodes;

        /// <summary>
        /// cost capacity and supplies for each edge in output problem
        /// </summary>
        public int[] unitCosts;
        public int[] capacities;
        public int[] supplies;

        private int currEdges;



        public NF_ProblemInstance(int numNodes, int numArcs)
        {
            this.numNodes = numNodes;
            this.numArcs = numArcs;

            this.supplies = new int[numNodes];
            for (int i = 0; i < supplies.Length; i++)
                supplies[i] = 0;

            this.startNodes = new int[numArcs];
            this.endNodes = new int[numArcs];
            this.unitCosts = new int[numArcs];
            this.capacities = new int[numArcs];

            currEdges = 0;
        }

        public void AddEdge(int edgeInput, int edgeOutput, int edgeCost, int edgeCapacity)
        {
            this.startNodes[currEdges] = edgeInput;
            this.endNodes[currEdges] = edgeOutput;
            this.unitCosts[currEdges] = edgeCost;
            this.capacities[currEdges] = edgeCapacity;
            currEdges++;
        }

        public void AddSupply(int nodeIndex, int supply)
        {
            supplies[nodeIndex] = supply;
        }

        public int getEdgeCost(int input, int output)
        {
            for (int i = 0; i < startNodes.Length; i++)
                if (startNodes[i] == input && endNodes[i] == output)
                    return unitCosts[i];
            return -1;
        }

        public int getEdgeCapacity(int input, int output)
        {
            for (int i = 0; i < startNodes.Length; i++)
                if (startNodes[i] == input && endNodes[i] == output)
                    return capacities[i];
            return -1;
        }

        public int getSupply(int nodeIndex)
        {
            return this.supplies[nodeIndex];
        }


    }
}
