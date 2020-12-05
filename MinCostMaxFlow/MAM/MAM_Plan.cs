using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace CPF_experiment
{
    /// <summary>
    /// Represents a plan for a set of agents.
    /// </summary>
    public class MAM_Plan
    {
        public List<List<Move>> listOfLocations; // The plan

        /// <summary>
        /// Reconstructs the plan by goind backwards from the goal.
        /// </summary>
        /// <param name="lastAgentsStates">The goal state from which to start going backwards</param>
        public MAM_Plan
        (
            List<MAM_AgentState> lastAgentsStates
        )
        {
            listOfLocations = new List<List<Move>>();
            lastAgentsStates = lastAgentsStates.OrderBy(o => o.agentIndex).ToList();
            foreach (MAM_AgentState state in lastAgentsStates)
            {
                List<Move> newList = new List<Move>();
                MAM_AgentState lastState = state;
                while (lastState != null)
                {
                    newList.Add(lastState.lastMove);
                    lastState = lastState.prev;
                }
                newList.Reverse();
                listOfLocations.Add(newList);
            }
        }

        public void ToString()
        {
            if (listOfLocations == null)
                return;
            PrintLine();
            List<Move> currentMoves = new List<Move>();
            for (int agentIndex = 0; agentIndex < listOfLocations.Count; agentIndex++)
            {
                currentMoves.Add(listOfLocations[agentIndex][0]);
            }
            string[] columns = new string[listOfLocations.Count + 1];
            columns[0] = "";
            for (int agentIndex = 1; agentIndex < listOfLocations.Count + 1; agentIndex++)
            {
                columns[agentIndex] = (agentIndex - 1).ToString();

            }
            PrintRow(columns);
            PrintLine();
            int maxTime = 0;
            foreach (List<Move> agentPath in listOfLocations)
            {
                if (agentPath.Count > maxTime)
                    maxTime = agentPath.Count;
            }
            for(int time = 0; time < maxTime; time++)
            {
                columns = new string[listOfLocations.Count + 1];
                columns[0] = time.ToString();
                for (int i = 0; i < currentMoves.Count; i++)
                {
                    Move currentMove = currentMoves[i];
                    if (currentMove != null)
                        columns[i + 1] = currentMove.x + "," + currentMove.y;
                    else
                        columns[i + 1] = " ";
                }
                PrintRow(columns);
                for (int agentIndex = 0; time != maxTime - 1 && agentIndex < listOfLocations.Count; agentIndex++)
                {
                    if (listOfLocations[agentIndex].Count > time + 1)
                        currentMoves[agentIndex] = listOfLocations[agentIndex][time + 1];
                    else
                        currentMoves[agentIndex] = null;
                }
            }
            PrintLine();
        }

        private int tableWidth = 200;

        private void PrintLine()
        {
            Console.WriteLine(new string('-', tableWidth));
        }

        private void PrintRow(params string[] columns)
        {
            int width = (tableWidth - columns.Length) / columns.Length;
            string row = "|";

            foreach (string column in columns)
            {
                row += AlignCentre(column, width) + "|";
            }

            Console.WriteLine(row);

        }

        private string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        }

    }
}
