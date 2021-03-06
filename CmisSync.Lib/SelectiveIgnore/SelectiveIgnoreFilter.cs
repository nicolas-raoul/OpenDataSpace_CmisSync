//-----------------------------------------------------------------------
// <copyright file="SelectiveIgnoreFilter.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.SelectiveIgnore {
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;

    using CmisSync.Lib.Events;
    using CmisSync.Lib.Filter;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;

    using DotCMIS.Client;

    /// <summary>
    /// Selective ignore filter.
    /// All file/folder events for affecting files/folders which are inside an ignored folder are filtered out.
    /// </summary>
    public class SelectiveIgnoreFilter : SyncEventHandler {
        private IIgnoredEntitiesStorage storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="CmisSync.Lib.SelectiveIgnore.SelectiveIgnoreFilter"/> class.
        /// </summary>
        /// <param name="storage">Ignored entities storage.</param>
        public SelectiveIgnoreFilter(IIgnoredEntitiesStorage storage) {
            if (storage == null) {
                throw new ArgumentNullException("storage");
            }

            this.storage = storage;
        }

        /// <summary>
        /// Handles IFilterableRemoteObjectEvent or IFilterableLocalPathEvent if they affect an ignored folder.
        /// </summary>
        /// <param name="e">The event to handle.</param>
        /// <returns>true if handled</returns>
        public override bool Handle(ISyncEvent e) {
            var filterableRemoteObjectEvent = e as IFilterableRemoteObjectEvent;
            if (filterableRemoteObjectEvent != null) {
                var remoteObject = filterableRemoteObjectEvent.RemoteObject;
                var remoteFolder = remoteObject as IFolder;
                var remoteDoc = remoteObject as IDocument;
                if (remoteFolder != null) {
                    if (this.storage.IsIgnored(remoteFolder) == IgnoredState.Inherited) {
                        var filterablePathEvent = e as IFilterableLocalPathEvent;
                        if (filterablePathEvent != null) {
                            var localPath = filterablePathEvent.LocalPath;
                            if (localPath != null && this.storage.IsIgnoredPath(localPath) == IgnoredState.NotIgnored) {
                                return false;
                            }
                        }

                        return true;
                    }
                } else if (remoteDoc != null) {
                    if (this.storage.IsIgnored(remoteDoc) == IgnoredState.Inherited) {
                        return true;
                    }
                }
            }

            if (e is IFilterableLocalPathEvent) {
                var path = (e as IFilterableLocalPathEvent).LocalPath;
                if (path != null) {
                    if (this.storage.IsIgnoredPath(path) == IgnoredState.Inherited) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}