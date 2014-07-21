//-----------------------------------------------------------------------
// <copyright file="LocalObjectChangedTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.ConsumerTests.SituationSolverTests
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.FileSystem;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Consumer.SituationSolver;

    using DotCMIS.Client;
    using DotCMIS.Data;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.TestUtils;

    [TestFixture]
    public class LocalObjectChangedTest
    {
        private Mock<ActiveActivitiesManager> manager;

        [SetUp]
        public void SetUp() {
            this.manager = new Mock<ActiveActivitiesManager>() {
                CallBase = true
            };
        }

        [Test, Category("Fast"), Category("Solver")]
        public void DefaultConstructorTest()
        {
            new LocalObjectChanged(Mock.Of<ISyncEventQueue>(), this.manager.Object);
        }

        [Test, Category("Fast"), Category("Solver")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfQueueIsNull() {
            new LocalObjectChanged(null, this.manager.Object);
        }

        [Test, Category("Fast"), Category("Solver")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfTransmissionManagerIsNull() {
            new LocalObjectChanged(Mock.Of<ISyncEventQueue>(), null);
        }

        [Test, Category("Fast"), Category("Solver")]
        public void LocalFolderChanged()
        {
            var modificationDate = DateTime.UtcNow;
            var storage = new Mock<IMetaDataStorage>();
            var localDirectory = Mock.Of<IDirectoryInfo>(
                f =>
                f.LastWriteTimeUtc == modificationDate.AddMinutes(1));
            var queue = new Mock<ISyncEventQueue>();

            var mappedObject = new MappedObject(
                "name",
                "remoteId",
                MappedObjectType.Folder,
                "parentId",
                "changeToken")
            {
                Guid = Guid.NewGuid(),
                LastRemoteWriteTimeUtc = modificationDate.AddMinutes(1)
            };
            storage.AddMappedFolder(mappedObject);

            new LocalObjectChanged(queue.Object, this.manager.Object).Solve(Mock.Of<ISession>(), storage.Object, localDirectory, Mock.Of<IFolder>());

            storage.VerifySavedMappedObject(
                MappedObjectType.Folder,
                "remoteId",
                mappedObject.Name,
                mappedObject.ParentId,
                mappedObject.LastChangeToken,
                true,
                localDirectory.LastWriteTimeUtc);
            queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());
            this.manager.Verify(m => m.AddTransmission(It.IsAny<FileTransmissionEvent>()), Times.Never());
        }

        [Test, Category("Fast"), Category("Solver")]
        public void LocalFileModificationDateNotWritableShallNotThrow()
        {
            var modificationDate = DateTime.UtcNow;
            var newModificationDate = modificationDate.AddHours(1);
            var newChangeToken = "newChangeToken";
            var storage = new Mock<IMetaDataStorage>();
            int fileLength = 20;
            byte[] content = new byte[fileLength];
            var queue = new Mock<ISyncEventQueue>();

            var localFile = new Mock<IFileInfo>();
            localFile.SetupProperty(f => f.LastWriteTimeUtc, modificationDate.AddMinutes(1));
            localFile.SetupSet(f => f.LastWriteTimeUtc = It.IsAny<DateTime>()).Throws(new IOException());
            localFile.Setup(f => f.Length).Returns(fileLength);
            localFile.Setup(f => f.FullName).Returns("path");
            using (var uploadedContent = new MemoryStream()) {
                localFile.Setup(
                    f =>
                    f.Open(FileMode.Open, FileAccess.Read, FileShare.Read)).Returns(() => { return new MemoryStream(content); });

                var mappedObject = new MappedObject(
                    "name",
                    "remoteId",
                    MappedObjectType.File,
                    "parentId",
                    "changeToken",
                    fileLength)
                {
                    Guid = Guid.NewGuid(),
                    LastRemoteWriteTimeUtc = modificationDate.AddMinutes(1),
                    LastLocalWriteTimeUtc = modificationDate,
                    LastChecksum = new byte[20],
                    ChecksumAlgorithmName = "SHA1"
                };
                storage.AddMappedFile(mappedObject, "path");
                var remoteFile = MockOfIDocumentUtil.CreateRemoteDocumentMock(null, "remoteId", "name", "parentId", fileLength, new byte[20]);
                remoteFile.Setup(r => r.SetContentStream(It.IsAny<IContentStream>(), true, true)).Callback<IContentStream, bool, bool>(
                    (s, o, r) =>
                    { s.Stream.CopyTo(uploadedContent);
                    remoteFile.Setup(f => f.LastModificationDate).Returns(newModificationDate);
                    remoteFile.Setup(f => f.ChangeToken).Returns(newChangeToken);
                });

                new LocalObjectChanged(queue.Object, this.manager.Object).Solve(Mock.Of<ISession>(), storage.Object, localFile.Object, remoteFile.Object);
            }
            this.manager.Verify(m => m.AddTransmission(It.IsAny<FileTransmissionEvent>()), Times.Once());
        }

        [Test, Category("Fast"), Category("Solver")]
        public void LocalFileModificationDateChanged()
        {
            string path = "path";
            var modificationDate = DateTime.UtcNow;
            var storage = new Mock<IMetaDataStorage>();
            int fileLength = 20;
            byte[] content = new byte[fileLength];
            byte[] expectedHash = SHA1Managed.Create().ComputeHash(content);
            var queue = new Mock<ISyncEventQueue>();
            var localFile = new Mock<IFileInfo>();
            localFile.SetupProperty(f => f.LastWriteTimeUtc, modificationDate.AddMinutes(1));
            localFile.Setup(f => f.Length).Returns(fileLength);
            localFile.Setup(f => f.FullName).Returns(path);
            using (var stream = new MemoryStream(content)) {
                localFile.Setup(
                    f =>
                    f.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read)).Returns(stream);

                var mappedObject = new MappedObject(
                    "name",
                    "remoteId",
                    MappedObjectType.File,
                    "parentId",
                    "changeToken",
                    fileLength)
                {
                    Guid = Guid.NewGuid(),
                    LastRemoteWriteTimeUtc = modificationDate.AddMinutes(1),
                    LastLocalWriteTimeUtc = modificationDate,
                    LastChecksum = expectedHash,
                    ChecksumAlgorithmName = "SHA1"
                };

                storage.AddMappedFile(mappedObject, path);

                new LocalObjectChanged(Mock.Of<ISyncEventQueue>(), this.manager.Object).Solve(Mock.Of<ISession>(), storage.Object, localFile.Object, Mock.Of<IDocument>());

                storage.VerifySavedMappedObject(
                    MappedObjectType.File,
                    "remoteId",
                    mappedObject.Name,
                    mappedObject.ParentId,
                    mappedObject.LastChangeToken,
                    true,
                    localFile.Object.LastWriteTimeUtc,
                    expectedHash,
                    fileLength);
                queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());
                this.manager.Verify(m => m.AddTransmission(It.IsAny<FileTransmissionEvent>()), Times.Never());
            }
        }

        [Test, Category("Fast"), Category("Solver")]
        public void LocalFileContentChanged()
        {
            var modificationDate = DateTime.UtcNow;
            var newModificationDate = modificationDate.AddHours(1);
            var newChangeToken = "newChangeToken";
            var storage = new Mock<IMetaDataStorage>();
            int fileLength = 20;
            byte[] content = new byte[fileLength];
            byte[] expectedHash = SHA1Managed.Create().ComputeHash(content);
            var queue = new Mock<ISyncEventQueue>();

            var localFile = new Mock<IFileInfo>();
            localFile.SetupProperty(f => f.LastWriteTimeUtc, modificationDate.AddMinutes(1));
            localFile.Setup(f => f.Length).Returns(fileLength);
            localFile.Setup(f => f.FullName).Returns("path");
            using (var uploadedContent = new MemoryStream()) {
                localFile.Setup(
                    f =>
                    f.Open(FileMode.Open, FileAccess.Read, FileShare.Read)).Returns(() => { return new MemoryStream(content); });

                var mappedObject = new MappedObject(
                    "name",
                    "remoteId",
                    MappedObjectType.File,
                    "parentId",
                    "changeToken",
                    fileLength)
                {
                    Guid = Guid.NewGuid(),
                    LastRemoteWriteTimeUtc = modificationDate.AddMinutes(1),
                    LastLocalWriteTimeUtc = modificationDate,
                    LastChecksum = new byte[20],
                    ChecksumAlgorithmName = "SHA1"
                };
                storage.AddMappedFile(mappedObject, "path");
                var remoteFile = MockOfIDocumentUtil.CreateRemoteDocumentMock(null, "remoteId", "name", "parentId", fileLength, new byte[20]);
                remoteFile.Setup(r => r.SetContentStream(It.IsAny<IContentStream>(), true, true)).Callback<IContentStream, bool, bool>(
                    (s, o, r) =>
                    { s.Stream.CopyTo(uploadedContent);
                    remoteFile.Setup(f => f.LastModificationDate).Returns(newModificationDate);
                    remoteFile.Setup(f => f.ChangeToken).Returns(newChangeToken);
                });

                new LocalObjectChanged(queue.Object, this.manager.Object).Solve(Mock.Of<ISession>(), storage.Object, localFile.Object, remoteFile.Object);

                storage.VerifySavedMappedObject(
                    MappedObjectType.File,
                    "remoteId",
                    mappedObject.Name,
                    mappedObject.ParentId,
                    newChangeToken,
                    true,
                    localFile.Object.LastWriteTimeUtc,
                    expectedHash,
                    fileLength);
                remoteFile.VerifySetContentStream();
                queue.Verify(q => q.AddEvent(It.Is<FileTransmissionEvent>(e => e.Path == localFile.Object.FullName && e.Type == FileTransmissionType.UPLOAD_MODIFIED_FILE)), Times.Once());
                Assert.That(uploadedContent.ToArray(), Is.EqualTo(content));
                Assert.That(localFile.Object.LastWriteTimeUtc, Is.EqualTo(newModificationDate));
                this.manager.Verify(m => m.AddTransmission(It.IsAny<FileTransmissionEvent>()), Times.Once());
            }
        }
    }
}