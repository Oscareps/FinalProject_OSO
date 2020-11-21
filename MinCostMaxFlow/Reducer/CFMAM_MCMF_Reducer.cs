using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.OrTools.Graph;

namespace CPF_experiment
{
    class CFMAM_MCMF_Reducer
    {
        /// <summary>
        /// The grid graph we are working on
        /// </summary>
        private bool[][] problemGrid;

        private Move[] startPositions;
        private int startPositionsToDiscover;
        private Dictionary<KeyValuePair<int, int>, int> startPositionsDict;
        private Move goalState;

        private Stopwatch timer;

        /// <summary>
        /// l is the length of the longest path from one of the agents to the goal node
        /// </summary>
        private int l;

        /// <summary>
        /// T is the worst possible time to get from one of the agents to the goal node. ** T = l+(#agents)-1 **
        /// </summary>
        private int T;

        private int edgeCounter;

        private HashSet<NFReducerNode> NFNodes;

        public NF_ProblemInstance outputProblem;

        public CFMAM_MCMF_Reducer(MAM_ProblemInstance problem, Move goalState)
        {
            this.problemGrid = problem.m_vGrid;

            startPositions = new Move[problem.GetNumOfAgents()];
            this.startPositionsDict = new Dictionary<KeyValuePair<int, int>, int>();
            for (int i=0; i<problem.m_vAgents.Length; i++)
            {
                startPositions[i] = problem.m_vAgents[i].lastMove;
                this.startPositionsDict.Add(new KeyValuePair<int, int>(startPositions[i].x, startPositions[i].y), 1);
            }

            startPositionsToDiscover = problem.GetNumOfAgents();

            this.goalState = goalState;
            this.NFNodes = new HashSet<NFReducerNode>();
            this.l = -1;
            this.edgeCounter = 0;
            NFReducerNode.indexCounter = 0;
        }

        public void GetCFMAMSolution(MinCostFlow mcmfSolution, long mcmfTime, bool printPath = false)
        {
            long optimalCost = mcmfSolution.OptimalCost();
            Console.WriteLine("Goal State: (" + this.goalState.x + "," + this.goalState.y + ")");
            Console.WriteLine("Minimum cost: " + optimalCost);
            Console.WriteLine("Reduction Execution Time: " + ((float)timer.ElapsedMilliseconds / 1000.0) + " Seconds");
            Console.WriteLine("MCMF Execution Time: " + ((float)mcmfTime / 1000.0) + " Seconds");

            if (printPath)
            {
                for (int i = 0; i < outputProblem.numArcs; ++i)
                {
                    long cost = mcmfSolution.Flow(i) * mcmfSolution.UnitCost(i);
                    if (cost != 0)
                    {
                        NFReducerNode fromNode = GetNode(mcmfSolution.Tail(i));
                        NFReducerNode toNode = GetNode(mcmfSolution.Head(i));
                        Console.WriteLine("(" + fromNode.x + "," + fromNode.y + ")" + " -> " +
                                          "(" + toNode.x + "," + toNode.y + ")" + "  " +
                                          string.Format("{0,3}", "Time: " + (T - fromNode.nodeTime + 1)));
                    }
                }
            }
            Console.WriteLine("");
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("");
        }

        private NFReducerNode GetNode(int index)
        {
            foreach(NFReducerNode node in NFNodes)
            {
                if (node.nodeIndex == index)
                    return node;
            }
            return null;
        }

        public void reduce()
        {
            timer = Stopwatch.StartNew();
            CreateNFProblem();
            ImportToMCMFAlgorithm();
            // PrintSolution();
            timer.Stop();
        }

        private void ImportToMCMFAlgorithm()
        {
            this.outputProblem = new NF_ProblemInstance(this.NFNodes.Count, this.edgeCounter);
            foreach(NFReducerNode inputNodeToEdge in NFNodes)
            {
                foreach(NFReducerNode outputNodeFromEdge in inputNodeToEdge.edgeTo)
                {
                    int edgeCost, edgeCapacity;
                    if(outputNodeFromEdge.nodeIndex == 0)
                    {
                        edgeCost = 0;
                        edgeCapacity = this.startPositions.Length;
                    }
                    else if(inputNodeToEdge.isInputNode && !outputNodeFromEdge.isInputNode)
                    {
                        edgeCost = 0;
                        edgeCapacity = 1;
                    }
                    else
                    {
                        edgeCost = 1;
                        edgeCapacity = 1;
                    }
                    outputProblem.AddEdge(inputNodeToEdge.nodeIndex, outputNodeFromEdge.nodeIndex, edgeCost, edgeCapacity);
                }
                if (inputNodeToEdge.nodeTime == T && IsStartPosition(inputNodeToEdge))
                    outputProblem.AddSupply(inputNodeToEdge.nodeIndex, 1);
            }

            outputProblem.AddSupply(0, this.startPositions.Length * (-1));
        }

        private void PrintSolution()
        {
            foreach(NFReducerNode node in NFNodes)
            {
                Console.WriteLine("--------------------------");
                Console.WriteLine("Node Index: " + node.nodeIndex);
                Console.WriteLine("Node position: (" + node.x + "," + node.y + ")");
                Console.WriteLine("Node Time: " + node.nodeTime);
                Console.WriteLine("Node Supply: " + outputProblem.getSupply(node.nodeIndex));
                Console.WriteLine("Is input Node: " + node.isInputNode);
                Console.WriteLine("Edges: ");
                foreach (NFReducerNode edgeTo in node.edgeTo)
                    Console.WriteLine("\tEdge to: " + edgeTo.nodeIndex + ", Edge cost: " + outputProblem.getEdgeCost(node.nodeIndex, edgeTo.nodeIndex) + ", Edge capacity: " + outputProblem.getEdgeCapacity(node.nodeIndex, edgeTo.nodeIndex));
                Console.WriteLine();
                Console.WriteLine("--------------------------");
            }
        }

        /// <summary>
        /// Creates an initial network flow problem reduced from the given problem
        /// </summary>
        private void CreateNFProblem()
        {
            NFReducerNode superSink = new NFReducerNode(-1, -1, -1);
            NFNodes.Add(superSink);

            ReducerOpenList openList = new ReducerOpenList();
            openList.Enqueue(new NFReducerNode(0, goalState.x, goalState.y));
            while (openList.Count != 0)
            {
                NFReducerNode node = openList.Dequeue();
                LinkedList<NFReducerNode> nodeSons = new LinkedList<NFReducerNode>();
                if (l == -1 || (l != -1 && node.nodeTime != T))
                    nodeSons = GetSons(node, openList);
                foreach (NFReducerNode son in nodeSons)
                {
                    son.AddEdgeTo(node);
                    node.AddEdgeFrom(son);
                    if(!openList.Contains(son))
                        openList.Enqueue(son);
                    if (l == -1 && IsStartPosition(son) && this.startPositionsDict[new KeyValuePair<int, int>(son.x, son.y)] == 1)
                    {
                        this.startPositionsDict[new KeyValuePair<int, int>(son.x, son.y)] = 0;
                        startPositionsToDiscover--;
                    }
                    if (l == -1 && startPositionsToDiscover == 0)
                    {
                        l = son.nodeTime;
                        T = l + startPositions.Length - 1;
                    }
                }
                if (!NFNodes.Contains(node))
                    AddAfterDuplicationAndSinkConnection(node, superSink);
            }
        }

        private void AddAfterDuplicationAndSinkConnection(NFReducerNode node, NFReducerNode megaSink)
        {
            if (node.nodeTime != 0 && node.nodeTime != T)
            {
                NFReducerNode dupNode = node.Duplicate();
                foreach (NFReducerNode nodeTo in node.edgeTo)
                {
                    dupNode.AddEdgeTo(nodeTo);
                    this.edgeCounter++;
                }
                node.edgeTo.Clear();
                node.AddEdgeTo(dupNode);
                this.edgeCounter++;
                if (node.x == goalState.x && node.y == goalState.y)
                {
                    node.AddEdgeTo(megaSink);
                    dupNode.AddEdgeTo(megaSink);
                    this.edgeCounter += 2;
                }
                NFNodes.Add(dupNode);
            }
            else
            {
                edgeCounter += node.edgeTo.Count;
                if (node.x == goalState.x && node.y == goalState.y)
                {
                    node.AddEdgeTo(megaSink);
                    this.edgeCounter++;
                }
            }

            NFNodes.Add(node);
        }


        /// <summary>
        /// Discovers all of the neighbors of a given node
        /// </summary>
        /// <param name="node"> Node to discover neighbors to </param>
        /// <param name="openList"> linked list containing all of the unhandled nodes. recieved to check that neighboor is not already discovered </param>
        /// <returns> List of all node neighboors </returns>
        private LinkedList<NFReducerNode> GetSons(NFReducerNode node, ReducerOpenList openList)
        {
            LinkedList<NFReducerNode> sons = new LinkedList<NFReducerNode>();

            NFReducerNode son = new NFReducerNode(node.nodeTime + 1, node.x, node.y);
            if (openList.Contains(son))
            {
                son = openList.Get(son);
                NFReducerNode.DecreaseIndexCounter();
            }
            sons.AddLast(son);

            if (node.y != this.problemGrid.Length - 1 && !problemGrid[node.y + 1][node.x])
            {
                son = new NFReducerNode(node.nodeTime + 1, node.x, node.y + 1);
                if (openList.Contains(son))
                {
                    son = openList.Get(son);
                    NFReducerNode.DecreaseIndexCounter();
                }
                sons.AddLast(son);
            }
            if (node.y != 0 && !problemGrid[node.y - 1][node.x])
            {
                son = new NFReducerNode(node.nodeTime + 1, node.x, node.y-1);
                if (openList.Contains(son))
                {
                    son = openList.Get(son);
                    NFReducerNode.DecreaseIndexCounter();
                }
                sons.AddLast(son);
            }
            if (node.x != this.problemGrid[node.y].Length - 1 && !problemGrid[node.y][node.x + 1])
            {
                son = new NFReducerNode(node.nodeTime + 1, node.x + 1, node.y);
                if (openList.Contains(son))
                {
                    son = openList.Get(son);
                    NFReducerNode.DecreaseIndexCounter();
                }
                sons.AddLast(son);
            }
            if (node.x != 0 && !problemGrid[node.y][node.x - 1])
            {
                son = new NFReducerNode(node.nodeTime + 1, node.x - 1, node.y);
                if (openList.Contains(son))
                {
                    son = openList.Get(son);
                    NFReducerNode.DecreaseIndexCounter();
                }
                sons.AddLast(son);
            }

            return sons;
        }
        
        /// <summary>
        /// Checks if a given node is a start position
        /// </summary>
        /// <param name="node"> Node to check </param>
        /// <returns> true if the node is a start position, false otherwise </returns>
        private bool IsStartPosition(NFReducerNode node)
        {
            foreach (Move startPos in this.startPositions)
                if (node.x == startPos.x && node.y == startPos.y)
                    return true;
            return false;
        }
    }
}
