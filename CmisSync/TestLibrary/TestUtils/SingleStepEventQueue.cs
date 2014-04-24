//-----------------------------------------------------------------------
// <copyright file="SingleStepEventQueue.cs" company="GRAU DATA AG">
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General private License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General private License for more details.
//
//   You should have received a copy of the GNU General private License
//   along with this program. If not, see http://www.gnu.org/licenses/.
//
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Generic;

using CmisSync.Lib.Events;

namespace TestLibrary
{   
    /// <summary>
    /// This is a synchronous test-replacement for SyncEventQueue
    /// </summary>
    /// Do not use this in production code. 
    /// It contains public fields that could do a lot of harm 
    public class SingleStepEventQueue : ISyncEventQueue {
        public SyncEventManager manager; 
        public Queue<ISyncEvent> queue = new Queue<ISyncEvent>();

        public SingleStepEventQueue(SyncEventManager manager) {
            this.manager = manager;
        }

        public void AddEvent(ISyncEvent e) {
            queue.Enqueue(e);
        }

        public bool IsStopped {
            get {
                return queue.Count == 0; 
            }
        }

        public void Step() {
            ISyncEvent e = queue.Dequeue();
            manager.Handle(e);
        }

        public void Run() {
            while(!IsStopped) {
                Step();
            }
        }

        public void RunStartSyncEvent() {
            var startSyncEvent = new StartNextSyncEvent (false);
            AddEvent(startSyncEvent);
            Run();
        }
    }
}
