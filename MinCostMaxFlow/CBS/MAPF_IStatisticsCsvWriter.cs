using System;
using System.IO;

namespace CPF_experiment
{
    public interface MAPF_IStatisticsCsvWriter
    {

        /// Prints statistics of a single run to the given output.
        /// </summary>
        void OutputStatistics(TextWriter output);

    }

    public interface IAccumulatingStatisticsCsvWriter : MAPF_IStatisticsCsvWriter
    {
        void ClearAccumulatedStatistics();
        void AccumulateStatistics();
        void OutputAccumulatedStatistics(TextWriter output);
    }
}
