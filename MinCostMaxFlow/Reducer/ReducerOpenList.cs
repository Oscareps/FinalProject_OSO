using System;
using System.Collections.Generic;
using System.Text;

namespace CPF_experiment
{
    class ReducerOpenList
    {
        private Dictionary<NFReducerNode, NFReducerNode> listDict;
        private Queue<NFReducerNode> listQueue;
        public int Count;

        public ReducerOpenList()
        {
            this.listDict = new Dictionary<NFReducerNode, NFReducerNode>();
            this.listQueue = new Queue<NFReducerNode>();
            this.Count = 0;
        }

        public void Enqueue(NFReducerNode toAdd)
        {
            this.listDict.Add(toAdd, toAdd);
            this.listQueue.Enqueue(toAdd);
            this.Count++;
        }

        public NFReducerNode Get(NFReducerNode toGet)
        {
            return this.listDict[toGet];
        }

        public bool Contains(NFReducerNode toCheck)
        {
            return this.listDict.ContainsKey(toCheck);
        }

        public NFReducerNode Dequeue()
        {
            if(this.Count != 0)
            {
                NFReducerNode firstInQueue = this.listQueue.Dequeue();
                this.listDict.Remove(firstInQueue);
                Count--;
                return firstInQueue;
            }
            throw new Exception("Can't dequeue from empty queue");
        }
    }
}
