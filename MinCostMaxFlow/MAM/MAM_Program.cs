using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using Google.OrTools.Graph;

namespace CPF_experiment
{
    /// <summary>
    /// This is the entry point of the application. 
    /// </summary>
    public class MAM_Program
    {

        private static string RESULTS_FILE_NAME = "Results.csv"; // Overridden by Main
        private static bool onlyReadInstances = false;

        /// <summary>
        /// Simplest run possible with a randomly generated problem instance.
        /// </summary>
        public void SimpleRun()
        {
            MAM_Run runner = new MAM_Run();
            runner.OpenResultsFile(RESULTS_FILE_NAME);
            runner.PrintResultsFileHeader();
            MAM_ProblemInstance instance = runner.GenerateProblemInstance(10, 3, 10);
            instance.Export("Test.instance");
            runner.SolveGivenProblem(instance);
            runner.CloseResultsFile();
        }

        /// <summary>
        /// Runs a single instance, imported from a given filename.
        /// </summary>
        /// <param name="fileName"></param>
        public bool RunInstance(string fileName)
        {
            MAM_ProblemInstance instance;
            try
            {
                instance = MAM_ProblemInstance.Import(Directory.GetCurrentDirectory() + "\\MAM_Instances\\" + fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Skipping bad problem instance {0}. Error: {1}", fileName, e.Message));
                return false;
            }

            // --- test reducer ---

            Move goalstate = new Move(2, 2);
            CFMAM_MCMF_Reducer reducer = new CFMAM_MCMF_Reducer(instance, goalstate);
            reducer.reduce();
            MinCostMaxFlow mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
            Stopwatch timer = Stopwatch.StartNew();
            MinCostFlow solution = mcmfSolver.SolveMinCostFlow();
            timer.Stop();
            if (solution != null)
            {
                reducer.GetCFMAMSolution(solution, timer.ElapsedMilliseconds);
                return true;
            }
            return false;

            // --- test reducer ---
        }

        /// <summary>
        /// Runs a set of experiments.
        /// This function will generate a random instance (or load it from a file if it was already generated)
        /// </summary>
        public void RunExperimentSet(int[] gridSizes, int[] agentListSizes, int[] obstaclesProbs, int instances)
        {

            MAM_ProblemInstance instance;
            string instanceName;
            MAM_Run runner = new MAM_Run();

            bool resultsFileExisted = File.Exists(RESULTS_FILE_NAME);
            runner.OpenResultsFile(RESULTS_FILE_NAME);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();

            bool continueFromLastRun = false;
            string[] LastProblemDetails = null;
            string currentProblemFileName = Directory.GetCurrentDirectory() + "\\MAM_Instances\\current problem-" + Process.GetCurrentProcess().ProcessName;
            if (File.Exists(currentProblemFileName)) //if we're continuing running from last time
            {
                var lastProblemFile = new StreamReader(currentProblemFileName);
                LastProblemDetails = lastProblemFile.ReadLine().Split(',');  //get the last problem
                lastProblemFile.Close();
                continueFromLastRun = true;
            }

            for (int gs = 0; gs < gridSizes.Length; gs++)
            {
                for (int obs = 0; obs < obstaclesProbs.Length; obs++)
                {
                    runner.ResetOutOfTimeCounters();
                    for (int ag = 0; ag < agentListSizes.Length; ag++)
                    {
                        if (gridSizes[gs] * gridSizes[gs] * (1 - obstaclesProbs[obs] / 100) < agentListSizes[ag]) // Probably not enough room for all agents
                            continue;
                        for (int i = 0; i < instances; i++)
                        {
                            string allocation = Process.GetCurrentProcess().ProcessName.Substring(1);

                            if (continueFromLastRun)  //set the latest problem
                            {
                                gs = int.Parse(LastProblemDetails[0]);
                                obs = int.Parse(LastProblemDetails[1]);
                                ag = int.Parse(LastProblemDetails[2]);
                                i = int.Parse(LastProblemDetails[3]);
                                for (int j = 4; j < LastProblemDetails.Length; j++)
                                {
                                    runner.outOfTimeCounters[j - 4] = int.Parse(LastProblemDetails[j]);
                                }
                                continueFromLastRun = false;
                                continue; // "current problem" file describes last solved problem, no need to solve it again
                            }
                            if (runner.outOfTimeCounters.Length != 0 &&
                                runner.outOfTimeCounters.Sum() == runner.outOfTimeCounters.Length * Constants.MAX_FAIL_COUNT) // All algs should be skipped
                                break;
                            instanceName = "Instance-" + gridSizes[gs] + "-" + obstaclesProbs[obs] + "-" + agentListSizes[ag] + "-" + i;
                            try
                            {
                                instance = MAM_ProblemInstance.Import(Directory.GetCurrentDirectory() + "\\MAM_Instances\\" + instanceName);
                                instance.instanceId = i;
                            }
                            catch (Exception importException)
                            {
                                if (onlyReadInstances)
                                {
                                    Console.WriteLine("File " + instanceName + "  dosen't exist");
                                    return;
                                }

                                instance = runner.GenerateProblemInstance(gridSizes[gs], agentListSizes[ag], obstaclesProbs[obs] * gridSizes[gs] * gridSizes[gs] / 100);
                                instance.instanceId = i;
                                instance.Export(instanceName);
                            }
                            instance.fileName = instanceName;
                            //runner.SolveGivenProblem(instance);
                            // --- test reducer ---

                            Move goalState = FixProblemInstance(instance);
                            CFMAM_MCMF_Reducer reducer = new CFMAM_MCMF_Reducer(instance, goalState);
                            reducer.reduce();
                            MinCostMaxFlow mcmfSolver = new MinCostMaxFlow(reducer.outputProblem);
                            Stopwatch timer = Stopwatch.StartNew();
                            MinCostFlow solution = mcmfSolver.SolveMinCostFlow();
                            timer.Stop();
                            if (solution != null)
                            {
                                reducer.GetCFMAMSolution(solution, timer.ElapsedMilliseconds);
                            }
                        }
                    }
                }
            }
            runner.CloseResultsFile();
        }

        private Move FixProblemInstance(MAM_ProblemInstance instance)
        {
            Random rand = new Random();
            HashSet<KeyValuePair<int, int>> startPositions = new HashSet<KeyValuePair<int, int>>();
            int goalState_y;
            int goalState_x;
            do
            {
                goalState_y = rand.Next(instance.m_vGrid.Length);
                goalState_x = rand.Next(instance.m_vGrid[0].Length);
            }
            while (instance.m_vGrid[goalState_y][goalState_x]);

            Move goalState = new Move(goalState_x, goalState_y);

            foreach (MAM_AgentState state in instance.m_vAgents)
            {
                while (instance.m_vGrid[state.lastMove.y][state.lastMove.x] || startPositions.Contains(new KeyValuePair<int, int>(state.lastMove.x, state.lastMove.y)))
                {
                    state.lastMove.y = rand.Next(instance.m_vGrid.Length);
                    state.lastMove.x = rand.Next(instance.m_vGrid[0].Length);
                }
                startPositions.Add(new KeyValuePair<int, int>(state.lastMove.x, state.lastMove.y));
            }

            return goalState;
        }

        //protected static readonly string[] daoMapFilenames = {/* "dao_maps\\den502d.map", "dao_maps\\ost003d.map", */"dao_maps\\brc202d.map" ,dao_maps\\kiva.map};

        protected static readonly string[] daoMapFilenames = { "dao_maps\\Enigma.map" };

        /*protected static readonly string[] daoMapFilenames = {  "dao_maps\\Berlin_0_256.map",
                                                                "dao_maps\\Berlin_0_512.map",
                                                                "dao_maps\\Berlin_0_1024.map",
                                                                "dao_maps\\Berlin_1_256.map",
                                                                "dao_maps\\Berlin_1_512.map",
                                                                "dao_maps\\Berlin_1_1024.map",
                                                                "dao_maps\\Boston_0_256.map",
                                                                "dao_maps\\Boston_0_512.map",
                                                                "dao_maps\\Boston_0_1024.map", };*/

        protected static readonly string[] mazeMapFilenames = { "mazes-width1-maps\\maze512-1-6.map", "mazes-width1-maps\\maze512-1-2.map",
                                                "mazes-width1-maps\\maze512-1-9.map" };


        public static Stopwatch sw = new Stopwatch();
        /// <summary>
        /// Dragon Age experiment
        /// </summary>
        /// <param name="numInstances"></param>
        /// <param name="mapFileNames"></param>
        public void RunDragonAgeExperimentSet(int numInstances, string[] mapFileNames)
        {

            MAM_ProblemInstance instance;
            string instanceName;
            MAM_Run runner = new MAM_Run();

            bool resultsFileExisted = File.Exists(RESULTS_FILE_NAME);
            runner.OpenResultsFile(RESULTS_FILE_NAME);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();

            TextWriter output;
            int[] agentListSizes = { 5 };

            bool continueFromLastRun = false;
            string[] lineParts = null;

            string currentProblemFileName = Directory.GetCurrentDirectory() + "\\MAM_Instances\\current problem-" + Process.GetCurrentProcess().ProcessName;
            if (File.Exists(currentProblemFileName)) //if we're continuing running from last time
            {
                TextReader input = new StreamReader(currentProblemFileName);
                lineParts = input.ReadLine().Split(',');  //get the last problem
                input.Close();
                continueFromLastRun = true;
            }

            for (int ag = 0; ag < agentListSizes.Length; ag++)
            {
                for (int i = 0; i < numInstances; i++)
                {
                    string name = Process.GetCurrentProcess().ProcessName.Substring(1);


                    for (int map = 0; map < mapFileNames.Length; map++)
                    {
                        if (continueFromLastRun) // Set the latest problem
                        {
                            ag = int.Parse(lineParts[0]);
                            i = int.Parse(lineParts[1]);
                            map = int.Parse(lineParts[2]);
                            for (int j = 3; j < lineParts.Length && j - 3 < runner.outOfTimeCounters.Length; j++)
                            {
                                runner.outOfTimeCounters[j - 3] = int.Parse(lineParts[j]);
                            }
                            continueFromLastRun = false;
                            continue;
                        }
                        if (runner.outOfTimeCounters.Sum() == runner.outOfTimeCounters.Length * 20) // All algs should be skipped
                            break;
                        string mapFileName = mapFileNames[map];
                        instanceName = Path.GetFileNameWithoutExtension(mapFileName) + "-" + agentListSizes[ag] + "-" + i;
                        try
                        {
                            instance = MAM_ProblemInstance.Import(Directory.GetCurrentDirectory() + "\\MAM_Instances\\" + instanceName);
                        }
                        catch (Exception importException)
                        {
                            if (onlyReadInstances)
                            {
                                Console.WriteLine("File " + instanceName + "  dosen't exist");
                                return;
                            }

                            instance = runner.GenerateDragonAgeProblemInstance(mapFileName, agentListSizes[ag]);
                            instance.instanceId = i;
                            instance.Export(instanceName);
                            instance.fileName = instanceName;
                        }

                        runner.SolveGivenProblem(instance);

                        //save the latest problem
                        try
                        {
                            File.Delete(currentProblemFileName);
                        }
                        catch
                        {
                            ;
                        }
                        output = new StreamWriter(currentProblemFileName);
                        output.Write("{0},{1},{2}", ag, i, map);
                        for (int j = 0; j < runner.outOfTimeCounters.Length; j++)
                        {
                            output.Write("," + runner.outOfTimeCounters[j]);
                        }
                        output.Close();

                    }
                }
                runner.CloseResultsFile();
            }
        }


        /// <summary>
        /// This is the starting point of the program. 
        /// </summary>
        public static void Main(string[] args)
        {
            MAM_Program me = new MAM_Program();
            MAM_Program.RESULTS_FILE_NAME = Process.GetCurrentProcess().ProcessName + ".csv";
            TextWriterTraceListener tr1 = new TextWriterTraceListener(System.Console.Out);
            Trace.Listeners.Add(tr1);


            if (Directory.Exists(Directory.GetCurrentDirectory() + "\\Instances") == false)
            {
                Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Instances");
            }

            MAM_Program.onlyReadInstances = false;

            int instances = 10;

            bool runDragonAge = false;
            bool runGrids = true;
            bool runMazesWidth1 = false;
            bool runSpecific = false;

            if (runGrids == true)
            {
                int[] gridSizes = new int[] { 100 };     // Map size 8x8, 16x16 ...

                int[] agentListSizes = new int[] { 10 };  // Number of agents


                int[] obstaclesPercents = new int[] { 50 };   // Randomly allocatade obstacles percents
                me.RunExperimentSet(gridSizes, agentListSizes, obstaclesPercents, instances);
            }
            else if (runDragonAge == true)
                me.RunDragonAgeExperimentSet(instances, MAM_Program.daoMapFilenames); // Obstacle percents and grid sizes built-in to the maps.
            else if (runMazesWidth1 == true)
                me.RunDragonAgeExperimentSet(instances, MAM_Program.mazeMapFilenames); // Obstacle percents and grid sizes built-in to the maps.
            else if (runSpecific == true)
            {

                Console.WriteLine();
                me.RunInstance("Instance-20-0-2-0");
            }
            Console.WriteLine("\n*********************THE END**************************");
            Console.ReadLine();
        }
    }
}