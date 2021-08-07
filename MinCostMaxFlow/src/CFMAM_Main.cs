using System;
using System.Collections.Generic;
using System.Diagnostics;
using CPF_experiment;

namespace CFMAM.src
{
    public class CFMAM_Main
    {
        static int INSTANCES_NUM = 10;
        static string OUTPUT_FOLDER = ""; 

        // Solvers definition on createSolvers function
        static List<IMS_ISolver> IMSSolvers = new List<IMS_ISolver>();
        static List<ICbsSolver> CFMCBSSolvers = new List<ICbsSolver>();

        /// <summary>
        /// This is the starting point of the program. 
        /// </summary>
        public static void Main(string[] args)
        {
            TextWriterTraceListener tr1 = new TextWriterTraceListener(System.Console.Out);
            Trace.Listeners.Add(tr1);

            createSolvers();

            CFMAM_Program me = new CFMAM_Program(IMSSolvers, CFMCBSSolvers, OUTPUT_FOLDER);

            bool runDragonAge = false;
            bool runGrids = true;
            bool runSpecific = false;

            if (runGrids == true)
            {
                int gridSizes = 10;     // Map size 8x8, 16x16 ...
                int agentListSizes = 3;  // Number of agents
                int obstaclesPercents = 20;   // Randomly allocatade obstacles percents

                me.RunExperimentSet(gridSizes, agentListSizes, obstaclesPercents, INSTANCES_NUM);
            }
            else if (runDragonAge == true)
            {
                // string[] daoMapFilenames = { "den502d.map", "ost003d.map", "brc202d.map" ,kiva.map};

                String[] daoMapFilenames = { "kiva.map","den312d.map" };

                /* string[] daoMapFilenames = {  "dao_maps\\Berlin_0_256.map",
                                                                        "dao_maps\\Berlin_0_512.map",
                                                                        "dao_maps\\Berlin_0_1024.map",
                                                                        "dao_maps\\Berlin_1_256.map",
                                                                        "dao_maps\\Berlin_1_512.map",
                                                                        "dao_maps\\Berlin_1_1024.map",
                                                                        "dao_maps\\Boston_0_256.map",
                                                                        "dao_maps\\Boston_0_512.map",
                                                                        "dao_maps\\Boston_0_1024.map", };*/

                me.RunDragonAgeExperimentSet(INSTANCES_NUM, "dao_maps", daoMapFilenames); // Obstacle percents and grid sizes built-in to the maps.
            }
            else if (runSpecific == true)
            {
                me.RunInstance("test2");
            }
            Console.WriteLine("*********************THE END**************************");
            Console.ReadLine();
        }

        /// <summary>
        /// Creates all the solvers for the CFMAM problem
        /// ** Change the solvers and the heuristics on this section to change problem solvers **
        /// </summary>
        private static void createSolvers()
        {

            // FastMap Heuristic
            //ISolver
            //CFMAM_ISolver CFMMStar_FastMapH_Makespan = new CFMMStar(CFMMStar.CostFunction.MakeSpan);
            //CFMAM_ISolver CFMMStar_FastMapH_SOC = new CFMMStar(CFMMStar.CostFunction.SOC);
            //MAM_HeuristicCalculator FastMapHCalculator = new FastMapHCalculator();
            //CFMMStar_FastMapH_Makespan.SetHeuristic(FastMapHCalculator);
            //CFMMStar_FastMapH_SOC.SetHeuristic(FastMapHCalculator);


            // Median Heuristic
            //ISolver
            IMS_ISolver CFMMStar_MedianH_Makespan = new CFMAStar(CFMAStar.CostFunction.MakeSpan);
            IMS_ISolver CFMMStar_MedianH_SOC = new CFMAStar(CFMAStar.CostFunction.SOC);
            MAM_HeuristicCalculator MedianHCalculator = new MedianHCalculator();
            CFMMStar_MedianH_Makespan.SetHeuristic(MedianHCalculator);
            CFMMStar_MedianH_SOC.SetHeuristic(MedianHCalculator);

            // Clique Heuristic
            //ISolver
            IMS_ISolver CFMMStar_CliqueH_Makespan = new CFMAStar(CFMAStar.CostFunction.MakeSpan);
            IMS_ISolver CFMMStar_CliqueH_SOC = new CFMAStar(CFMAStar.CostFunction.SOC);
            MAM_HeuristicCalculator CliqueHeuristic = new CliqueHCalculator();
            CFMMStar_CliqueH_Makespan.SetHeuristic(CliqueHeuristic);
            CFMMStar_CliqueH_SOC.SetHeuristic(CliqueHeuristic);


            // No Heuristic
            //ISolver
            IMS_ISolver MMStar_ZeroH_Makespan = new CFMAStar(CFMAStar.CostFunction.MakeSpan);
            IMS_ISolver CFMMStar_ZeroeH_SOC = new CFMAStar(CFMAStar.CostFunction.SOC);
            MAM_HeuristicCalculator ZeroHeuristic = new ZeroHCalculator();
            MMStar_ZeroH_Makespan.SetHeuristic(ZeroHeuristic);
            CFMMStar_ZeroeH_SOC.SetHeuristic(ZeroHeuristic);

            // *****  SOC CFMMStar Solvers  *****
            //solvers.Add(CFMMStar_FastMapH_Makespan);
            //solvers.Add(CFMMStar_FastMapH_SOC);

            //CFMMStarSolvers.Add(CFMMStar_MedianH_Makespan);
            //solvers.Add(CFMMStar_MedianH_SOC);

            IMSSolvers.Add(CFMMStar_CliqueH_Makespan);
            //CFMMStarSolvers.Add(CFMMStar_CliqueH_SOC);

            //CFMMStarSolvers.Add(MMStar_ZeroH_Makespan);
            //solvers.Add(CFMMStar_ZeroeH_SOC);



            // ***** SOC CBSMMStar Solvers *****
            CFMCBSSolvers.Add(new CFM_CBS());
        } 


    }
}
