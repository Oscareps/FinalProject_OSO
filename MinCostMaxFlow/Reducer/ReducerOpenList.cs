using System;
using System.Collections.Generic;
using System.Text;

namespace CPF_experiment
{
    class ReducerOpenList<T>
    {
        private Dictionary<T, T> listDict;
        private Queue<T> listQueue;
        public int Count;

        public ReducerOpenList()
        {
            this.listDict = new Dictionary<T, T>();
            this.listQueue = new Queue<T>();
            this.Count = 0;
        }

        public void Enqueue(T toAdd)
        {
            this.listDict.Add(toAdd, toAdd);
            this.listQueue.Enqueue(toAdd);
            this.Count++;
        }

        public T Get(T toGet)
        {
            return this.listDict[toGet];
        }

        public bool Contains(T toCheck)
        {
            return this.listDict.ContainsKey(toCheck);
        }

        public T Dequeue()
        {
            if(this.Count != 0)
            {
                T firstInQueue = this.listQueue.Dequeue();
                this.listDict.Remove(firstInQueue);
                Count--;
                return firstInQueue;
            }
            throw new Exception("Can't dequeue from empty queue");
        }
    }
}
