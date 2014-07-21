//-----------------------------------------------------------------------
// <copyright file="SyncStrategyInitializerTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.QueueingTests
{
    using System;
    using System.IO;

    using CmisSync.Lib;
    using CmisSync.Lib.Accumulator;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Config;
    using CmisSync.Lib.Consumer;
    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Filter;
    using CmisSync.Lib.Storage.FileSystem;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Producer.ContentChange;
    using CmisSync.Lib.Producer.Crawler;
    using CmisSync.Lib.Producer.Watcher;

    using DotCMIS.Client;
    using DotCMIS.Enums;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.IntegrationTests;
    using TestLibrary.TestUtils;

    [TestFixture]
    public class EventManagerInitializerTest
    {
        private ActivityListenerAggregator listener;


        [SetUp]
        public void SetUp() {
            this.listener = new ActivityListenerAggregator(Mock.Of<IActivityListener>(), new ActiveActivitiesManager());
        }

        [Test, Category("Fast")]
        public void ConstructorTakesQueueAndManagerAndStorage()
        {
            new EventManagerInitializer(Mock.Of<ISyncEventQueue>(), Mock.Of<IMetaDataStorage>(), CreateRepoInfo(), MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, this.listener);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfQueueIsNull()
        {
            new EventManagerInitializer(null, Mock.Of<IMetaDataStorage>(), CreateRepoInfo(), MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, this.listener);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfStorageIsNull()
        {
            new EventManagerInitializer(Mock.Of<ISyncEventQueue>(), null, CreateRepoInfo(), MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, this.listener);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfRepoInfoIsNull()
        {
            new EventManagerInitializer(Mock.Of<ISyncEventQueue>(), Mock.Of<IMetaDataStorage>(), null, MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, this.listener);
        }

        [Test, Category("Fast")]
        public void IgnoresWrongEventsTest()
        {
            var queue = new Mock<ISyncEventQueue>();
            var storage = new Mock<IMetaDataStorage>();
            var handler = new EventManagerInitializer(queue.Object, storage.Object, CreateRepoInfo(), MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, this.listener);

            var e = new Mock<ISyncEvent>();
            Assert.False(handler.Handle(e.Object));
        }

        [Test, Category("Fast")]
        public void RootFolderGetsAddedToStorage()
        {
            string id = "id";
            string token = "token";
            var storage = new Mock<IMetaDataStorage>();
            var manager = new Mock<ISyncEventManager>();
            RunSuccessfulLoginEvent(
                storage: storage.Object,
                manager: manager.Object,
                listener: this.listener,
                id: id,
                token: token);

            MappedObject rootObject = new MappedObject("/", id, MappedObjectType.Folder, null, token);
            storage.Verify(s => s.SaveMappedObject(It.Is<MappedObject>(m => AssertMappedObjectEqualExceptGUID(rootObject, m))));
        }

        [Test, Category("Fast")]
        public void HandlersAddedInitiallyWithoutContentChanges()
        {
            var storage = new Mock<IMetaDataStorage>();
            var manager = new Mock<ISyncEventManager>();
            RunSuccessfulLoginEvent(
                storage: storage.Object,
                manager: manager.Object,
                listener: this.listener,
                changeEventSupported: false);

            manager.Verify(m => m.AddEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(4));
            VerifyNonContenChangeHandlersAdded(manager, Times.Once());
            VerifyContenChangeHandlersAdded(manager, Times.Never());
        }

        [Test, Category("Fast")]
        public void HandlersAddedInitiallyWithContentChanges()
        {
            var storage = new Mock<IMetaDataStorage>();
            var manager = new Mock<ISyncEventManager>();
            RunSuccessfulLoginEvent(
                storage: storage.Object,
                manager: manager.Object,
                listener: this.listener,
                changeEventSupported: true);

            manager.Verify(m => m.AddEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(7));
            VerifyNonContenChangeHandlersAdded(manager, Times.Once());
            VerifyContenChangeHandlersAdded(manager, Times.Once());
        }

        [Test, Category("Fast")]
        public void ReinitiallizationContentChangeBeforeAndAfter()
        {
            var storage = new Mock<IMetaDataStorage>();
            var manager = new Mock<ISyncEventManager>();

            var handler = CreateStrategyInitializer(storage.Object, manager.Object, this.listener);

            var e = CreateNewSessionEvent(changeEventSupported: true);
            handler.Handle(e);

            manager.Verify(m => m.RemoveEventHandler(It.IsAny<SyncEventHandler>()), Times.Never());

            handler.Handle(e);

            manager.Verify(m => m.AddEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(14));
            VerifyNonContenChangeHandlersAdded(manager, Times.Exactly(2));
            VerifyContenChangeHandlersAdded(manager, Times.Exactly(2));
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(7));
            VerifyNonContenChangeHandlersRemoved(manager, Times.Once());
            VerifyContenChangeHandlersRemoved(manager, Times.Once());
        }

        [Test, Category("Fast")]
        public void ReinitiallizationContentChangeSupportAdded()
        {
            var storage = new Mock<IMetaDataStorage>();
            var manager = new Mock<ISyncEventManager>();

            var handler = CreateStrategyInitializer(storage.Object, manager.Object, this.listener);

            var e = CreateNewSessionEvent(changeEventSupported: false);
            handler.Handle(e);

            manager.Verify(m => m.RemoveEventHandler(It.IsAny<SyncEventHandler>()), Times.Never());

            e = CreateNewSessionEvent(changeEventSupported: true);
            handler.Handle(e);

            manager.Verify(m => m.AddEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(11));
            VerifyNonContenChangeHandlersAdded(manager, Times.Exactly(2));
            VerifyContenChangeHandlersAdded(manager, Times.Exactly(1));
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<SyncEventHandler>()), Times.Exactly(4));
            VerifyNonContenChangeHandlersRemoved(manager, Times.Exactly(1));
            VerifyContenChangeHandlersRemoved(manager, Times.Never());
        }

        private static RepoInfo CreateRepoInfo()
        {
            return new RepoInfo
            {
                Address = new Uri("http://example.com"),
                LocalPath = ITUtils.GetConfig()[1].ToString(),
                RemotePath = "/"
            };
        }

        private static SuccessfulLoginEvent CreateNewSessionEvent(bool changeEventSupported, string id = "i", string token = "t")
        {
            var session = new Mock<ISession>();
            var remoteObject = new Mock<IFolder>();
            remoteObject.Setup(r => r.Id).Returns(id);
            remoteObject.Setup(r => r.ChangeToken).Returns(token);

            session.Setup(s => s.GetObjectByPath(It.IsAny<string>())).Returns(remoteObject.Object);
            if (changeEventSupported)
            {
                session.Setup(s => s.RepositoryInfo.Capabilities.ChangesCapability).Returns(CapabilityChanges.All);
            }

            return new SuccessfulLoginEvent(new Uri("http://example.com"), session.Object);
        }

        private static EventManagerInitializer CreateStrategyInitializer(IMetaDataStorage storage, ISyncEventManager manager, ActivityListenerAggregator listener)
        {
            var queue = new Mock<ISyncEventQueue>();
            queue.Setup(s => s.EventManager).Returns(manager);

            return new EventManagerInitializer(queue.Object, storage, CreateRepoInfo(), MockOfIFilterAggregatorUtil.CreateFilterAggregator().Object, listener);
        }

        private static void VerifyNonContenChangeHandlersAdded(Mock<ISyncEventManager> manager, Times times)
        {
            manager.Verify(m => m.AddEventHandler(It.IsAny<DescendantsCrawler>()), times);
            manager.Verify(m => m.AddEventHandler(It.IsAny<RemoteObjectFetcher>()), times);
            manager.Verify(m => m.AddEventHandler(It.IsAny<SyncMechanism>()), times);
            manager.Verify(m => m.AddEventHandler(It.IsAny<RemoteObjectMovedOrRenamedAccumulator>()), times);
        }

        private static void VerifyContenChangeHandlersAdded(Mock<ISyncEventManager> manager, Times times)
        {
            manager.Verify(m => m.AddEventHandler(It.IsAny<ContentChanges>()), times);
            manager.Verify(m => m.AddEventHandler(It.IsAny<ContentChangeEventAccumulator>()), times);
            manager.Verify(m => m.AddEventHandler(It.IsAny<IgnoreAlreadyHandledContentChangeEventsFilter>()), times);
        }

        private static void VerifyNonContenChangeHandlersRemoved(Mock<ISyncEventManager> manager, Times times)
        {
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<DescendantsCrawler>()), times);
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<RemoteObjectFetcher>()), times);
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<SyncMechanism>()), times);
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<RemoteObjectMovedOrRenamedAccumulator>()), times);
        }

        private static void VerifyContenChangeHandlersRemoved(Mock<ISyncEventManager> manager, Times times)
        {
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<ContentChanges>()), times);
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<ContentChangeEventAccumulator>()), times);
            manager.Verify(m => m.RemoveEventHandler(It.IsAny<IgnoreAlreadyHandledContentChangeEventsFilter>()), times);
        }

        private static void RunSuccessfulLoginEvent(IMetaDataStorage storage, ISyncEventManager manager, ActivityListenerAggregator listener, bool changeEventSupported = false, string id = "i", string token = "t")
        {
            var e = CreateNewSessionEvent(changeEventSupported, id, token);

            var handler = CreateStrategyInitializer(storage, manager, listener);

            Assert.True(handler.Handle(e));
        }

        private static bool AssertMappedObjectEqualExceptGUID(IMappedObject expected, IMappedObject actual) {
            Assert.AreEqual(actual.ParentId, expected.ParentId);
            Assert.AreEqual(actual.Type, expected.Type);
            Assert.AreEqual(actual.RemoteObjectId, expected.RemoteObjectId);
            Assert.AreEqual(actual.LastChangeToken, expected.LastChangeToken);
            Assert.AreEqual(actual.LastRemoteWriteTimeUtc, expected.LastRemoteWriteTimeUtc);
            Assert.AreEqual(actual.LastLocalWriteTimeUtc, expected.LastLocalWriteTimeUtc);
            Assert.AreEqual(actual.LastChecksum, expected.LastChecksum);
            Assert.AreEqual(actual.ChecksumAlgorithmName, expected.ChecksumAlgorithmName);
            Assert.AreEqual(actual.Name, expected.Name);
            Assert.AreNotEqual(actual.Guid, Guid.Empty);
            return true;
        }
    }
}