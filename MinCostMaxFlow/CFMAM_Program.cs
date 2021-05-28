using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace CPF_experiment
{
    /// <summary>
    /// This is the entry point of the application. 
    /// </summary>
    public class CFMAM_Program
    {

        private static string RESULTS_FILE_NAME = "Results.csv"; // Overridden by Main
        private static bool onlyReadInstances = false;

        /// <summary>
        /// Simplest run possible with a randomly generated problem instance.
        /// </summary>
        public void SimpleRun()
        {
            CFMAM_Run runner = new CFMAM_Run();
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
                String[] pathElements = { Directory.GetCurrentDirectory(), "MAM_Instances", fileName };
                instance = MAM_ProblemInstance.Import(Path.Combine(pathElements));
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Skipping bad problem instance {0}. Error: {1}", fileName, e.Message));
                return false;
            }

            CFMAM_Run runner = new CFMAM_Run();
            if (runner.m_mapFileName == "")
                runner.m_mapFileName = "Grid" + instance.GetMaxX() + "x" + instance.GetMaxY();
            bool resultsFileExisted = File.Exists(RESULTS_FILE_NAME);
            runner.OpenResultsFile(RESULTS_FILE_NAME);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();
            bool success;
            success = runner.SolveGivenProblem(instance);
            runner.CloseResultsFile();


            return success;
        }

        /// <summary>
        /// Runs a set of experiments.
        /// This function will generate a random instance (or load it from a file if it was already generated)
        /// </summary>
        public void RunExperimentSet(int[] gridSizes, int[] agentListSizes, int[] obstaclesProbs, int instances)
        {

            MAM_ProblemInstance instance;
            string instanceName;
            CFMAM_Run runner = new CFMAM_Run();

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

                            try
                            {
                                runner.SolveGivenProblem(instance);
                            }
                            catch (TimeoutException e)
                            {
                                Console.Out.WriteLine(e.Message);
                                Console.Out.WriteLine();
                                continue;
                            }

                            // Save the latest problem
                            try
                            {
                                if (File.Exists(currentProblemFileName))
                                    File.Delete(currentProblemFileName);
                            }
                            catch
                            {
                                ;
                            }
                            var lastProblemFile = new StreamWriter(currentProblemFileName);
                            lastProblemFile.Write("{0},{1},{2},{3}", gs, obs, ag, i);
                            for (int j = 0; j < runner.outOfTimeCounters.Length; j++)
                            {
                                lastProblemFile.Write("," + runner.outOfTimeCounters[j]);
                            }
                            lastProblemFile.Close();
                        }
                    }
                }
            }
            runner.CloseResultsFile();
        }

        //protected static readonly string[] daoMapFilenames = {/* "dao_maps\\den502d.map", "dao_maps\\ost003d.map", */"dao_maps\\brc202d.map" ,dao_maps\\kiva.map};

        static String[] pathElem = { "dao_maps", "kiva.map" };
        protected static readonly string[] daoMapFilenames = { Path.Combine(pathElem) };

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
            CFMAM_Run runner = new CFMAM_Run();

            bool resultsFileExisted = File.Exists(RESULTS_FILE_NAME);
            runner.OpenResultsFile(RESULTS_FILE_NAME);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();

            TextWriter output;
            int[] agentListSizes = { 5 };

            bool continueFromLastRun = false;
            string[] lineParts = null;

            String[] pathElements = { Directory.GetCurrentDirectory(), "MAM_Instances", "current problem-" + Process.GetCurrentProcess().ProcessName };
            string currentProblemFileName = Path.Combine(pathElements);

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
                            String[] path = { Directory.GetCurrentDirectory(), "MAM_Instances", instanceName };
                            instance = MAM_ProblemInstance.Import(Path.Combine(pathElements));
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
        public static void CFMAM_Main(string[] args)
        {
            CFMAM_Program me = new CFMAM_Program();
            CFMAM_Program.RESULTS_FILE_NAME = Process.GetCurrentProcess().ProcessName + ".csv";
            TextWriterTraceListener tr1 = new TextWriterTraceListener(System.Console.Out);
            Trace.Listeners.Add(tr1);


            if (Directory.Exists(Directory.GetCurrentDirectory() + "\\Instances") == false)
            {
                Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Instances");
            }

            CFMAM_Program.onlyReadInstances = false;

            int instances = 100;

            bool runDragonAge = false;
            bool runGrids = false;
            bool runMazesWidth1 = false;
            bool runSpecific = true;

            if (runGrids == true)
            {
                int[] gridSizes = new int[] { 10 };     // Map size 8x8, 16x16 ...

                int[] agentListSizes = new int[] { 3 };  // Number of agents


                int[] obstaclesPercents = new int[] { 20 };   // Randomly allocatade obstacles percents
                me.RunExperimentSet(gridSizes, agentListSizes, obstaclesPercents, instances);
            }
            else if (runDragonAge == true)
                me.RunDragonAgeExperimentSet(instances, CFMAM_Program.daoMapFilenames); // Obstacle percents and grid sizes built-in to the maps.
            else if (runMazesWidth1 == true)
                me.RunDragonAgeExperimentSet(instances, CFMAM_Program.mazeMapFilenames); // Obstacle percents and grid sizes built-in to the maps.
            else if (runSpecific == true)
            {

                Console.WriteLine();
                me.RunInstance("test2");
            }
            Console.WriteLine("*********************THE END**************************");
            Console.ReadLine();
        }
    }
}