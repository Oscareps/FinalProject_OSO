using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CPF_experiment
{
    public class CFMCbsConstraint : IComparable
    {
        public byte agentNum {get; protected set;}
        public TimedMove move {get; protected set;}
        public bool queryInstance = false;

        public CFMCbsConstraint(int agentNum, int posX, int posY, Move.Direction direction, int timeStep)
        {
            this.Init(agentNum, posX, posY, direction, timeStep);
        }

        public CFMCbsConstraint(int agentNum, TimedMove move)
        {
            this.Init(agentNum, move);
        }

        public CFMCbsConstraint() : this(-1, -1, -1, Move.Direction.NO_DIRECTION, -1) {} // Nonsense values until Init, just allocate move

        public CFMCbsConstraint(CFMCbsConflict conflict, ProblemInstance instance, bool agentA)
        {
            Move move;
            int agentNum;
            int minTime;
            
            if (agentA)
                minTime = conflict.timeStepAgentA;
            else
                minTime = conflict.timeStepAgentB;
            
            if (agentA)
            {
                move = conflict.agentAmove;
                agentNum = instance.m_vAgents[conflict.agentAIndex].agentIndex;
                this.move = new TimedMove(move, minTime);
            }
            else
            {
                move = conflict.agentBmove;
                agentNum = instance.m_vAgents[conflict.agentBIndex].agentIndex;
                this.move = new TimedMove(move, minTime);
            }

            this.agentNum = (byte)agentNum;
            

            if (conflict.vertex)
                this.move.direction = Move.Direction.NO_DIRECTION;
        }

        public void Init(int agentNum, int posX, int posY, Move.Direction direction, int timeStep)
        {
            this.Init(agentNum, new TimedMove(posX, posY, direction, timeStep));
        }

        public void Init(int agentNum, TimedMove move)
        {
            this.agentNum = (byte)agentNum;
            this.move = move;
        }

        public int time
        {
            get
            {
                return this.move.time;
            }
        }

        /// <summary>
        /// Checks that the agentNum is equal, and compares the move.
        /// If one of the constraints is a query, an instance only created and used to quickly search for a move in a set of constraints,
        /// the direction is ignored if the other constraint is a vertex constraint.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            CFMCbsConstraint other = (CFMCbsConstraint)obj;
            if (this.agentNum != other.agentNum)
                return false;

            Debug.Assert(this.queryInstance == false || other.queryInstance == false); // At most one of the instances is a query
            Debug.Assert(this.queryInstance == false || this.move.direction != Move.Direction.NO_DIRECTION); // Must query regarding a specific direction
            Debug.Assert(other.queryInstance == false || other.move.direction != Move.Direction.NO_DIRECTION); // Must query regarding a specific direction
            if (this.queryInstance || other.queryInstance) // This way if the constraint is a vertex constraint than it will be equal to a query containing a move from any direction to that position,
                                                           // and if it is an edge constraint than it will only be equal to queries containing a move from that specific direction to that position.
                return this.move.Equals(other.move);
            else // A vertex constraint is different to an edge constraint for the same agentNum and position.
                 // Must check the direction explicitly because vertex constraints have no direction and moves with no direction
                 // compare equal to moves with any direction
                return this.move.Equals(other.move) && this.move.direction == other.move.direction; 
        }

        /// <summary>
        /// Uses the move and the agents.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int ans = 0;
                ans += this.move.GetHashCode() * 3;
                ans += this.agentNum * 5;
                return ans;
            }
        }

        public int GetTimeStep() { return this.move.time; } // FIXME: Make this into a property

        public Move.Direction GetDirection()
        {
            return this.move.direction;
        }
        
        public override string ToString()
        {
            return move.ToString() + "-" + move.direction.ToString().PadRight(12) + " time=" + move.time + " agentNum " + agentNum + "";
        }


        public int CompareTo(object item)
        {
            CFMCbsConstraint other = (CFMCbsConstraint)item;

            return this.move.time.CompareTo(other.move.time);
        }

    }
}
