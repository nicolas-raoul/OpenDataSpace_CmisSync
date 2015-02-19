//-----------------------------------------------------------------------
// <copyright file="Unsubscriber.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Queueing {
    using System;
    using System.Collections.Generic;

    public class Unsubscriber<T> : IDisposable {
        private List<IObserver<T>> observers;
        private IObserver<T> observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer) {
            this.observers = observers;
            this.observer = observer;
        }

        public void Dispose() {
            if (this.observer != null && this.observers.Contains(this.observer)) {
                this.observers.Remove(this.observer);
            }
        }
    }
}