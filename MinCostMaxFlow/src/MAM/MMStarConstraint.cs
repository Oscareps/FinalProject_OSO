using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    public class MMStarConstraint
    {
        public int agentNum;
        public TimedMove move;
        public bool vertexConflict = false;

        public MMStarConstraint(int agentNum, int posX, int posY, Move.Direction direction, int timeStep)
        {
            this.move = new TimedMove(posX, posY, direction, timeStep);
            this.agentNum = agentNum;
            if (direction == Move.Direction.NO_DIRECTION)
                this.vertexConflict = true;
            else
                this.vertexConflict = false;
        }

        public MMStarConstraint(CFMCbsConstraint constraint)
            : this(constraint.agentNum, constraint.move.x, constraint.move.y, constraint.move.direction, constraint.move.time)
        { }


        public override bool Equals(object obj)
        {
            MMStarConstraint other = (MMStarConstraint)obj;
            if (this.agentNum != other.agentNum)
                return false;

            if (this.vertexConflict || other.vertexConflict) // This way if the constraint is a vertex constraint than it will be equal to a query containing a move from any direction to that position,
                                                           // and if it is an edge constraint than it will only be equal to queries containing a move from that specific direction to that position.
                return this.move.Equals(other.move);
            else // A vertex constraint is different to an edge constraint for the same agentNum and position.
                 // Must check the direction explicitly because vertex constraints have no direction and moves with no direction
                 // compare equal to moves with any direction
                return this.move.Equals(other.move) && this.move.direction == other.move.direction;
        }

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
    }
}
