//-----------------------------------------------------------------------
// <copyright file="SuccessfulLoginEvent.cs" company="GRAU DATA AG">
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
using System;

namespace CmisSync.Lib.Events
{
    /// <summary>
    /// Successful login on a server should add this event to the event queue.
    /// </summary>
    public class SuccessfulLoginEvent : ISyncEvent
    {
        private Uri Url;

        /// <summary>
        /// Initializes a new instance of the <see cref="CmisSync.Lib.Events.SuccessfulLoginEvent"/> class.
        /// </summary>
        /// <param name="url">URL of the successful connection</param>
        public SuccessfulLoginEvent( Uri url)
        {
            if (url == null)
                throw new ArgumentNullException("Given Url is null");
            Url = url;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="CmisSync.Lib.Events.SuccessfulLoginEvent"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="CmisSync.Lib.Events.SuccessfulLoginEvent"/>.</returns>
        public override string ToString ()
        {
            return string.Format ("[SuccessfulLoginEvent {0}]", Url.ToString());
        }
    }
}

