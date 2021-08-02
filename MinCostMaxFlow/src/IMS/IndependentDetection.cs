using System;
using System.Collections.Generic;

namespace CPF_experiment
{
    public class IndependentDetection
    {
        private ProblemInstance instance;
        private Move goalState;

        public IndependentDetection(ProblemInstance instance, Move goalState)
        {
            this.instance = instance;
            this.goalState = goalState;
        }

        public MAM_AgentState[] Detect(out List<List<TimedMove>> nonConflictsPaths)
        {
            List<List<TimedMove>> paths = bfsToStartPositions(this.goalState);
            List<MAM_AgentState> conflictedStartPositions = new List<MAM_AgentState>();
            gatherAndRemoveConflictedAgents(paths, conflictedStartPositions);
            nonConflictsPaths = paths;
            return conflictedStartPositions.ToArray();
        }

        private void gatherAndRemoveConflictedAgents(List<List<TimedMove>> paths, List<MAM_AgentState> startPositionsDeleted)
        {
            int conflictedPathLength = findAndRemoveFirstConflict(paths, startPositionsDeleted);
            if (conflictedPathLength == 0)
                return;
            for (int i = conflictedPathLength; i < conflictedPathLength + startPositionsDeleted.Count; i++)
                removePaths(paths, i, startPositionsDeleted);
            gatherAndRemoveConflictedAgents(paths, startPositionsDeleted);
        }

        private void removePaths(List<List<TimedMove>> paths, int pathLength, List<MAM_AgentState> startPositionsDeleted)
        {
            List<int> toDelete = new List<int>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].Count == pathLength)
                {
                    toDelete.Add(i);
                    startPositionsDeleted.Add(findStartPosition(paths[i][0]));
                }
            }
            toDelete.Sort();
            toDelete.Reverse();
            foreach (int deleteIndex in toDelete)
                paths.RemoveAt(deleteIndex);
        }

        private int findAndRemoveFirstConflict(List<List<TimedMove>> paths, List<MAM_AgentState> startPositionsDeleted)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                for (int j = i + 1; j < paths.Count; j++)
                {
                    if (paths[i].Count != paths[j].Count)
                        continue;
                    for (int k = 0; k < paths[i].Count - 1; k++)
                    {
                        if (paths[i][k].x == paths[j][k].x && paths[i][k].y == paths[j][k].y)
                        {
                            int pathLength = paths[i].Count;
                            startPositionsDeleted.Add(findStartPosition(paths[i][0]));
                            startPositionsDeleted.Add(findStartPosition(paths[j][0]));
                            paths.RemoveAt(j);
                            paths.RemoveAt(i);
                            return pathLength;
                        }
                    }
                }
            }
            return 0;
        }

        public List<List<TimedMove>> bfsToStartPositions(Move goalState)
        {
            ReducerOpenList<BFSNode> openList = new ReducerOpenList<BFSNode>();
            HashSet<BFSNode> closedList = new HashSet<BFSNode>();

            List<List<TimedMove>> paths = new List<List<TimedMove>>();
            openList.Enqueue(new BFSNode(goalState));
            int startPositionsFound = 0;
            while (openList.Count != 0)
            {
                BFSNode node = openList.Dequeue();
                if (closedList.Contains(node))
                    continue;
                if (findStartPosition(node.position) != null)
                {
                    addPath(instance, paths, node);
                    startPositionsFound++;
                    if (startPositionsFound == this.instance.m_vAgents.Length)
                        break;
                }
                GetSons(node, openList, instance);
                closedList.Add(node);
            }
            return paths;
        }

        private void addPath(ProblemInstance problemInstance, List<List<TimedMove>> paths, BFSNode node)
        {
            List<TimedMove> newPath = new List<TimedMove>();
            BFSNode currNode = node;
            while (currNode != null)
            {
                newPath.Add(currNode.position);
                currNode = currNode.lastMove;
            }
            paths.Add(newPath);
        }

        private MAM_AgentState findStartPosition(Move node)
        {
            foreach (MAM_AgentState startPos in this.instance.m_vAgents)
                if (startPos.lastMove.x == node.x && startPos.lastMove.y == node.y)
                    return startPos;
            return null;
        }

        private void GetSons(BFSNode node, ReducerOpenList<BFSNode> openList, ProblemInstance problem)
        {

            bool[][] problemGrid = problem.m_vGrid;

            if (node.position.x != problemGrid.Length - 1 && !problemGrid[node.position.x + 1][node.position.y])
            {
                CollectSon(openList, node, node.position.x + 1, node.position.y);
            }
            if (node.position.x != 0 && !problemGrid[node.position.x - 1][node.position.y])
            {
                CollectSon(openList, node, node.position.x - 1, node.position.y);
            }
            if (node.position.y != problemGrid[node.position.x].Length - 1 && !problemGrid[node.position.x][node.position.y + 1])
            {
                CollectSon(openList, node, node.position.x, node.position.y + 1);
            }
            if (node.position.y != 0 && !problemGrid[node.position.x][node.position.y - 1])
            {
                CollectSon(openList, node, node.position.x, node.position.y - 1);
            }
        }

        private void CollectSon(ReducerOpenList<BFSNode> openList, BFSNode node, int x, int y)
        {
            BFSNode son = new BFSNode(new TimedMove(x, y, Move.Direction.NO_DIRECTION, 0), node);
            if (!openList.Contains(son))
                openList.Enqueue(son);
        }

        private class BFSNode
        {
            public TimedMove position;
            public BFSNode lastMove;

            public BFSNode(Move position)
            {
                this.position = new TimedMove(position, 0);
                lastMove = null;
            }

            public BFSNode(TimedMove position, BFSNode lastMove)
            {
                this.position = position;
                this.lastMove = lastMove;
            }

            public override bool Equals(object obj)
            {
                return obj is BFSNode node &&
                       EqualityComparer<Move>.Default.Equals(position, node.position);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(position);
            }
        }
    }
}
