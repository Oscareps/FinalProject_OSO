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

        private string resultsFileName;
        private bool onlyReadInstances;
        private string outputDirectory;
        private List<IMS_ISolver> imsSolversList;
        private List<ICbsSolver> cfmCbsSolversList;

        public CFMAM_Program(List<IMS_ISolver> imsSolversList, List<ICbsSolver> cfmCbsSolversList, string outputDirectory="", string resultsFileName="", bool onlyReadInstances=false)
        { 

            if(resultsFileName.Equals(""))
            {
                this.resultsFileName = Process.GetCurrentProcess().ProcessName + ".csv";
            }

            if (outputDirectory.Equals(""))
                outputDirectory = Directory.GetCurrentDirectory();

            if (!Directory.Exists(outputDirectory))
                throw new Exception("Not a valid output directory");

            this.outputDirectory = Path.Combine(new[] { outputDirectory, "Instances" });
            Directory.CreateDirectory(this.outputDirectory);

            this.onlyReadInstances = onlyReadInstances;
            this.imsSolversList = imsSolversList;
            this.cfmCbsSolversList = cfmCbsSolversList;
        }

        /// <summary>
        /// Simplest run possible with a randomly generated problem instance.
        /// </summary>
        public void SimpleRun(int gridSize, int agentsNum, int obstaclesNum)
        {
            CFMAM_Run runner = new CFMAM_Run(this.imsSolversList, this.cfmCbsSolversList);
            runner.OpenResultsFile(this.resultsFileName);
            runner.PrintResultsFileHeader();
            ProblemInstance instance = runner.GenerateProblemInstance(gridSize, agentsNum, obstaclesNum);
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
            ProblemInstance instance;
            try
            {
                String[] pathElements = { this.outputDirectory, fileName };
                instance = ProblemInstance.Import(Path.Combine(pathElements));
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Bad problem instance {0}. Error: {1}", fileName, e.Message));
                return false;
            }

            CFMAM_Run runner = new CFMAM_Run(this.imsSolversList, this.cfmCbsSolversList);
            if (runner.m_mapFileName == "")
                runner.m_mapFileName = "Grid" + instance.GetMaxX() + "x" + instance.GetMaxY();
            bool resultsFileExisted = File.Exists(this.resultsFileName);
            runner.OpenResultsFile(this.resultsFileName);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();
            bool success = runner.SolveGivenProblem(instance);
            runner.CloseResultsFile();


            return success;
        }

        /// <summary>
        /// Runs a set of experiments.
        /// This function will generate a random instance (or load it from a file if it was already generated)
        /// </summary>
        public void RunExperimentSet(int gridSizes, int agentListSizes, int obstaclesProbs, int instances)
        {

            int[] grid = new int[] { gridSizes };
            int[] agentList = new int[] { agentListSizes };
            int[] obstacles = new int[] { obstaclesProbs };

            ProblemInstance instance;
            string instanceName;
            CFMAM_Run runner = new CFMAM_Run(this.imsSolversList, this.cfmCbsSolversList);

            bool resultsFileExisted = File.Exists(this.resultsFileName);
            runner.OpenResultsFile(this.resultsFileName);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();

            bool continueFromLastRun = false;
            string[] LastProblemDetails = null;
            string currentProblemFileName = Path.Combine(new[] { this.outputDirectory, "current_problem_" + Process.GetCurrentProcess().ProcessName });

            if (File.Exists(currentProblemFileName)) //if we're continuing running from last time
            {
                var lastProblemFile = new StreamReader(currentProblemFileName);
                LastProblemDetails = lastProblemFile.ReadLine().Split(',');  //get the last problem
                lastProblemFile.Close();
                continueFromLastRun = true;
            }

            for (int gs = 0; gs < grid.Length; gs++)
            {
                for (int obs = 0; obs < obstacles.Length; obs++)
                {
                    runner.ResetOutOfTimeCounters();
                    for (int ag = 0; ag < agentList.Length; ag++)
                    {
                        if (grid[gs] * grid[gs] * (1 - obstacles[obs] / 100) < agentList[ag]) // Probably not enough room for all agents
                            continue;
                        for (int i = 0; i < instances; i++)
                        {
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
                            instanceName = "Instance-" + grid[gs] + "-" + obstacles[obs] + "-" + agentList[ag] + "-" + i;
                            try
                            {
                                instance = ProblemInstance.Import(Path.Combine(new[] { this.outputDirectory, instanceName}));
                                instance.instanceId = i;
                            }
                            catch (Exception importException)
                            {
                                if (onlyReadInstances)
                                {
                                    Console.WriteLine("File " + instanceName + "  dosen't exist");
                                    return;
                                }

                                instance = runner.GenerateProblemInstance(grid[gs], agentList[ag], obstacles[obs] * grid[gs] * grid[gs] / 100);
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

        protected static readonly string[] mazeMapFilenames = { "mazes-width1-maps\\maze512-1-6.map", "mazes-width1-maps\\maze512-1-2.map",
                                                "mazes-width1-maps\\maze512-1-9.map" };


        public static Stopwatch sw = new Stopwatch();
        /// <summary>
        /// Dragon Age experiment
        /// </summary>
        /// <param name="numInstances"></param>
        /// <param name="mapFileNames"></param>
        public void RunDragonAgeExperimentSet(int numInstances, string mapsFolder, string[] mapFileNames)
        {
            string[] mapPaths = createMapPathes(mapsFolder, mapFileNames);

            ProblemInstance instance;
            string instanceName;
            CFMAM_Run runner = new CFMAM_Run(this.imsSolversList, this.cfmCbsSolversList);

            bool resultsFileExisted = File.Exists(this.resultsFileName);
            runner.OpenResultsFile(this.resultsFileName);
            if (resultsFileExisted == false)
                runner.PrintResultsFileHeader();

            TextWriter output;
            int[] agentListSizes = { 5 };

            bool continueFromLastRun = false;
            string[] lineParts = null;

            String[] pathElements = { Directory.GetCurrentDirectory(), "current problem-" + Process.GetCurrentProcess().ProcessName };
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


                    for (int map = 0; map < mapPaths.Length; map++)
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
                        string mapFileName = mapPaths[map];
                        instanceName = Path.GetFileNameWithoutExtension(mapFileName) + "-" + agentListSizes[ag] + "-" + i;
                        try
                        {
                            String[] path = { Directory.GetCurrentDirectory(), mapsFolder, instanceName };
                            instance = ProblemInstance.Import(Path.Combine(path));
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

        private string[] createMapPathes(string mapsFolder, string[] mapFileNames)
        {
            return mapFileNames.Select(mapFileName => Path.Combine(mapsFolder, mapFileName)).ToArray();
        }
    }
}