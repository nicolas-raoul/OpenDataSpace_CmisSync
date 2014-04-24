//-----------------------------------------------------------------------
// <copyright file="ContentChangesTest.cs" company="GRAU DATA AG">
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
using System.Collections.Generic;

using CmisSync.Lib;
using CmisSync.Lib.Config;
using CmisSync.Lib.Events;
using CmisSync.Lib.Sync.Strategy;
using CmisSync.Lib.Storage;

using DotCMIS.Client;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Binding.Services;

using NUnit.Framework;

using Moq;
using TestLibrary.TestUtils;

namespace TestLibrary.SyncStrategiesTests
{
    [TestFixture]
    public class ContentChangesTest
    {

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }
        private readonly bool isPropertyChangesSupported = false;
        private readonly string changeLogToken = "token";
        private readonly string latestChangeLogToken = "latestChangeLogToken";
        private readonly string repoId = "repoId";
        private readonly int maxNumberOfContentChanges = 1000;


        [Test, Category("Fast"), Category("ContentChange")]
        public void ConstructorWithVaildEntriesTest ()
        {
            var storage = new Mock<IMetaDataStorage>();
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            bool isPropertyChangesSupported = true;
            new ContentChanges (session.Object, storage.Object, queue.Object);
            new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges);
            new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges, isPropertyChangesSupported);
            new ContentChanges (session.Object, storage.Object, queue.Object, isPropertyChangesSupported: true);
            new ContentChanges (session.Object, storage.Object, queue.Object, isPropertyChangesSupported: false);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        [ExpectedException( typeof( ArgumentNullException ) )]
        public void ConstructorFailsOnNullDbTest ()
        {
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            new ContentChanges (session.Object, null, queue.Object);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void ConstructorFailsOnInvalidMaxEventsLimitTest ()
        {
            var storage = new Mock<IMetaDataStorage>();
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            try {
                new ContentChanges (session.Object, storage.Object, queue.Object, -1);
                Assert.Fail ();
            } catch (ArgumentException) {
            }
            try {
                new ContentChanges (session.Object, storage.Object, queue.Object, 0);
                Assert.Fail ();
            } catch (ArgumentException) {
            }
            try {
                new ContentChanges (session.Object, storage.Object, queue.Object, 1);
                Assert.Fail ();
            } catch (ArgumentException) {
            }
        }

        [Test, Category("Fast"), Category("ContentChange")]
        [ExpectedException( typeof( ArgumentNullException ) )]
        public void ConstructorFailsOnNullSessionTest ()
        {
            var storage = new Mock<IMetaDataStorage>();
            var queue = new Mock<ISyncEventQueue>();
            new ContentChanges (null, storage.Object, queue.Object);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        [ExpectedException( typeof( ArgumentNullException ) )]
        public void ConstructorFailsOnNullQueueTest ()
        {
            var storage = new Mock<IMetaDataStorage>();
            var session = new Mock<ISession> ().Object;
            new ContentChanges (session, storage.Object, null);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void IgnoreWrongEventTest ()
        {
            var storage = new Mock<IMetaDataStorage>();
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            var startSyncEvent = new Mock<ISyncEvent>().Object ;
            Assert.IsFalse (changes.Handle (startSyncEvent));
        }


        [Test, Category("Fast"), Category("ContentChange")]
        public void RetrunFalseOnError () {
            //TODO: this might not be the best behavior, this test verifies the current implementation not the desired one
            var storage = new Mock<IMetaDataStorage>();
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            session.Setup(x => x.Binding).Throws(new Exception("SOME EXCEPTION"));
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            var wrongEvent = new Mock<ISyncEvent> ().Object;
            Assert.IsFalse (changes.Handle (wrongEvent));
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void HandleFullSyncCompletedEventTest ()
        {
            var startSyncEvent = new StartNextSyncEvent (false);
            startSyncEvent.SetParam (ContentChanges.FULL_SYNC_PARAM_NAME, changeLogToken);
            var completedEvent = new FullSyncCompletedEvent (startSyncEvent);
            var storage = new Mock<IMetaDataStorage>();
            storage.SetupProperty(db => db.ChangeLogToken);
            var queue = new Mock<ISyncEventQueue>();
            var session = new Mock<ISession>();
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            Assert.IsFalse (changes.Handle (completedEvent));
            storage.VerifySet(db => db.ChangeLogToken = changeLogToken);
            Assert.AreEqual (changeLogToken, storage.Object.ChangeLogToken);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void HandleStartSyncEventOnNoRemoteChangeTest ()
        {
            var startSyncEvent = new StartNextSyncEvent (false);
            var session = new Mock<ISession>();
            session.SetupSessionDefaultValues();
            session.Setup (s => s.Binding.GetRepositoryService ().GetRepositoryInfo (repoId, null).LatestChangeLogToken).Returns (changeLogToken);
            var storage = new Mock<IMetaDataStorage>();
            storage.Setup (db => db.ChangeLogToken).Returns (changeLogToken);
            var queue = new Mock<ISyncEventQueue>();
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            Assert.IsTrue (changes.Handle (startSyncEvent));
        }


        [Test, Category("Fast"), Category("ContentChange")]
        public void ExecuteCrawlSyncOnNoLocalTokenAvailableTest ()
        {
            ISyncEvent queuedEvent = null;
            var startSyncEvent = new StartNextSyncEvent (false);
            var session = new Mock<ISession>();
            session.SetupSessionDefaultValues();
            session.Setup (s => s.Binding.GetRepositoryService ().GetRepositoryInfo (repoId, null).LatestChangeLogToken).Returns (changeLogToken);
            var storage = new Mock<IMetaDataStorage>();
            storage.Setup (db => db.ChangeLogToken).Returns ((string)null);
            int handled = 0;
            var queue = new Mock<ISyncEventQueue>();
            queue.Setup (q => q.AddEvent (It.IsAny<ISyncEvent> ())).Callback<ISyncEvent> ((e) => {
                handled ++;
                queuedEvent = e;
            }
            );
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            Assert.IsTrue (changes.Handle (startSyncEvent));
            Assert.AreEqual (1, handled);
            Assert.NotNull (queuedEvent);
            Assert.IsTrue (queuedEvent is StartNextSyncEvent);
            Assert.IsTrue (((StartNextSyncEvent)queuedEvent).FullSyncRequested);
        }


        [Test, Category("Fast"), Category("ContentChange")]
        public void IgnoreCrawlSyncEventTest ()
        {
            var start = new StartNextSyncEvent (true);
            var repositoryService = new Mock<IRepositoryService> ();
            repositoryService.Setup (r => r.GetRepositoryInfos (null)).Returns ((IList<IRepositoryInfo>)null);
            repositoryService.Setup (r => r.GetRepositoryInfo (It.IsAny<string> (), It.IsAny<IExtensionsData> ()).LatestChangeLogToken).Returns (latestChangeLogToken);
            var session = new Mock<ISession>();
            session.Setup (s => s.Binding.GetRepositoryService ()).Returns (repositoryService.Object);
            session.Setup (s => s.RepositoryInfo.Id).Returns (repoId);
            var storage = new Mock<IMetaDataStorage>();
            storage.Setup (db => db.ChangeLogToken ).Returns (changeLogToken);
            var queue = new Mock<ISyncEventQueue>();
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            Assert.IsFalse (changes.Handle (start));
            string result;
            Assert.IsFalse (start.TryGetParam (ContentChanges.FULL_SYNC_PARAM_NAME, out result));
            Assert.IsNull (result);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void ExtendCrawlSyncEventTest ()
        {
            var start = new StartNextSyncEvent (true);
            var repositoryService = new Mock<IRepositoryService> ();
            repositoryService.Setup (r => r.GetRepositoryInfos (null)).Returns ((IList<IRepositoryInfo>)null);
            repositoryService.Setup (r => r.GetRepositoryInfo (It.IsAny<string> (), It.IsAny<IExtensionsData> ()).LatestChangeLogToken).Returns (latestChangeLogToken);
            var session = new Mock<ISession>();
            session.Setup (s => s.Binding.GetRepositoryService ()).Returns (repositoryService.Object);
            session.Setup (s => s.RepositoryInfo.Id).Returns (repoId);
            var manager = new Mock<SyncEventManager> ("").Object;
            var storage = new Mock<IMetaDataStorage>();
            storage.Setup (db => db.ChangeLogToken ).Returns ((string)null);
            var queue = new Mock<ISyncEventQueue>();
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object);
            Assert.IsFalse (changes.Handle (start));
            string result;
            Assert.IsTrue (start.TryGetParam (ContentChanges.FULL_SYNC_PARAM_NAME, out result));
            Assert.AreEqual (latestChangeLogToken, result);
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void GivesCorrectContentChangeEvent ()
        {
            ContentChangeEvent csEvent = null;
            var queue = new Mock<ISyncEventQueue>();
            queue.Setup(q => q.AddEvent (It.IsAny<ContentChangeEvent>())).Callback ((ISyncEvent f) => {
                    csEvent = f as ContentChangeEvent;
                }
            );
            string id = "myId";

            Mock<IMetaDataStorage> storage = MockMetaDataStorageUtil.GetMetaStorageMockWithToken();
            var session = MockSessionUtil.PrepareSessionMockForSingleChange(DotCMIS.Enums.ChangeType.Created,id);
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges, isPropertyChangesSupported);

            var startSyncEvent = new StartNextSyncEvent (false);
            Assert.IsTrue (changes.Handle (startSyncEvent));

            queue.Verify(foo => foo.AddEvent(It.IsAny<ContentChangeEvent>()), Times.Once());
            Assert.That(csEvent.Type, Is.EqualTo(DotCMIS.Enums.ChangeType.Created));
            Assert.That(csEvent.ObjectId, Is.EqualTo(id));

        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void PagingTest ()
        {
            var queue = new Mock<ISyncEventQueue>();

            Mock<IMetaDataStorage> storage = MockMetaDataStorageUtil.GetMetaStorageMockWithToken();

            Mock<ISession> session = MockSessionUtil.GetSessionMockReturning3Changesin2Batches();

            var startSyncEvent = new StartNextSyncEvent (false);
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges, isPropertyChangesSupported);

            Assert.IsTrue (changes.Handle (startSyncEvent));
            queue.Verify(foo => foo.AddEvent(It.IsAny<ContentChangeEvent>()), Times.Exactly(3));
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void IgnoreDuplicatedContentChangesEventTestCreated ()
        {
            var queue = new Mock<ISyncEventQueue>();

            Mock<IMetaDataStorage> storage = MockMetaDataStorageUtil.GetMetaStorageMockWithToken();

            Mock<ISession> session = MockSessionUtil.GetSessionMockReturning3Changesin2Batches(DotCMIS.Enums.ChangeType.Created, true);

            var startSyncEvent = new StartNextSyncEvent (false);
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges, isPropertyChangesSupported);

            Assert.IsTrue (changes.Handle (startSyncEvent));
            queue.Verify(foo => foo.AddEvent(It.IsAny<ContentChangeEvent>()), Times.Exactly(3));
        }

        [Test, Category("Fast"), Category("ContentChange")]
        public void IgnoreDuplicatedContentChangesEventTestDeleted ()
        {
            var queue = new Mock<ISyncEventQueue>();

            Mock<IMetaDataStorage> storage = MockMetaDataStorageUtil.GetMetaStorageMockWithToken();

            Mock<ISession> session = MockSessionUtil.GetSessionMockReturning3Changesin2Batches(DotCMIS.Enums.ChangeType.Deleted, true);

            var startSyncEvent = new StartNextSyncEvent (false);
            var changes = new ContentChanges (session.Object, storage.Object, queue.Object, maxNumberOfContentChanges, isPropertyChangesSupported);

            Assert.IsTrue (changes.Handle (startSyncEvent));
            queue.Verify(foo => foo.AddEvent(It.IsAny<ContentChangeEvent>()), Times.Exactly(3));
        }
    }
}

