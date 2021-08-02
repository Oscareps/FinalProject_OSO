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
        
        private List<NFReducerNode> zeroLayer;

        private NFReducerNode superSink;

        public CFMAM_MCMF_Reducer(ProblemInstance problem, Move goalState)
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
            this.T = -1;
            this.edgeCounter = 0;
            this.outputProblem = null;
            NFReducerNode.indexCounter = 0;
            this.zeroLayer = new List<NFReducerNode>();
        }
        
        internal void addNetworkLayer()
        {
            ReducerOpenList<NFReducerNode> openList = new ReducerOpenList<NFReducerNode>();
            this.T++;
            foreach (NFReducerNode node in this.zeroLayer)
            {
                NFNodes.Remove(node);
                openList.Enqueue(node);
            }
            this.zeroLayer = new List<NFReducerNode>();
            while (openList.Count != 0)
            {
                NFReducerNode node = openList.Dequeue();
                LinkedList<NFReducerNode> nodeSons = new LinkedList<NFReducerNode>();
                if (node.nodeTime != T)
                    nodeSons = GetSons(node, openList);
                foreach (NFReducerNode son in nodeSons)
                {
                    son.AddEdgeTo(node);
                    node.AddEdgeFrom(son);
                    if (!openList.Contains(son))
                        openList.Enqueue(son);
                }
                if (!NFNodes.Contains(node))
                    AddAfterDuplicationAndSinkConnection(node);
            }

            ImportToMCMFAlgorithm();
        }
        
        public List<TimedMove>[] GetCFMAMSolution(MinCostFlow mcmfSolution, long mcmfTime, bool printPath = false)
        {
            Stack<NFReducerNode>[] paths = new Stack<NFReducerNode>[startPositions.Length];
            Stack<NFReducerNode[]>[] nodesForEachTime = new Stack<NFReducerNode[]>[this.T + 1];

            for(int i=0; i<nodesForEachTime.Length; i++)
                nodesForEachTime[i] = new Stack<NFReducerNode[]>();
            // Sorting each move to it's time
            for (int i = 0; i < outputProblem.numArcs; i++)
            {
                long cost = mcmfSolution.Flow(i) * mcmfSolution.UnitCost(i);
                if (cost != 0)
                {
                    NFReducerNode fromNode = GetNode(mcmfSolution.Tail(i));
                    NFReducerNode toNode = GetNode(mcmfSolution.Head(i));
                    if (T - fromNode.nodeTime == 0)
                    {
                        NFReducerNode[] nodesStartArray = { null, fromNode };
                        nodesForEachTime[0].Push(nodesStartArray);
                    }

                    NFReducerNode[] nodesArray = { fromNode, toNode };
                    nodesForEachTime[T - toNode.nodeTime].Push(nodesArray); 
                }
            }

            // Inserting start nodes to each agent path
            int startNodesCounter = 0;
            foreach(NFReducerNode[] startNode in nodesForEachTime[0])
            {
                paths[startNodesCounter] = new Stack<NFReducerNode>();
                paths[startNodesCounter].Push(startNode[1]);
                startNodesCounter++;
            }

            // Searching agents that started on the meeting points
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == null)
                {
                    if (isAgentStartedOnGoalNode())
                    {
                        paths[i] = new Stack<NFReducerNode>();
                        paths[i].Push(new NFReducerNode(0, goalState.x, goalState.y));
                    }
                    else
                        paths = paths.Where(p => p != null).ToArray();
                    break;
                }
            }

            // Adding each node of each agent to his path
            for (int i=1; i<nodesForEachTime.Length; i++)
            {
                while (nodesForEachTime[i].Count != 0)
                {
                    NFReducerNode[] move = nodesForEachTime[i].Pop();
                    for (int j = 0; j < paths.Length; j++)
                    {
                        NFReducerNode lastNode = paths[j].Peek();
                        if (lastNode.x == move[0].x && lastNode.y == move[0].y && lastNode.nodeTime == move[0].nodeTime)
                        {
                            paths[j].Push(move[1]);
                            break;
                        }
                    }
                }
            }

            List<TimedMove>[] agentPaths = new List<TimedMove>[startPositions.Length];
            for (int i = 0; i < agentPaths.Length; i++)
                agentPaths[i] = new List<TimedMove>();

            for(int i=0; i<paths.Length; i++)
            {
                int pathLength = paths[i].Count;
                while (paths[i].Count != 0)
                {
                    int nodeTime = pathLength - paths[i].Count;
                    NFReducerNode node = paths[i].Pop();
                    agentPaths[i].Insert(0, new TimedMove(node.x, node.y, Move.Direction.NO_DIRECTION, nodeTime));
                }
            }

            return agentPaths;
        }

         private bool isAgentStartedOnGoalNode()
        {
            foreach (Move startPosition in this.startPositions)
                if (startPosition.x == this.goalState.x && startPosition.y == this.goalState.y)
                    return true;
            return false;
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

        public void reduce(CFMAStar.CostFunction costFunction)
        {
            timer = Stopwatch.StartNew();
            if(CreateNFProblem(costFunction))
                ImportToMCMFAlgorithm();
            timer.Stop();
        }

        private void ImportToMCMFAlgorithm()
        {
            this.outputProblem = new NF_ProblemInstance(this.NFNodes.Max(x => x.nodeIndex), this.edgeCounter);
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
        private bool CreateNFProblem(CFMAStar.CostFunction costFunction)
        {
            this.superSink = new NFReducerNode(-1, -1, -1);
            NFNodes.Add(this.superSink);

            ReducerOpenList<NFReducerNode> openList = new ReducerOpenList<NFReducerNode>();
            openList.Enqueue(new NFReducerNode(0, goalState.x, goalState.y));
            while (openList.Count != 0)
            {

                //if (timer.ElapsedMilliseconds > Constants.MCMF_MAX_TIME)
                //    return false;

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
                        if (costFunction == CFMAStar.CostFunction.SOC)
                            T = l + startPositions.Length - 1;
                        else
                            T = l;                    }
                }
                if (!NFNodes.Contains(node))
                    AddAfterDuplicationAndSinkConnection(node);
            }

            return true;
        }

        private void AddAfterDuplicationAndSinkConnection(NFReducerNode node)
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
                    node.AddEdgeTo(this.superSink);
                    dupNode.AddEdgeTo(this.superSink);
                    this.edgeCounter += 2;
                }
                NFNodes.Add(dupNode);
            }
            else
            {
                if (node.nodeTime == T)
                    this.zeroLayer.Add(node);
                edgeCounter += node.edgeTo.Count;
                if (node.x == goalState.x && node.y == goalState.y)
                {
                    node.AddEdgeTo(this.superSink);
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
        private LinkedList<NFReducerNode> GetSons(NFReducerNode node, ReducerOpenList<NFReducerNode> openList)
        {
            LinkedList<NFReducerNode> sons = new LinkedList<NFReducerNode>();

            CollectSon(openList, sons, node.nodeTime + 1, node.x, node.y);

            if (node.x != this.problemGrid.Length - 1 && !problemGrid[node.x + 1][node.y])
            {
                CollectSon(openList, sons, node.nodeTime + 1, node.x + 1, node.y);
            }
            if (node.x != 0 && !problemGrid[node.x - 1][node.y])
            {
                CollectSon(openList, sons, node.nodeTime + 1, node.x - 1, node.y);
            }
            if (node.y != this.problemGrid[node.x].Length - 1 && !problemGrid[node.x][node.y + 1])
            {
                CollectSon(openList, sons, node.nodeTime + 1, node.x, node.y + 1);
            }
            if (node.y != 0 && !problemGrid[node.x][node.y - 1])
            {
                CollectSon(openList, sons ,node.nodeTime + 1, node.x, node.y - 1);
            }

            return sons;
        }

        private void CollectSon(ReducerOpenList<NFReducerNode> openList, LinkedList<NFReducerNode> sons, int nodeTime, int x, int y)
        {
            NFReducerNode son = new NFReducerNode(nodeTime, x, y);
            if (openList.Contains(son))
            {
                son = openList.Get(son);
                NFReducerNode.DecreaseIndexCounter();
            }
            sons.AddLast(son);
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
