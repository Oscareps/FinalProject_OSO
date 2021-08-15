using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CPF_experiment
{
    public class CFMAM_Run
    {

        /// <summary>
        /// Delimiter character used when writing the results of the runs to the output file.
        /// </summary>
        /// 
        public enum CostFunction { MakeSpan, SOC };

        public static bool toPrint = true;

        public static readonly string RESULTS_DELIMITER = ",";

        public static readonly int SUCCESS_CODE = 1;

        public static readonly int FAILURE_CODE = 0;

        public static readonly bool WRITE_LOG = false;

        /// <summary>
        /// Indicates the starting time in ms for timing the different algorithms.
        /// </summary>
        private double startTime;

        public int solutionCost = -1;

        /// <summary>
        /// This hold an open stream to the results file.
        /// </summary>
        private TextWriter resultsWriter;

        /// <summary>
        /// EH: I introduced this variable so that debugging and experiments
        /// can have deterministic results.
        /// </summary>
        public static Random rand = new Random();



        /// <summary>
        /// Open the results file for output. Currently the file is opened in append mode.
        /// </summary>
        /// <param name="fileName">The name of the results file</param>
        public void OpenResultsFile
        (
            string fileName
        )
        {
            this.resultsWriter = new StreamWriter(fileName, true); // 2nd argument indicates the "append" mode
        }

        /// <summary>
        /// Closes the results file.
        /// </summary>
        public void CloseResultsFile()
        {
            this.resultsWriter.Close();
        }

        /// <summary>
        /// all types of algorithms to be run
        /// </summary>
        List<IMS_ISolver> IMSSolvers;
        List<ICbsSolver> CFMCBSSolvers;

        /// <summary>
        /// all types of heuristics used
        /// </summary>
        public List<MAM_HeuristicCalculator> heuristics; // FIXME: Make unpublic again later

        /// <summary>
        /// Counts the number of times each algorithm went out of time consecutively
        /// </summary>
        public int[] outOfTimeCounters;

        /// <summary>
        /// Construct with chosen algorithms.
        /// </summary>
        public CFMAM_Run(List<IMS_ISolver> IMSSolvers, List<ICbsSolver> CFMCBSSolvers)
        {
            this.watch = Stopwatch.StartNew();


            // Preparing the solvers:
            this.IMSSolvers = IMSSolvers;
            this.CFMCBSSolvers = CFMCBSSolvers;


            outOfTimeCounters = new int[IMSSolvers.Count + CFMCBSSolvers.Count];
            for (int i = 0; i < IMSSolvers.Count; i++)
            {
                outOfTimeCounters[i] = 0;
            }
            for (int i = IMSSolvers.Count; i < outOfTimeCounters.Length; i++)
            {
                outOfTimeCounters[i] = 0;
            }
        }

        /// <summary>
        /// Generates a problem instance, including a board, start and goal locations of desired number of agents
        /// and desired precentage of obstacles
        /// TODO: Refactor to use operators.
        /// </summary>
        /// <param name="gridSize"></param>
        /// <param name="agentsNum"></param>
        /// <param name="obstaclesNum"></param>
        /// <returns></returns>
        public ProblemInstance GenerateProblemInstance
        (
            int gridSize,
            int agentsNum,
            int obstaclesNum
        )
        {
            m_mapFileName = "GRID" + gridSize + "X" + gridSize;
            m_agentNum = agentsNum;

            if (agentsNum + obstaclesNum > gridSize * gridSize)
                throw new Exception("Not enough room for " + agentsNum + ", " + obstaclesNum + " and one empty space in a " + gridSize + "x" + gridSize + "map.");

            int x;
            int y;
            MAM_AgentState[] aStart = new MAM_AgentState[agentsNum];
            bool[][] grid = new bool[gridSize][];
            bool[][] starts = new bool[gridSize][];

            // Generate a random grid
            for (int i = 0; i < gridSize; i++)
            {
                grid[i] = new bool[gridSize];
                starts[i] = new bool[gridSize];
            }
            for (int i = 0; i < obstaclesNum; i++)
            {
                x = rand.Next(gridSize);
                y = rand.Next(gridSize);
                if (grid[x][y]) // Already an obstacle
                    i--;
                grid[x][y] = true;
            }

            // Choose random start locations
            for (int i = 0; i < agentsNum; i++)
            {
                x = rand.Next(gridSize);
                y = rand.Next(gridSize);
                if (starts[x][y] || grid[x][y])
                    i--;
                else
                {
                    starts[x][y] = true;
                    aStart[i] = new MAM_AgentState(x, y, i, 0);
                }
            }

            ProblemInstance problem = new ProblemInstance();
            problem = new ProblemInstance();
            problem.Init(aStart, grid);
            IndependentDetection id = new IndependentDetection(problem, problem.m_vAgents[0].lastMove);
            List<List<TimedMove>> connectionCheck = new List<List<TimedMove>>();
            if (id.bfsToStartPositions(problem.m_vAgents[0].lastMove).Count != problem.m_vAgents.Length)
                problem = GenerateProblemInstance(gridSize, agentsNum, obstaclesNum);
            return problem;
        }

        public string m_mapFileName = "";
        public int m_agentNum = 0;

        /// <summary>
        /// Generates a problem instance based on a DAO map file.
        /// TODO: Fix code dup with GenerateProblemInstance and Import later.
        /// </summary>
        /// <param name="agentsNum"></param>
        /// <returns></returns>
        public ProblemInstance GenerateDragonAgeProblemInstance
        (
            string mapFileName,
            int agentsNum
        )
        {
            m_mapFileName = mapFileName;
            m_agentNum = agentsNum;
            TextReader input = new StreamReader(mapFileName);
            string[] lineParts;
            string line;

            line = input.ReadLine();
            Debug.Assert(line.StartsWith("type octile"));

            // Read grid dimensions
            line = input.ReadLine();
            lineParts = line.Split(' ');
            Debug.Assert(lineParts[0].StartsWith("height"));
            int maxX = int.Parse(lineParts[1]);
            line = input.ReadLine();
            lineParts = line.Split(' ');
            Debug.Assert(lineParts[0].StartsWith("width"));
            int maxY = int.Parse(lineParts[1]);
            line = input.ReadLine();
            Debug.Assert(line.StartsWith("map"));
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

            int x;
            int y;
            MAM_AgentState[] aStart = new MAM_AgentState[agentsNum];
            bool[][] starts = new bool[maxX][];

            for (int i = 0; i < maxX; i++)
                starts[i] = new bool[maxY];

            // Choose random valid unclaimed goal locations
            for (int i = 0; i < agentsNum; i++)
            {
                x = rand.Next(maxX);
                y = rand.Next(maxY);
                if (starts[x][y] || grid[x][y])
                    i--;
                else
                {
                    starts[x][y] = true;
                    aStart[i] = new MAM_AgentState(x, y, i, 0);
                }
            }

            ProblemInstance problem = new ProblemInstance();
            problem.parameters[ProblemInstance.GRID_NAME_KEY] = Path.GetFileNameWithoutExtension(mapFileName);
            problem.Init(aStart, grid);
            IndependentDetection id = new IndependentDetection(problem, problem.m_vAgents[0].lastMove);
            List<List<TimedMove>> connectionCheck = new List<List<TimedMove>>();
            if (id.bfsToStartPositions(problem.m_vAgents[0].lastMove).Count != problem.m_vAgents.Length)
                problem = GenerateDragonAgeProblemInstance(mapFileName, agentsNum);
            return problem;
        }


        public static double planningTime;
        public static double preprocessingTime;
        public static int instanceId = 0;

        /// <summary>
        /// Solve given instance with a list of algorithms 
        /// </summary>
        /// <param name="instance">The instance to solve</param>
        public bool SolveGivenProblem
        (
            ProblemInstance instance
        )
        {
            instanceId += 1;
            bool success = true;

            List<uint> agentList = Enumerable.Range(0, instance.m_vAgents.Length).Select<int, uint>(x => (uint)x).ToList<uint>(); // FIXME: Must the heuristics really receive a list of uints?

            // Solve using the different algorithms
            Debug.WriteLine("Solving " + instance);
            int CFMcost = solveWithIMS(instance);
            int CBScost = solveWithCFMCBS(instance);

            if(CFMcost == CBScost)
            {
                Debug.WriteLine("---------------- CFM Cost = CBS Cost =  " + CBScost+ " -----------------------");
                Debug.WriteLine("");

            }
            else
            {
                if(CFMcost == Constants.TIMEOUT_COST || CBScost == Constants.TIMEOUT_COST)
                {
                    Debug.WriteLine("---------------- TimeOut! CBS cost:   " + CBScost + " CFM cost: " + CFMcost + " -----------------------");
                }
                else
                {
                    throw new Exception("Inconsist Cost");

                }

            }

            return true;
        }

        private int solveWithCFMCBS(ProblemInstance instance)
        {
            int costConsistency = -1;
            MAM_AgentState[] vAgents = new MAM_AgentState[instance.GetNumOfAgents()];
            for (int agentIndex = 0; agentIndex < instance.GetNumOfAgents(); agentIndex++)
                vAgents[agentIndex] = new MAM_AgentState(instance.m_vAgents[agentIndex]);
            for (int i = 0; i < CFMCBSSolvers.Count; i++)
            {
                solutionCost = -1;
                if (outOfTimeCounters[i] < Constants.MAX_FAIL_COUNT)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    this.runCFMCBS(CFMCBSSolvers[i], instance);
                    MAM_AgentState[] vAgents2 = new MAM_AgentState[vAgents.Count()];
                    for (int agentIndex = 0; agentIndex < vAgents.Count(); agentIndex++)
                        vAgents2[agentIndex] = new MAM_AgentState(vAgents[agentIndex]);
                    instance.m_vAgents = vAgents2;

                    solutionCost = CFMCBSSolvers[i].GetSolutionCost();

                    if (costConsistency == -1) costConsistency = solutionCost;
                    else if(solutionCost != costConsistency) throw new Exception("Inconsist Cost");

                    String plan = null;
                    if (CFMCBSSolvers[i].isSolved()) // Solved successfully
                    {
                        plan = CFMCBSSolvers[i].GetPlan();
                        if (toPrint)
                        {
                            Console.WriteLine();
                            Console.WriteLine(plan);
                            Console.WriteLine();
                        }
                        outOfTimeCounters[i] = 0;
                        Console.WriteLine("+SUCCESS+ (:");
                    }
                    else
                    {
                        outOfTimeCounters[i]++;
                        Console.WriteLine("-FAILURE- ):");
                    }
                    planningTime = elapsedTime;
                    WriteGivenCFMCBSProblem(instance, CFMCBSSolvers[i], plan);
                }
                else if (toPrint)
                    PrintNullStatistics(IMSSolvers[i]);
                Console.WriteLine();
            }
            return costConsistency;
        }

        private void WriteGivenCFMCBSProblem(ProblemInstance instance, ICbsSolver solver, string plan)
        {
            writeToFile(
                solver.GetName(),                       // solver name
                planningTime.ToString(),                // planning time 
                "MakeSpan",                                  // cost function
                solver.GetSolutionCost().ToString(),    // solution  cost
                instanceId.ToString(),                  // instanceId  
                instance.fileName,                      // file Name
                instance.m_vAgents.Length.ToString(),   // #Agents
                m_mapFileName,                          // Map name
                solver.isSolved().ToString(),           // Success
                instance.m_nObstacles,                  // Obstacles
                solver.GetExpanded().ToString(),        // Expansions 
                solver.GetGenerated().ToString(),       // Generates
                preprocessingTime.ToString());            // preprocessing time
        }

        private void writeToFile
        (
            string solver,
            string planTime,
            string costFunction,
            string planCost,
            string instanceId,
            string instanceName,
            string agentsCount,
            string mapFileName,
            string success,
            uint obstaclesPercents,
            string expandedNodes,
            string generatedNodes,
            string preprocessingTime
        )
        {
            {
                string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = pathDesktop + "\\MMS_CSV.csv";
                int length;
                string delimter = ",";
                List<string[]> output = new List<string[]>();
                string[] temps = new string[16];


                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();

                    temps[0] = "IncstanceId";
                    temps[1] = "#Agents";
                    temps[2] = "Map";
                    temps[3] = "Obstacles";
                    temps[4] = "File";
                    temps[5] = "Cost Function";
                    temps[6] = "Solver";
                    //temps[7] = "Heuristic";
                    temps[7] = "Success";
                    temps[8] = "Plan Cost";
                    temps[9] = "Plan time";
                    temps[10] = "Expansions";
                    temps[11] = "Generations";
                    temps[12] = "preprocessingTime";

                    output.Add(temps);

                    length = output.Count;
                    using (System.IO.TextWriter writer = File.AppendText(filePath))
                    {
                        for (int index = 0; index < length; index++)
                        {
                            writer.WriteLine(string.Join(delimter, output[index]));
                        }
                    }
                }
                output = new List<string[]>();
                string new_file_name = instanceName;
                string[] splitFilePath = new_file_name.Split('\\');
                new_file_name = splitFilePath[splitFilePath.Count() - 1];
                string map = (new_file_name.Split('.'))[0];
                //string instance = (new_file_name.Split('-'))[1];
                string instance = new_file_name + "CBSMMStar";

                temps[0] = instanceId;
                temps[1] = agentsCount;
                temps[2] = mapFileName;
                temps[3] = obstaclesPercents.ToString();
                temps[4] = new_file_name;
                temps[5] = costFunction;
                temps[6] = solver;
                temps[7] = success;
                temps[8] = planCost;
                temps[9] = planTime;
                temps[10] = expandedNodes;
                temps[11] = generatedNodes;
                temps[12] = preprocessingTime;
                output.Add(temps);

                length = output.Count;
                using (System.IO.TextWriter writer = File.AppendText(filePath))
                {
                    for (int index = 0; index < length; index++)
                    {
                        writer.WriteLine(string.Join(delimter, output[index]));
                    }
                }
            }
        }

        private void runCFMCBS(ICbsSolver solver, ProblemInstance instance)
        {
            // Run the algorithm
            bool solved;
            Console.WriteLine("----------------- " + solver + ", Minimizing MakeSpan -----------------");
            Constants.ALLOW_WAIT_MOVE = true;
            this.startTime = this.ElapsedMillisecondsTotal();
            solver.Setup(instance, new MAM_Run()); // Defining MMStar as the low level of the algorithm
            solved = solver.Solve();
            elapsedTime = this.ElapsedMilliseconds();
            if (solved)
            {
                Console.WriteLine("Total MakeSpan cost: {0}", solver.GetSolutionCost());
            }
            else
            {
                Console.WriteLine("Failed to solve CBS");
            }
            Console.WriteLine();
            Console.WriteLine("Expanded nodes: {0}", solver.GetExpanded());
            Console.WriteLine("Time in milliseconds: {0}", elapsedTime);
            if (toPrint)
                this.PrintStatistics(instance, elapsedTime, null, solver);
        }

        public int solveWithIMS(ProblemInstance instance)
        {
            int costConsistency = -1;
            MAM_AgentState[] vAgents = new MAM_AgentState[instance.GetNumOfAgents()];
            for (int agentIndex = 0; agentIndex < instance.GetNumOfAgents(); agentIndex++)
                vAgents[agentIndex] = new MAM_AgentState(instance.m_vAgents[agentIndex]);
            for (int i = 0; i < IMSSolvers.Count; i++)
            {
                solutionCost = -1;
                if (outOfTimeCounters[i] < Constants.MAX_FAIL_COUNT)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    preprocessingTime = 0;
                    if (IMSSolvers[i].GetHeuristicCalculator().GetName() == "FastMap H")
                    {
                        this.startTime = this.ElapsedMillisecondsTotal();
                        IMSSolvers[i].GetHeuristicCalculator().init(instance);
                        IMSSolvers[i].GetHeuristicCalculator().preprocessing();
                        preprocessingTime = this.ElapsedMilliseconds();
                        Console.WriteLine("Preprocessing time in milliseconds: {0}", preprocessingTime);
                    }

                    this.runIMS(IMSSolvers[i], instance);
                    MAM_AgentState[] vAgents2 = new MAM_AgentState[vAgents.Count()];
                    for (int agentIndex = 0; agentIndex < vAgents.Count(); agentIndex++)
                        vAgents2[agentIndex] = new MAM_AgentState(vAgents[agentIndex]);
                    instance.m_vAgents = vAgents2;

                    solutionCost = IMSSolvers[i].GetSolutionCost();

                    if (costConsistency == -1) costConsistency = solutionCost;
                    else if (solutionCost != costConsistency) throw new Exception("Inconsist Cost");

                    String plan = null;
                    if (IMSSolvers[i].IsSolved()) // Solved successfully
                    {
                        plan = IMSSolvers[i].GetPlan();
                        if (toPrint)
                        {
                            Console.WriteLine();
                            Console.WriteLine(plan);
                            Console.WriteLine();
                        }
                        outOfTimeCounters[i] = 0;
                        Console.WriteLine("+SUCCESS+ (:");
                    }
                    else
                    {
                        outOfTimeCounters[i]++;
                        Console.WriteLine("-FAILURE- ):");
                    }
                    planningTime = elapsedTime;
                    WriteGivenIMSProblem(instance, IMSSolvers[i], plan);
                }
                else if (toPrint)
                    PrintNullStatistics(IMSSolvers[i]);
                Console.WriteLine();
            }
            return costConsistency;
        }

        public double elapsedTime;

        /// <summary>
        /// Solve a given instance with the given solver
        /// </summary>
        /// <param name="solver">The solver</param>
        /// <param name="instance">The problem instance that will be solved</param>
        private void runIMS
        (
            IMS_ISolver solver,
            ProblemInstance instance
        )
        {
            // Run the algorithm
            bool solved;
            Console.WriteLine("----------------- " + solver + " with " + solver.GetHeuristicCalculator().GetName() + ", Minimizing " + solver.GetCostFunction().ToString() + " -----------------");
            Constants.ALLOW_WAIT_MOVE = false;
            this.startTime = this.ElapsedMillisecondsTotal();
            solver.GetHeuristicCalculator().init(instance);
            solver.Setup(instance, this);
            solved = solver.Solve();
            elapsedTime = this.ElapsedMilliseconds();
            if (solved)
            {
                Console.WriteLine();
                Console.WriteLine("Total {0} cost: {1}",solver.GetCostFunction(), solver.GetSolutionCost());
            }
            else
            {
                Console.WriteLine("Failed to solve");
            }
            Console.WriteLine();
            Console.WriteLine("Expanded nodes: {0}", solver.GetExpanded());
            Console.WriteLine("Time in milliseconds: {0}", elapsedTime);
            if (toPrint)
                this.PrintStatistics(instance, elapsedTime, solver);
        }

        /// <summary>
        /// Print the solver statistics to the results file.
        /// </summary>
        /// <param name="instance">The problem instance that was solved. Not used!</param>
        /// <param name="CFMAMSolver">The solver that solved the problem instance</param>
        /// <param name="runtimeInMillis">The time it took the given solver to solve the given instance</param>
        private void PrintStatistics
        (
            ProblemInstance instance,
            double runtimeInMillis,
            IMS_ISolver CFMAMSolver,
            ICbsSolver CBSMMStarSolver = null
        )
        {
            // Success col:
            //if (solver.GetSolutionCost() < 0)
            //  this.resultsWriter.Write(Run.FAILURE_CODE + RESULTS_DELIMITER);
            //else
            this.resultsWriter.Write(MAM_Run.SUCCESS_CODE + RESULTS_DELIMITER);
            // Runtime col:
            this.resultsWriter.Write(runtimeInMillis + RESULTS_DELIMITER);
            // Solution Cost col:
            //this.resultsWriter.Write(solver.GetSolutionCost() + RESULTS_DELIMITER);
            if (CBSMMStarSolver != null)
            {
                CBSMMStarSolver.OutputStatistics(this.resultsWriter);
                this.resultsWriter.Write(CBSMMStarSolver.GetSolutionDepth() + RESULTS_DELIMITER);
            }
            else
            {
                // Algorithm specific cols:
                CFMAMSolver.OutputStatistics(this.resultsWriter);
                // Solution Depth col:
                this.resultsWriter.Write(CFMAMSolver.GetSolutionDepth() + RESULTS_DELIMITER);
            }
            //this.resultsWriter.Flush();
        }

        private void PrintNullStatistics
        (
            IMS_ISolver solver
        )
        {
            // Success col:
            this.resultsWriter.Write(MAM_Run.FAILURE_CODE + RESULTS_DELIMITER);
            // Runtime col:
            this.resultsWriter.Write(Constants.MAX_TIME + RESULTS_DELIMITER);
            // Solution Cost col:
            this.resultsWriter.Write("irrelevant" + RESULTS_DELIMITER);
            // Max Group col:
            this.resultsWriter.Write("irrelevant" + RESULTS_DELIMITER);
            // Solution Depth col:
            this.resultsWriter.Write("irrelevant" + RESULTS_DELIMITER);
        }

        private Stopwatch watch;
        private double ElapsedMillisecondsTotal()
        {
            return this.watch.Elapsed.TotalMilliseconds;
        }

        public double ElapsedMilliseconds()
        {
            return ElapsedMillisecondsTotal() - this.startTime;
        }


        /// <summary>
        /// Write to file a given instance 
        /// </summary>
        /// <param name="instance">The instance to execute</param>
        public void WriteGivenIMSProblem
        (
            ProblemInstance instance,
            IMS_ISolver solver,
            String currentPlan = null)
        {
            string initialH = 0.ToString();
            if (solver.GetCostFunction() == CostFunction.SOC)
            {
                Tuple<double, int> bestInitH = solver.GetHeuristicCalculatorInitialH();
                double bestH = bestInitH.Item1;
                double bestAgents = bestInitH.Item2;
                initialH = bestH.ToString();

            }

            writeToFile(
                solver.GetName(),                       // solver name
                planningTime.ToString(),                // planning time 
                costFunctionToString(solver.GetCostFunction()), // cost function
                solver.GetSolutionCost().ToString(),    // solution  cost
                instanceId.ToString(),                  // instanceId  
                instance.fileName,                      // file Name
                instance.m_vAgents.Length.ToString(),   // #Agents
                m_mapFileName,                          // Map name
                solver.IsSolved().ToString(),           // Success
                instance.m_nObstacles,                  // Obstacles
                solver.GetExpanded().ToString(),        // Expansions 
                solver.GetGenerated().ToString(),       // Generates
                preprocessingTime.ToString()           // preprocessing time
                //solver.GetHeuristicCalculator().GetName() // Heuristic Name
                //initialH // Initial h value
                ); 
        }

        private string costFunctionToString(CostFunction costFunction)
        {
            if (costFunction == CostFunction.MakeSpan)
                return "MakeSpan";
            else if (costFunction == CostFunction.SOC)
                return "SOC";
            return "NULL";
        }

        public void PrintResultsFileHeader()
        {
            this.resultsWriter.Write("Grid Name");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            this.resultsWriter.Write("Grid Rows");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            this.resultsWriter.Write("Grid Columns");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            this.resultsWriter.Write("Num Of Agents");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            this.resultsWriter.Write("Num Of Obstacles");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            this.resultsWriter.Write("Instance Id");
            this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);

            for (int i = 0; i < IMSSolvers.Count; i++)
            {
                var solver = IMSSolvers[i];
                this.resultsWriter.Write(solver + " Success");
                this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
                this.resultsWriter.Write(solver + " Runtime");
                this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
                this.resultsWriter.Write(solver + " Solution Cost");
                this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
                solver.OutputStatisticsHeader(this.resultsWriter);
                this.resultsWriter.Write(solver + " Max Group");
                this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
                this.resultsWriter.Write(solver + " Solution Depth");
                this.resultsWriter.Write(MAM_Run.RESULTS_DELIMITER);
            }
            this.ContinueToNextLine();
        }

        private void ContinueToNextLine()
        {
            this.resultsWriter.WriteLine();
            this.resultsWriter.Flush();
        }

        public void ResetOutOfTimeCounters()
        {
            for (int i = 0; i < outOfTimeCounters.Length; i++)
            {
                outOfTimeCounters[i] = 0;
            }
        }

        /// <summary>
        /// write execution info to file
        /// </summary>
        public void writeToFile
        (
            string solver,
            string planTime,
            string costFunction,
            string planSOCCost,
            string planMakeSpanCost,
            string instanceId,
            string instanceName,
            string agentsCount,
            string mapFileName,
            string success,
            uint obstaclesPercents,
            string expandedNodes,
            string generatedNodes,
            string preprocessingTime,
            string heuristicName,
            string initialH
        )
        {
            string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = pathDesktop + "\\MMS_CSV.csv";
            int length;
            string delimter = ",";
            List<string[]> output = new List<string[]>();
            string[] temps = new string[16];


            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();

                temps[0] = "IncstanceId";
                temps[1] = "#Agents";
                temps[2] = "Map";
                temps[3] = "Obstacles";
                temps[4] = "File";
                temps[5] = "Cost Function";
                temps[6] = "Solver";
                temps[7] = "Heuristic";
                temps[8] = "Success";
                temps[9] = "Plan SOC";
                temps[10] = "Plan MakeSpan";
                temps[11] = "Plan time";
                temps[12] = "Expansions";
                temps[13] = "Generations";
                temps[14] = "Preprocessing time";
                temps[15] = "Initial h value";

                output.Add(temps);

                length = output.Count;
                using (System.IO.TextWriter writer = File.AppendText(filePath))
                {
                    for (int index = 0; index < length; index++)
                    {
                        writer.WriteLine(string.Join(delimter, output[index]));
                    }
                }
            }
            output = new List<string[]>();
            string new_file_name = instanceName;
            string[] splitFilePath = new_file_name.Split('\\');
            new_file_name = splitFilePath[splitFilePath.Count() - 1];
            string map = (new_file_name.Split('.'))[0];
            //string instance = (new_file_name.Split('-'))[1];
            string instance = new_file_name;

            temps[0] = instanceId;
            temps[1] = agentsCount;
            temps[2] = mapFileName;
            temps[3] = obstaclesPercents.ToString();
            temps[4] = new_file_name;
            temps[5] = costFunction;
            temps[6] = solver;
            temps[7] = heuristicName;
            temps[8] = success;
            temps[9] = planSOCCost;
            temps[10] = planMakeSpanCost;
            temps[11] = planTime;
            temps[12] = expandedNodes;
            temps[13] = generatedNodes;
            temps[14] = preprocessingTime;
            temps[15] = initialH;
            output.Add(temps);

            length = output.Count;
            using (System.IO.TextWriter writer = File.AppendText(filePath))
            {
                for (int index = 0; index < length; index++)
                {
                    writer.WriteLine(string.Join(delimter, output[index]));
                }
            }
        }
    }
}
