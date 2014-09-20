//-----------------------------------------------------------------------
// <copyright file="RemoteObjectChanged.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Consumer.SituationSolver
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    using CmisSync.Lib.Events;
    using CmisSync.Lib.FileTransmission;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Storage.FileSystem;

    using DotCMIS.Client;

    using log4net;

    /// <summary>
    /// Remote object has been changed. => update the metadata locally.
    /// </summary>
    public class RemoteObjectChanged : AbstractEnhancedSolver
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(RemoteObjectChanged));
        private static readonly ILog OperationsLogger = LogManager.GetLogger("OperationsLogger");

        private IFileSystemInfoFactory fsFactory;
        private ActiveActivitiesManager transmissonManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="CmisSync.Lib.Consumer.SituationSolver.RemoteObjectChanged"/> class.
        /// </summary>
        /// <param name="session">Cmis session.</param>
        /// <param name="storage">Meta data storage.</param>
        /// <param name="transmissonManager">Transmisson manager.</param>
        /// <param name="fsFactory">File System Factory.</param>
        public RemoteObjectChanged(
            ISession session,
            IMetaDataStorage storage,
            ActiveActivitiesManager transmissonManager,
            IFileSystemInfoFactory fsFactory = null) : base(session, storage)
        {
            if (transmissonManager == null) {
                throw new ArgumentNullException("Given transmission manager is null");
            }

            this.transmissonManager = transmissonManager;
            this.fsFactory = fsFactory ?? new FileSystemInfoFactory();
        }

        /// <summary>
        /// Solve the specified situation by using the session, storage, localFile and remoteId.
        /// If a folder is affected, simply update the local change time of the corresponding local folder.
        /// If it is a file and the changeToken is not equal to the saved, the new content is downloaded.
        /// </summary>
        /// <param name="localFile">Local file.</param>
        /// <param name="remoteId">Remote identifier.</param>
        /// <param name="localContent">Hint if the local content has been changed.</param>
        /// <param name="remoteContent">Information if the remote content has been changed.</param>
        public override void Solve(
            IFileSystemInfo localFile,
            IObjectId remoteId,
            ContentChangeType localContent = ContentChangeType.NONE,
            ContentChangeType remoteContent = ContentChangeType.NONE)
        {
            IMappedObject obj = this.Storage.GetObjectByRemoteId(remoteId.Id);
            if (remoteId is IFolder) {
                var remoteFolder = remoteId as IFolder;
                DateTime? lastModified = remoteFolder.LastModificationDate;
                obj.LastChangeToken = remoteFolder.ChangeToken;
                if (lastModified != null) {
                    try {
                        localFile.LastWriteTimeUtc = (DateTime)lastModified;
                    } catch(IOException e) {
                        Logger.Debug("Couldn't set the server side modification date", e);
                    }

                    obj.LastLocalWriteTimeUtc = localFile.LastWriteTimeUtc;
                }
            } else if (remoteId is IDocument) {
                var remoteDocument = remoteId as IDocument;
                DateTime? lastModified = remoteDocument.LastModificationDate;
                if ((lastModified != null && lastModified != obj.LastRemoteWriteTimeUtc) || obj.LastChangeToken != remoteDocument.ChangeToken) {
                    if (remoteContent != ContentChangeType.NONE) {
                        if (obj.LastLocalWriteTimeUtc != localFile.LastWriteTimeUtc) {
                            throw new ArgumentException("The local file has been changed since last write => aborting update");
                        }

                        obj.LastChecksum = DownloadChanges(localFile as IFileInfo, remoteDocument, obj, this.fsFactory, this.transmissonManager);
                    }

                    obj.LastRemoteWriteTimeUtc = remoteDocument.LastModificationDate;
                    if (remoteDocument.LastModificationDate != null) {
                        localFile.LastWriteTimeUtc = (DateTime)remoteDocument.LastModificationDate;
                    }

                    obj.LastLocalWriteTimeUtc = localFile.LastWriteTimeUtc;
                    obj.LastContentSize = remoteDocument.ContentStreamLength ?? 0;
                }

                obj.LastChangeToken = remoteDocument.ChangeToken;
                obj.LastRemoteWriteTimeUtc = lastModified;
            }

            this.Storage.SaveMappedObject(obj);
        }

        public static byte[] DownloadChanges(IFileInfo target, IDocument remoteDocument, IMappedObject obj, IFileSystemInfoFactory fsFactory, ActiveActivitiesManager transmissonManager) {
            // Download changes
            byte[] lastChecksum = obj.LastChecksum;
            byte[] hash = null;
            var cacheFile = fsFactory.CreateDownloadCacheFileInfo(target);
            var transmissionEvent = new FileTransmissionEvent(FileTransmissionType.DOWNLOAD_MODIFIED_FILE, target.FullName, cacheFile.FullName);
            transmissonManager.AddTransmission(transmissionEvent);
            using (SHA1 hashAlg = new SHA1Managed())
            using (var filestream = cacheFile.Open(FileMode.Create, FileAccess.Write, FileShare.None))
            using (IFileDownloader download = ContentTaskUtils.CreateDownloader()) {
                try {
                    download.DownloadFile(remoteDocument, filestream, transmissionEvent, hashAlg);
                } catch(Exception ex) {
                    transmissionEvent.ReportProgress(new TransmissionProgressEventArgs { FailedException = ex });
                    throw;
                }

                obj.ChecksumAlgorithmName = "SHA-1";
                hash = hashAlg.Hash;
            }

            var backupFile = fsFactory.CreateFileInfo(target.FullName + ".bak.sync");
            Guid? uuid = target.Uuid;
            cacheFile.Replace(target, backupFile, true);
            try {
                target.Uuid = uuid;
            } catch (RestoreModificationDateException e) {
                Logger.Debug("Failed to restore modification date of original file", e);
            }

            try {
                backupFile.Uuid = null;
            } catch (RestoreModificationDateException e) {
                Logger.Debug("Failed to restore modification date of backup file", e);
            }

            byte[] checksumOfOldFile = null;
            using (var oldFileStream = backupFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) {
                checksumOfOldFile = SHA1Managed.Create().ComputeHash(oldFileStream);
            }

            if (!lastChecksum.SequenceEqual(checksumOfOldFile)) {
                var conflictFile = fsFactory.CreateConflictFileInfo(target);
                backupFile.MoveTo(conflictFile.FullName);
                OperationsLogger.Info(string.Format("Updated local content of \"{0}\" with content of remote document {1} and created conflict file {2}", target.FullName, remoteDocument.Id, conflictFile.FullName));
            } else {
                backupFile.Delete();
                OperationsLogger.Info(string.Format("Updated local content of \"{0}\" with content of remote document {1}", target.FullName, remoteDocument.Id));
            }

            transmissionEvent.ReportProgress(new TransmissionProgressEventArgs { Completed = true });
            return hash;
        }
    }
}
