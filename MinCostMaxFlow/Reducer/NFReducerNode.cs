using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPF_experiment
{
    class NFReducerNode
    {
        public static int indexCounter = 0;

        public int nodeIndex;
        public int nodeTime;
        public LinkedList<NFReducerNode> edgeTo;
        public LinkedList<NFReducerNode> edgeFrom;
        public bool isInputNode;
        public int x;
        public int y;

        public NFReducerNode(int nodeTime, int x, int y)
        {
            this.nodeIndex = indexCounter;
            indexCounter++;
            this.nodeTime = nodeTime;
            this.edgeTo = new LinkedList<NFReducerNode>();
            this.edgeFrom = new LinkedList<NFReducerNode>();
            this.isInputNode = true;
            this.x = x;
            this.y = y;
        }

        public void AddEdgeTo(NFReducerNode node)
        {
            this.edgeTo.AddLast(node);
        }

        public void AddEdgeFrom(NFReducerNode node)
        {
            this.edgeFrom.AddLast(node);
        }

        public NFReducerNode Duplicate()
        {
            NFReducerNode duplicated = new NFReducerNode(this.nodeTime, this.x, this.y);
            duplicated.isInputNode = false;
            return duplicated;
        }

        public override bool Equals(object obj)
        {
            NFReducerNode other = ((NFReducerNode)obj);
            return (this.x == other.x) && (this.y == other.y) && (this.nodeTime == other.nodeTime) && (this.isInputNode == other.isInputNode);
        }

        public override int GetHashCode()
        {
            var hashCode = -794693248;
            hashCode = hashCode * -1521134295 + nodeTime.GetHashCode();
            hashCode = hashCode * -1521134295 + isInputNode.GetHashCode();
            hashCode = hashCode * -1521134295 + x.GetHashCode();
            hashCode = hashCode * -1521134295 + y.GetHashCode();
            return hashCode;
        }

        public static void DecreaseIndexCounter()
        {
            indexCounter--;
        }

        public override string ToString()
        {
            return this.nodeIndex + ": (" + this.x + "," + this.y + ")";
        }
    }
}
