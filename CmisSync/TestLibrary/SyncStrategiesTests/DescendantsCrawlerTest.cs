//-----------------------------------------------------------------------
// <copyright file="DescendantsCrawlerTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.SyncStrategiesTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using CmisSync.Lib.Data;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Storage;
    using CmisSync.Lib.Sync.Strategy;

    using DBreeze;

    using DotCMIS.Client;

    using Moq;

    using Newtonsoft.Json;

    using NUnit.Framework;

    using TestUtils;

    [TestFixture]
    public class DescendantsCrawlerTest
    {
        private readonly string remoteRootId = "rootId";
        private readonly string remoteRootPath = "/";
        private readonly Guid rootGuid = Guid.NewGuid();
        private Mock<ISyncEventQueue> queue;
        private IMetaDataStorage storage;
        private Mock<IFolder> remoteFolder;
        private Mock<IDirectoryInfo> localFolder;
        private Mock<IFileSystemInfoFactory> fsFactory;
        private string localRootPath;
        private MappedObject mappedRootObject;
        private IPathMatcher matcher;
        private DBreezeEngine storageEngine;
        private DateTime lastLocalWriteTime = DateTime.Now;

        [TestFixtureSetUp]
        public void InitCustomSerializator()
        {
            // Use Newtonsoft.Json as Serializator
            DBreeze.Utils.CustomSerializator.Serializator = JsonConvert.SerializeObject; 
            DBreeze.Utils.CustomSerializator.Deserializator = JsonConvert.DeserializeObject;
        }

        [SetUp]
        public void CreateMockObjects()
        {
            this.storageEngine = new DBreezeEngine(new DBreezeConfiguration { Storage = DBreezeConfiguration.eStorage.MEMORY });
            this.localRootPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            this.matcher = new PathMatcher(this.localRootPath, this.remoteRootPath);
            this.queue = new Mock<ISyncEventQueue>();
            this.remoteFolder = MockOfIFolderUtil.CreateRemoteFolderMock(this.remoteRootId, this.remoteRootPath, this.remoteRootPath);
            this.remoteFolder.SetupDescendants();
            this.localFolder = new Mock<IDirectoryInfo>();
            this.localFolder.Setup(f => f.FullName).Returns(this.localRootPath);
            this.localFolder.Setup(f => f.Exists).Returns(true);
            this.localFolder.Setup(f => f.IsExtendedAttributeAvailable()).Returns(true);
            this.localFolder.Setup(f => f.GetExtendedAttribute(MappedObject.ExtendedAttributeKey)).Returns(this.rootGuid.ToString());
            this.localFolder.Setup(f => f.LastWriteTimeUtc).Returns(this.lastLocalWriteTime);
            this.fsFactory = new Mock<IFileSystemInfoFactory>();
            this.fsFactory.AddIDirectoryInfo(this.localFolder.Object);
            this.mappedRootObject = new MappedObject(
                this.remoteRootPath,
                this.remoteRootId,
                MappedObjectType.Folder,
                null,
                "changeToken") {
                Guid = rootGuid,
                LastLocalWriteTimeUtc = this.lastLocalWriteTime
            };
            this.storage = new MetaDataStorage(this.storageEngine, this.matcher);
            this.storage.SaveMappedObject(this.mappedRootObject);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfLocalFolderIsNull()
        {
            new DescendantsCrawler(Mock.Of<ISyncEventQueue>(), Mock.Of<IFolder>(), null, Mock.Of<IMetaDataStorage>());
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfRemoteFolderIsNull()
        {
            new DescendantsCrawler(Mock.Of<ISyncEventQueue>(), null, Mock.Of<IDirectoryInfo>(), Mock.Of<IMetaDataStorage>());
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfQueueIsNull()
        {
            new DescendantsCrawler(null, Mock.Of<IFolder>(), Mock.Of<IDirectoryInfo>(), Mock.Of<IMetaDataStorage>());
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfStorageIsNull()
        {
            new DescendantsCrawler(Mock.Of<ISyncEventQueue>(), Mock.Of<IFolder>(), Mock.Of<IDirectoryInfo>(), null);
        }

        [Test, Category("Fast")]
        public void ConstructorWorksWithoutFsInfoFactory()
        {
            new DescendantsCrawler(Mock.Of<ISyncEventQueue>(), Mock.Of<IFolder>(), Mock.Of<IDirectoryInfo>(), Mock.Of<IMetaDataStorage>());
        }

        [Test, Category("Fast")]
        public void ConstructorTakesFsInfoFactory()
        {
            this.CreateCrawler();
        }

        [Test, Category("Fast")]
        public void PriorityIsNormal()
        {
            var crawler = this.CreateCrawler();
            Assert.That(crawler.Priority == EventHandlerPriorities.NORMAL);
        }

        [Test, Category("Fast")]
        public void IgnoresNonFittingEvents()
        {
            var crawler = this.CreateCrawler();
            Assert.That(crawler.Handle(Mock.Of<ISyncEvent>()), Is.False);
            this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());
        }

        [Test, Category("Fast")]
        public void HandlesStartNextSyncEventAndReportsOnQueueIfDone()
        {
            var crawler = this.CreateCrawler();
            var startEvent = new StartNextSyncEvent();
            Assert.That(crawler.Handle(startEvent), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FullSyncCompletedEvent>(e => e.StartEvent.Equals(startEvent))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneRemoteFolderAdded()
        {
            IFolder newRemoteFolder = MockOfIFolderUtil.CreateRemoteFolderMock("id", "name", "/name", this.remoteRootId).Object;
            this.remoteFolder.SetupDescendants(newRemoteFolder);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FolderEvent>(e => e.RemoteFolder.Equals(newRemoteFolder))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneRemoteDocumentAdded()
        {
            IDocument newRemoteDocument = MockOfIDocumentUtil.CreateRemoteDocumentMock(null, "id", "name", this.remoteRootId).Object;
            this.remoteFolder.SetupDescendants(newRemoteDocument);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FileEvent>(e => e.RemoteFile.Equals(newRemoteDocument))), Times.Once());
        }

        [Test, Category("Fast")]
        public void SimpleRemoteFolderHierarchyAdded()
        {
            var newRemoteSubFolder = MockOfIFolderUtil.CreateRemoteFolderMock("remoteSubFolder", "sub", "/name/sub", "remoteFolder");
            var newRemoteFolder = MockOfIFolderUtil.CreateRemoteFolderMock("remoteFolder", "name", "/name", this.remoteRootId);
            newRemoteFolder.SetupDescendants(newRemoteSubFolder.Object);
            this.remoteFolder.SetupDescendants(newRemoteFolder.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FolderEvent>(e => e.RemoteFolder.Equals(newRemoteFolder.Object))), Times.Once());
            this.queue.Verify(q => q.AddEvent(It.Is<FolderEvent>(e => e.RemoteFolder.Equals(newRemoteSubFolder.Object))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneLocalFolderAdded()
        {
            var newFolderMock = this.fsFactory.AddDirectory(Path.Combine(this.localRootPath, "newFolder"));
            this.localFolder.SetupDirectories(newFolderMock.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FolderEvent>(e => e.LocalFolder.Equals(newFolderMock.Object))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneLocalFileAdded()
        {
            var newFileMock = this.fsFactory.AddFile(Path.Combine(this.localRootPath, "newFile"));
            this.localFolder.SetupFiles(newFileMock.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FileEvent>(e => e.LocalFile.Equals(newFileMock.Object))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneLocalFileRemoved()
        {
            var oldLocalFile = this.fsFactory.AddFile(Path.Combine(this.localRootPath, "oldFile"));
            oldLocalFile.Setup(f => f.Exists).Returns(false);
            var remoteFile = MockOfIDocumentUtil.CreateRemoteDocumentMock(null, "oldFileId", "oldFile", this.remoteRootId, changeToken: "oldChange");
            var storedFile = new MappedObject("oldFile", "oldFileId", MappedObjectType.File, this.remoteRootId, "oldChange") {
                Guid = Guid.NewGuid()
            };
            this.storage.SaveMappedObject(storedFile);

            this.remoteFolder.SetupDescendants(remoteFile.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FileEvent>(e => e.LocalFile != null && e.LocalFile.Equals(oldLocalFile.Object) && e.Local.Equals(MetaDataChangeType.DELETED))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneLocalFolderRemoved()
        {
            var oldLocalFolder = this.fsFactory.AddDirectory(Path.Combine(this.localRootPath, "oldFolder"));
            oldLocalFolder.Setup(f => f.Exists).Returns(false);
            var remoteSubFolder = MockOfIFolderUtil.CreateRemoteFolderMock("oldFolderId", "oldFolder", remoteRootPath + "oldFolder" , remoteRootId, "oldChange");
            var storedFolder = new MappedObject("oldFolder", "oldFolderId", MappedObjectType.Folder, this.remoteRootId, "oldChange") {
                Guid = Guid.NewGuid()
            };
            this.storage.SaveMappedObject(storedFolder);
            this.remoteFolder.SetupDescendants(remoteSubFolder.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<FolderEvent>(e => e.LocalFolder != null && e.LocalFolder.Equals(oldLocalFolder.Object))), Times.Once());
        }

        [Test, Category("Fast")]
        public void OneLocalFolderRenamed()
        {
            var uuid = Guid.NewGuid();
            var oldLocalFolder = this.fsFactory.AddDirectory(Path.Combine(this.localRootPath, "oldFolder"));
            var newLocalFolder = this.fsFactory.AddDirectory(Path.Combine(this.localRootPath, "newFolder"));
            oldLocalFolder.Setup(f => f.Exists).Returns(false);
            newLocalFolder.Setup(f => f.GetExtendedAttribute(It.IsAny<string>())).Returns(uuid.ToString());
            var remoteSubFolder = MockOfIFolderUtil.CreateRemoteFolderMock("oldFolderId", "oldFolder", remoteRootPath + "oldFolder" , remoteRootId, "oldChange");
            var storedFolder = new MappedObject("oldFolder", "oldFolderId", MappedObjectType.Folder, this.remoteRootId, "oldChange") {
                Guid = uuid
            };
            this.storage.SaveMappedObject(storedFolder);
            this.remoteFolder.SetupDescendants(remoteSubFolder.Object);

            this.localFolder.SetupDirectories(newLocalFolder.Object);
            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(
                q =>
                q.AddEvent(
                It.Is<FolderMovedEvent>(
                e =>
                e.LocalFolder.Equals(newLocalFolder.Object) &&
                e.RemoteFolder.Equals(remoteSubFolder.Object) &&
                e.OldLocalFolder.Equals(oldLocalFolder.Object))),
                Times.Once());
        }

        [Test, Category("Fast")]
        public void NoChangeOnExistingFileAndFolder()
        {
            DateTime changeTime = DateTime.UtcNow;
            string changeToken = "token";
            string fileName = "name";
            string fileId = "fileId";
            string folderName = "folder";
            string folderId = "folderId";
            Guid folderGuid = Guid.NewGuid();
            Guid fileGuid = Guid.NewGuid();
            var remoteFile = MockOfIDocumentUtil.CreateRemoteDocumentMock(null, fileId, fileName, this.remoteRootId, changeToken: changeToken);
            var existingRemoteFolder = MockOfIFolderUtil.CreateRemoteFolderMock(folderId, folderName, this.remoteRootPath + folderName, this.remoteRootId, changeToken);
            var file = this.fsFactory.AddFile(Path.Combine(this.localRootPath, fileName), fileGuid);
            file.Setup(f => f.LastWriteTimeUtc).Returns(changeTime);
            var storedFile = new MappedObject(fileName, fileId, MappedObjectType.File, this.remoteRootId, changeToken) { Guid = fileGuid, LastLocalWriteTimeUtc = changeTime };
            this.storage.SaveMappedObject(storedFile);
            var folder = this.fsFactory.AddDirectory(Path.Combine(this.localRootPath, folderName), folderGuid);
            folder.Setup(f => f.LastWriteTimeUtc).Returns(changeTime);
            var storedFolder = new MappedObject(folderName, folderId, MappedObjectType.Folder, this.remoteRootId, changeToken) { Guid = folderGuid, LastLocalWriteTimeUtc = changeTime };
            this.storage.SaveMappedObject(storedFolder);
            this.remoteFolder.SetupDescendants(remoteFile.Object, existingRemoteFolder.Object);
            this.localFolder.SetupFilesAndDirectories(file.Object, folder.Object);

            var crawler = this.CreateCrawler();

            Assert.That(crawler.Handle(new StartNextSyncEvent()), Is.True);
            this.queue.Verify(q => q.AddEvent(It.Is<AbstractFolderEvent>(e => e.Local != MetaDataChangeType.NONE && e.Remote != MetaDataChangeType.NONE)), Times.Never());
        }

        private DescendantsCrawler CreateCrawler()
        {
            return new DescendantsCrawler(
                this.queue.Object,
                this.remoteFolder.Object,
                this.localFolder.Object,
                this.storage,
                this.fsFactory.Object);
        }
    }
}