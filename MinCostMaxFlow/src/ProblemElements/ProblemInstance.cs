using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace CPF_experiment
{
    /// <summary>
    /// This class represents a cooperative pathfinding problem instance. This includes:
    /// - The grid in which the agents are located
    /// - An array of initial state for every agent.
    /// </summary>
    public class ProblemInstance
    {
        public enum CostFunction { Makespan, SOC };
        public CostFunction costFunction = CostFunction.SOC;
        /// <summary>
        /// Delimiter used for export/import purposes
        /// </summary>
        private static readonly char EXPORT_DELIMITER = ',';

        public static readonly string GRID_NAME_KEY = "Grid Name";

        /// <summary>
        /// This contains extra data of this problem instance (used for special problem instances, e.g. subproblems of a bigger problem instance).
        /// </summary>
        public IDictionary<String, Object> parameters;

        public bool[][] m_vGrid;

        /// <summary>
        /// We keep a reference to the array of agents in the original problem.
        /// This will only change when IndependenceDetection's algorithm determines in another
        /// iteration that a new set of agents must be jointly planned due
        /// to their mutual conflicts.
        /// </summary>
        public MAM_AgentState[] m_vAgents;
        private Dictionary<int, int> agentsDictionary; // from agent index to its index in m_Agents
        public uint m_nObstacles;
        public uint m_nLocations;

        /// <summary>
        /// This field is used to identify an instance when running a set of experiments
        /// </summary>
        public int instanceId;

        public string fileName;

        private int nOfObstacles;
        private int nOfLocations;


        public ProblemInstance
        (
            IDictionary<String, Object> parameters = null
        )
        {
            if (parameters != null)
                this.parameters = parameters;
            else
                this.parameters = new Dictionary<String, Object>();
        }


        /// <summary>
        /// Initialize the members of this object, such that the given agent states are the start state of this instance.
        /// </summary>
        /// <param name="agentStartStates"></param>
        /// <param name="grid"></param>
        public void Init
        (
            MAM_AgentState[] agentStartStates,
            bool[][] grid,
            int nObstacles = -1,
            int nLocations = -1
        )
        {
            m_vAgents = agentStartStates;
            m_vGrid = grid;
            agentsDictionary = new Dictionary<int, int>();
            this.nOfObstacles = nObstacles;
            this.nOfLocations = nLocations;
            for (int index = 0; index < m_vAgents.Length; index++)
                agentsDictionary.Add(m_vAgents[index].agentIndex, index);
            if (nObstacles == -1)
                m_nObstacles = (uint)grid.Sum(row => row.Count(x => x));
            else
                m_nObstacles = (uint)nObstacles;

            if (nLocations == -1)
                m_nLocations = ((uint)(grid.Length * grid[0].Length)) - m_nObstacles;
            else
                m_nLocations = (uint)nLocations;
        }

        public ProblemInstance ReplanProblem(MAM_AgentState[] newStartStates)
        {
            ProblemInstance instanceCopy = new ProblemInstance(this.parameters);
            instanceCopy.Init(newStartStates, this.m_vGrid, this.nOfObstacles, this.nOfLocations);
            return instanceCopy;
        }

        public void SetCostFuncion
        (
            CostFunction costFunction
        )
        {
            this.costFunction = costFunction;
        }

        /// <summary>
        /// Utility function that returns the number of agents in this problem instance.
        /// </summary>
        public int GetNumOfAgents()
        {
            return m_vAgents.Length;
        }

        /// <summary>
        /// Utility function that returns the x dimension of the grid
        /// </summary>
        public int GetMaxX()
        {
            return this.m_vGrid.GetLength(0);
        }

        /// <summary>
        /// Utility function that returns the y dimension of the grid
        /// </summary>
        public int GetMaxY()
        {
            return this.m_vGrid[0].Length;
        }


        /// <summary>
        /// Imports a problem instance from a given file
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static ProblemInstance Import
        (
            string fileName
        )
        {
            TextReader input = new StreamReader(fileName);
            string[] lineParts;
            string line;
            int instanceId = 0;
            string gridName = "Random Grid"; // The default

            line = input.ReadLine();
            if (line.StartsWith("Grid:") == false)
            {
                lineParts = line.Split(',');
                instanceId = int.Parse(lineParts[0]);
                if (lineParts.Length > 1)
                    gridName = lineParts[1];
                line = input.ReadLine();
            }
            Debug.Assert(line.StartsWith("Grid:"));

            // Read grid dimensions
            line = input.ReadLine();
            lineParts = line.Split(',');
            int maxX = int.Parse(lineParts[0]);
            int maxY = int.Parse(lineParts[1]);
            bool[][] grid = new bool[maxX][];
            char cell;
            for (int i = 0; i < maxX; i++)
            {
                grid[i] = new bool[maxY];
                line = input.ReadLine();
                for (int j = 0; j < maxY; j++)
                {
                    cell = line.ElementAt(j);
                    if (cell == '@' || cell == 'O' || cell == 'T' || cell == 'W' /* Water isn't traversable from land */)
                        grid[i][j] = true;
                    else
                        grid[i][j] = false;
                }
            }

            // Next line is Agents:
            line = input.ReadLine();
            Debug.Assert(line.StartsWith("Agents:"));

            // Read the number of agents
            line = input.ReadLine();
            int numOfAgents = int.Parse(line);

            // Read the agents' start and goal states
            MAM_AgentState[] states = new MAM_AgentState[numOfAgents];
            MAM_AgentState state;
            int agentNum;
            int startX;
            int startY;
            for (int agentIndex = 0; agentIndex < numOfAgents; agentIndex++)
            {
                line = input.ReadLine();
                lineParts = line.Split(EXPORT_DELIMITER);
                agentNum = int.Parse(lineParts[0]);
                startX = int.Parse(lineParts[1]);
                startY = int.Parse(lineParts[2]);
                state = new MAM_AgentState(startX, startY, agentIndex,0);
                states[agentIndex] = state;
            }

            // Generate the problem instance
            ProblemInstance instance = new ProblemInstance();
            instance.fileName = fileName;
            instance.instanceId = instanceId;
            instance.Init(states, grid);
            instance.parameters[ProblemInstance.GRID_NAME_KEY] = gridName;
            return instance;
        }

        /// <summary>
        /// Exports a problem instance to a file
        /// </summary>
        /// <param name="fileName"></param>
        public void Export
        (
            string fileName
        )
        {
            String[] pathElements = { Directory.GetCurrentDirectory(), "MAM_Instances", fileName };
            TextWriter output = new StreamWriter(Path.Combine(pathElements));
            // Output the instance ID
            if (this.parameters.ContainsKey(ProblemInstance.GRID_NAME_KEY))
                output.WriteLine(this.instanceId.ToString() + "," + this.parameters[ProblemInstance.GRID_NAME_KEY]);
            else
                output.WriteLine(this.instanceId);

            // Output the grid
            output.WriteLine("Grid:");
            output.WriteLine(this.m_vGrid.GetLength(0) + "," + this.m_vGrid[0].GetLength(0));

            for (int i = 0; i < this.m_vGrid.GetLength(0); i++)
            {
                for (int j = 0; j < this.m_vGrid[0].GetLength(0); j++)
                {
                    if (this.m_vGrid[i][j] == true)
                        output.Write('@');
                    else
                        output.Write('.');

                }
                output.WriteLine();
            }
            // Output the agents state
            output.WriteLine("Agents:");
            output.WriteLine(this.m_vAgents.Length);
            MAM_AgentState state;
            for (int agentIndex = 0; agentIndex < this.m_vAgents.Length; agentIndex++)
            {
                state = this.m_vAgents[agentIndex];
                output.Write(agentIndex);
                output.Write(EXPORT_DELIMITER);
                output.Write(state.lastMove.x);
                output.Write(EXPORT_DELIMITER);
                output.Write(state.lastMove.y);
                output.WriteLine();
            }
            output.Flush();
            output.Close();
        }



        /// <summary>
        /// Check if the tile is valid, i.e. in the grid and without an obstacle.
        /// NOT checking the direction. A Move could be declared valid even if it came to an edge tile from outside the grid!
        /// NOT checking if the move is illegal
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>True if the given location is a valid grid location with no obstacles</returns>
        public bool IsValid
        (
            Move aMove
        )
        {
            return IsValidTile(aMove.x, aMove.y);
        }


        public bool IsValidTile
        (
            int x,
            int y
        )
        {
            if (x < 0 || x >= GetMaxX())
                return false;
            if (y < 0 || y >= GetMaxY())
                return false;
            return !m_vGrid[x][y];
        }

        public override string ToString()
        {
            string str = "Problem instance:" + instanceId;
            if (this.parameters.ContainsKey(ProblemInstance.GRID_NAME_KEY))
                str += " Grid Name:" + this.parameters[ProblemInstance.GRID_NAME_KEY];
            str += " #Agents:" + m_vAgents.Length + ", GridCells:" + m_nLocations + ", #Obstacles:" + m_nObstacles;
            return str;
        }

        public ProblemInstance CreateSubProblem
        (
            MAM_AgentState[] agentStartStates
        )
        {
            ProblemInstance newProblemInstance = new ProblemInstance();
            newProblemInstance.Init(agentStartStates, this.m_vGrid, (int)this.m_nObstacles, (int)this.m_nLocations);
            return newProblemInstance;
        }

        public int GetAgentIndexInArray
        (
            int agentIndex
        )
        {
            return agentsDictionary[agentIndex];
        }
    }
}
