//-----------------------------------------------------------------------
// <copyright file="LocalObjectRenamedRemoteObjectChanged.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Consumer.SituationSolver {
    using System;

    using CmisSync.Lib.Cmis.ConvenienceExtenders;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Exceptions;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.FileSystem;

    using DotCMIS.Client;
    using DotCMIS.Exceptions;

    /// <summary>
    /// Local object renamed remote object changed solver.
    /// </summary>
    public class LocalObjectRenamedRemoteObjectChanged : AbstractEnhancedSolver {
        private readonly ISolver changeChangeSolver;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="CmisSync.Lib.Consumer.SituationSolver.LocalObjectRenamedRemoteObjectChanged"/> class.
        /// </summary>
        /// <param name="session">Cmis session.</param>
        /// <param name="storage">Meta data storage.</param>
        /// <param name="changeSolver">Change solver.</param>
        public LocalObjectRenamedRemoteObjectChanged(
            ISession session,
            IMetaDataStorage storage,
            ISolver changeSolver) : base(session, storage) {
            if (changeSolver == null) {
                throw new ArgumentNullException("changeSolver", "Given situation solver for local and remote changes is null");
            }

            this.changeChangeSolver = changeSolver;
        }

        public override void Solve(
            IFileSystemInfo localFileSystemInfo,
            IObjectId remoteId, 
            ContentChangeType localContent,
            ContentChangeType remoteContent)
        {
            if (remoteId == null) {
                throw new ArgumentNullException("remoteId");
            }

            if (localFileSystemInfo == null) {
                throw new ArgumentNullException("localFileSystemInfo");
            }

            var obj = this.Storage.GetObjectByRemoteId(remoteId.Id);
            var cmisObject = remoteId as ICmisObject;
            string oldName = cmisObject.Name;
            string newName = localFileSystemInfo.Name;

            // Rename object
            try {
                cmisObject.Rename(newName, true);
            } catch (CmisConstraintException e) {
                if (!Utils.IsValidISO885915(newName)) {
                    OperationsLogger.Warn(string.Format("Server denied to rename {0} to {1}, perhaps because it contains UTF-8 characters", oldName, newName));
                    throw new InteractionNeededException(string.Format("Server denied renaming of {0}", oldName), e) {
                        Title = string.Format("Server denied renaming of {0}", oldName),
                        Description = string.Format("Server denied to rename {0} to {1}, perhaps because it contains UTF-8 characters", oldName, newName)
                    };
                }

                throw;
            } catch (CmisPermissionDeniedException) {
                OperationsLogger.Warn(string.Format("Unable to renamed remote object from {0} to {1}: Permission Denied", oldName, newName));
                return;
            }

            obj.Name = newName;
            obj.Ignored = cmisObject.AreAllChildrenIgnored();
            this.Storage.SaveMappedObject(obj);
            this.changeChangeSolver.Solve(localFileSystemInfo, remoteId, localContent, remoteContent);
        }
    }
}