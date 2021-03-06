﻿//-----------------------------------------------------------------------
// <copyright file="LocalObjectChangedRemoteObjectChanged.cs" company="GRAU DATA AG">
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
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    using CmisSync.Lib.Cmis.ConvenienceExtenders;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Storage.FileSystem;

    using DataSpace.Common.Transmissions;

    using DotCMIS.Client;
    using DotCMIS.Exceptions;

    using log4net;

    /// <summary>
    /// Local object changed and remote object changed.
    /// </summary>
    public class LocalObjectChangedRemoteObjectChanged : AbstractEnhancedSolver {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LocalObjectChangedRemoteObjectChanged));

        private ITransmissionFactory transmissionFactory;
        private IFileSystemInfoFactory fsFactory;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="CmisSync.Lib.Consumer.SituationSolver.LocalObjectChangedRemoteObjectChanged"/> class.
        /// </summary>
        /// <param name="session">Cmis session.</param>
        /// <param name="storage">Meta data storage.</param>
        /// <param name="transmissionStorage">Transmission storage.</param>
        /// <param name="transmissionFactory">Transmission factory.</param>
        /// <param name="fsFactory">File system factory.</param>
        public LocalObjectChangedRemoteObjectChanged(
            ISession session,
            IMetaDataStorage storage,
            IFileTransmissionStorage transmissionStorage,
            ITransmissionFactory transmissionFactory,
            IFileSystemInfoFactory fsFactory = null) : base(session, storage, transmissionStorage) {
            if (transmissionFactory == null) {
                throw new ArgumentNullException("transmissionFactory");
            }

            this.transmissionFactory = transmissionFactory;
            this.fsFactory = fsFactory ?? new FileSystemInfoFactory();
        }

        /// <summary>
        /// Solve the specified situation by using localFile and remote object.
        /// </summary>
        /// <param name="localFileSystemInfo">Local filesystem info instance.</param>
        /// <param name="remoteId">Remote identifier or object.</param>
        /// <param name="localContent">Hint if the local content has been changed.</param>
        /// <param name="remoteContent">Information if the remote content has been changed.</param>
        public override void Solve(
            IFileSystemInfo localFileSystemInfo,
            IObjectId remoteId,
            ContentChangeType localContent,
            ContentChangeType remoteContent)
        {
            if (remoteId == null) {
                throw new ArgumentNullException("remoteId");
            }

            var obj = this.Storage.GetObjectByRemoteId(remoteId.Id);
            if (localFileSystemInfo is IDirectoryInfo) {
                var remoteFolder = remoteId as IFolder;
                obj.LastLocalWriteTimeUtc = localFileSystemInfo.LastWriteTimeUtc;
                obj.LastRemoteWriteTimeUtc = remoteFolder.LastModificationDate;
                obj.LastChangeToken = remoteFolder.ChangeToken;
                obj.Ignored = remoteFolder.AreAllChildrenIgnored();
                localFileSystemInfo.TryToSetReadOnlyStateIfDiffers(from: remoteFolder);
                obj.IsReadOnly = localFileSystemInfo.ReadOnly;
                this.Storage.SaveMappedObject(obj);
            } else if (localFileSystemInfo is IFileInfo) {
                var fileInfo = localFileSystemInfo as IFileInfo;
                var doc = remoteId as IDocument;
                bool updateLocalDate = false;
                bool updateRemoteDate = false;
                if (remoteContent == ContentChangeType.NONE) {
                    if (fileInfo.IsContentChangedTo(obj, true)) {
                        // Upload local content
                        updateRemoteDate = true;
                        try {
                            var transmission = this.transmissionFactory.CreateTransmission(TransmissionType.UploadModifiedFile, fileInfo.FullName);
                            obj.LastChecksum = this.UploadFile(fileInfo, doc, transmission);
                            obj.LastContentSize = doc.ContentStreamLength ?? fileInfo.Length;
                        } catch (Exception ex) {
                            if (ex.InnerException is CmisPermissionDeniedException) {
                                OperationsLogger.Warn(string.Format("Local changed file \"{0}\" has not been uploaded: PermissionDenied", fileInfo.FullName), ex.InnerException);
                                return;
                            }

                            throw;
                        }
                    } else {
                        // Just date sync
                        var lastRemoteModificationDate = doc.LastModificationDate;
                        if (lastRemoteModificationDate != null && fileInfo.LastWriteTimeUtc < (DateTime)lastRemoteModificationDate) {
                            updateLocalDate = true;
                        } else {
                            updateRemoteDate = true;
                        }
                    }
                } else {
                    byte[] actualLocalHash;
                    if (fileInfo.IsContentChangedTo(obj, out actualLocalHash, true)) {
                        // Check if both are changed to the same value
                        if (actualLocalHash == null) {
                            using (var f = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                actualLocalHash = SHA1Managed.Create().ComputeHash(f);
                            }
                        }

                        byte[] remoteHash = doc.ContentStreamHash();
                        if (remoteHash != null && actualLocalHash.SequenceEqual(remoteHash)) {
                            // Both files are equal
                            obj.LastChecksum = remoteHash;
                            obj.LastContentSize = fileInfo.Length;

                            // Sync dates
                            var lastRemoteModificationDate = doc.LastModificationDate;
                            if (lastRemoteModificationDate != null && fileInfo.LastWriteTimeUtc < (DateTime)lastRemoteModificationDate) {
                                updateLocalDate = true;
                            } else {
                                updateRemoteDate = true;
                            }
                        } else {
                            // Both are different => Check modification dates
                            // Download remote version and create conflict file
                            updateLocalDate = true;
                            obj.LastChecksum = this.DownloadChanges(fileInfo, doc, obj, this.fsFactory, this.transmissionFactory, Logger);
                            obj.LastContentSize = doc.ContentStreamLength ?? 0;
                        }
                    } else {
                        // Download remote content
                        updateLocalDate = true;
                        obj.LastChecksum = this.DownloadChanges(fileInfo, doc, obj, this.fsFactory, this.transmissionFactory, Logger);
                        obj.LastContentSize = doc.ContentStreamLength ?? 0;
                    }
                }

                fileInfo.TryToSetReadOnlyStateIfDiffers(from: doc);
                if (this.ServerCanModifyDateTimes) {
                    if (updateLocalDate) {
                        fileInfo.TryToSetLastWriteTimeUtcIfAvailable(from: doc);
                    } else if (updateRemoteDate) {
                        doc.IfAllowedUpdateLastWriteTimeUtc(basedOn: fileInfo);
                    } else {
                        throw new ArgumentException("Algorithm failure");
                    }
                }

                obj.LastChangeToken = doc.ChangeToken;
                obj.LastLocalWriteTimeUtc = localFileSystemInfo.LastWriteTimeUtc;
                obj.LastRemoteWriteTimeUtc = doc.LastModificationDate;
                obj.IsReadOnly = localFileSystemInfo.ReadOnly;
                this.Storage.SaveMappedObject(obj);
            }
        }
    }
}