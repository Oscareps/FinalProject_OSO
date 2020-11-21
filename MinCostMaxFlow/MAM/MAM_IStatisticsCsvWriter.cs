using System;
using System.IO;

namespace CPF_experiment
{
    public interface MAM_IStatisticsCsvWriter
    {
        /// <summary>
        /// Prints header of statistics of a single run to the given output. 
        /// </summary>
        void OutputStatisticsHeader(TextWriter output);

        /// <summary>
        /// Prints statistics of a single run to the given output.
        /// </summary>
        void OutputStatistics(TextWriter output);


        /// <summary>
        /// Clears statistics.
        /// </summary>
        void ClearStatistics();
    }

}
