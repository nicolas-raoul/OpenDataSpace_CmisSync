﻿//-----------------------------------------------------------------------
// <copyright file="LocalObjectChangedWithPWC.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Consumer.SituationSolver.PWC {
    using System;

    using CmisSync.Lib.Cmis.ConvenienceExtenders;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.FileSystem;

    using DotCMIS.Client;

    using log4net;

    /// <summary>
    /// Local object changed and file content should be uploaded via PWC. Otherwise the fallback solver is called.
    /// </summary>
    public class LocalObjectChangedWithPWC : AbstractEnhancedSolverWithPWC {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LocalObjectChangedWithPWC));
        private readonly ISolver folderOrFileContentUnchangedSolver;

        public LocalObjectChangedWithPWC(
            ISession session,
            IMetaDataStorage storage,
            IFileTransmissionStorage transmissionStorage,
            TransmissionManager manager,
            ISolver folderOrFileContentUnchangedSolver) : base(session, storage, transmissionStorage) {
            if (folderOrFileContentUnchangedSolver == null) {
                throw new ArgumentNullException("Given solver for folder or unchanged file content situations is null");
            }

            if (!session.ArePrivateWorkingCopySupported()) {
                throw new ArgumentException("The given session does not support private working copies");
            }

            this.folderOrFileContentUnchangedSolver = folderOrFileContentUnchangedSolver;
        }

        public override void Solve(
            IFileSystemInfo localFileSystemInfo,
            IObjectId remoteId,
            ContentChangeType localContent = ContentChangeType.NONE,
            ContentChangeType remoteContent = ContentChangeType.NONE)
        {
            if (localFileSystemInfo is IFileInfo) {
                var localFile = localFileSystemInfo as IFileInfo;
            } else {
                this.folderOrFileContentUnchangedSolver.Solve(localFileSystemInfo, remoteId, localContent, remoteContent);
            }
            return;
        }
    }
}