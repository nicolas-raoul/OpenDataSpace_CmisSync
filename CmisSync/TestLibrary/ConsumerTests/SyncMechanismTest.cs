﻿//-----------------------------------------------------------------------
// <copyright file="SyncMechanismTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.ConsumerTests {
    using System;
    using System.IO;

    using CmisSync.Lib;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Consumer;
    using CmisSync.Lib.Consumer.SituationSolver;
    using CmisSync.Lib.Consumer.SituationSolver.PWC;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Exceptions;
    using CmisSync.Lib.Filter;
    using CmisSync.Lib.Producer.Watcher;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Storage.FileSystem;

    using DataSpace.Common.Transmissions;

    using DotCMIS.Client;
    using DotCMIS.Client.Impl;
    using DotCMIS.Exceptions;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.TestUtils;

    [TestFixture]
    public class SyncMechanismTest {
        private Mock<ISession> session;
        private Mock<ISyncEventQueue> queue;
        private Mock<IMetaDataStorage> storage;
        private Mock<IFileTransmissionStorage> fileTransmissionStorage;
        private ActivityListenerAggregator listener;
        private Mock<IActivityListener> activityListener;
        private Mock<IFilterAggregator> filters;
        private ITransmissionFactory transmissionFactory;

        [SetUp]
        public void SetUp() {
            this.session = new Mock<ISession>(MockBehavior.Strict);
            this.queue = new Mock<ISyncEventQueue>();
            this.storage = new Mock<IMetaDataStorage>();
            this.fileTransmissionStorage = new Mock<IFileTransmissionStorage>();
            this.activityListener = new Mock<IActivityListener>();
            var manager = new TransmissionManager();
            this.transmissionFactory = manager.CreateFactory();
            this.listener = new ActivityListenerAggregator(this.activityListener.Object, manager);
            this.filters = new Mock<IFilterAggregator>();
        }

        [Test, Category("Fast")]
        public void ConstructorWorksWithValidInput([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported);
            Assert.AreEqual(localDetection.Object, mechanism.LocalSituation);
            Assert.AreEqual(remoteDetection.Object, mechanism.RemoteSituation);
            Assert.AreEqual(Math.Pow(Enum.GetNames(typeof(SituationType)).Length, 2), mechanism.Solver.Length);
        }

        [Test, Category("Fast")]
        public void ConstructorFailsWithLocalDetectionNull([Values(true, false)]bool pwcSupported) {
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            Assert.Throws<ArgumentNullException>(() => new SyncMechanism(
                null,
                remoteDetection.Object,
                this.queue.Object,
                this.session.Object,
                this.storage.Object,
                this.fileTransmissionStorage.Object,
                this.listener,
                this.filters.Object,
                this.transmissionFactory,
                pwcSupported));
        }

        [Test, Category("Fast")]
        public void ConstructorFailsWithRemoteDetectionNull([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            Assert.Throws<ArgumentNullException>(() => new SyncMechanism(
                localDetection.Object,
                null,
                this.queue.Object,
                this.session.Object,
                this.storage.Object,
                this.fileTransmissionStorage.Object,
                this.listener,
                this.filters.Object,
                this.transmissionFactory,
                pwcSupported));
        }

        [Test, Category("Fast")]
        public void ConstructorThrowsExceptionIfFiltersAreNull([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            Assert.Throws<ArgumentNullException>(() => new SyncMechanism(
                localDetection.Object,
                remoteDetection.Object,
                this.queue.Object,
                this.session.Object,
                this.storage.Object,
                this.fileTransmissionStorage.Object,
                this.listener,
                null,
                this.transmissionFactory,
                pwcSupported));
        }

        [Test, Category("Fast")]
        public void ConstructorThrowsExceptionIfTransmissionFactoryIsNull([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            Assert.Throws<ArgumentNullException>(() => new SyncMechanism(
                localDetection.Object,
                remoteDetection.Object,
                this.queue.Object,
                this.session.Object,
                this.storage.Object,
                this.fileTransmissionStorage.Object,
                this.listener,
                this.filters.Object,
                null,
                pwcSupported));
        }

        [Test, Category("Fast")]
        public void ConstructorForTestWorksWithValidInput([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported, solver);
            Assert.AreEqual(localDetection.Object, mechanism.LocalSituation);
            Assert.AreEqual(remoteDetection.Object, mechanism.RemoteSituation);
            Assert.AreEqual(Math.Pow(Enum.GetNames(typeof(SituationType)).Length, 2), mechanism.Solver.Length);
            Assert.AreEqual(solver, mechanism.Solver);
        }

        [Test, Category("Fast")]
        public void ChooseCorrectSolverForNoChange([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            IObjectId remoteId = new ObjectId("RemoteId");
            string path = "path";
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var noChangeSolver = new Mock<ISolver>();
            noChangeSolver.Setup(s => s.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.Is<IObjectId>(id => id == remoteId),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>()));
            localDetection.Setup(d => d.Analyse(
                It.Is<IMetaDataStorage>(db => db == this.storage.Object),
                It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            remoteDetection.Setup(d => d.Analyse(
                It.Is<IMetaDataStorage>(db => db == this.storage.Object),
                It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            solver[(int)SituationType.NOCHANGE, (int)SituationType.NOCHANGE] = noChangeSolver.Object;
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported, solver);
            var remoteDoc = new Mock<IDocument>();
            remoteDoc.Setup(doc => doc.Id).Returns(remoteId.Id);
            var noChangeEvent = new Mock<FileEvent>(new FileInfoWrapper(new FileInfo(path)), remoteDoc.Object) { CallBase = true }.Object;
            Assert.True(mechanism.Handle(noChangeEvent));
            noChangeSolver.Verify(
                s => s.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.Is<IObjectId>(id => id.Id == remoteId.Id),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>()),
                Times.Once());
            this.VerifyThatListenerIsInformed();
        }

        [Test, Category("Fast")]
        public void IgnoreNonFileOrFolderEvents([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported);
            var invalidEvent = new Mock<ISyncEvent>().Object;
            Assert.IsFalse(mechanism.Handle(invalidEvent));
            localDetection.Verify(d => d.Analyse(It.IsAny<IMetaDataStorage>(), It.IsAny<AbstractFolderEvent>()), Times.Never());
            remoteDetection.Verify(d => d.Analyse(It.IsAny<IMetaDataStorage>(), It.IsAny<AbstractFolderEvent>()), Times.Never());
        }

        [Test, Category("Fast"), Category("IT")]
        public void RemoteFolderAddedSituation([Values(true, false)]bool pwcSupported) {
            var remoteFolder = Mock.Of<IFolder>(
                f =>
                f.Id == "remoteId" &&
                f.Name == "name");
            var remoteFolderAddedSolver = new Mock<ISolver>();
            var localDetection = new LocalSituationDetection();
            var remoteDetection = new RemoteSituationDetection();
            var folderEvent = new FolderEvent(remoteFolder: remoteFolder, localFolder: new Mock<IDirectoryInfo>().Object)
            {
                Remote = MetaDataChangeType.CREATED,
                Local = MetaDataChangeType.NONE
            };

            var mechanism = this.CreateMechanism(localDetection, remoteDetection, pwcSupported);
            mechanism.Solver[(int)SituationType.NOCHANGE, (int)SituationType.ADDED] = remoteFolderAddedSolver.Object;

            Assert.IsTrue(mechanism.Handle(folderEvent));

            remoteFolderAddedSolver.Verify(
                s => s.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>(),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>()),
                Times.Once());
            this.VerifyThatListenerIsInformed();
        }

        [Test, Category("Fast"), Category("IT")]
        public void LocalFolderAddedSituation([Values(true, false)]bool pwcSupported) {
            var localFolder = Mock.Of<IDirectoryInfo>();
            var localFolderAddedSolver = new Mock<ISolver>();
            var localDetection = new LocalSituationDetection();
            var remoteDetection = new RemoteSituationDetection();
            var folderEvent = new FolderEvent(localFolder: localFolder) { Local = MetaDataChangeType.CREATED, Remote = MetaDataChangeType.NONE };

            var mechanism = this.CreateMechanism(localDetection, remoteDetection, pwcSupported);
            mechanism.Solver[(int)SituationType.ADDED, (int)SituationType.NOCHANGE] = localFolderAddedSolver.Object;

            Assert.IsTrue(mechanism.Handle(folderEvent));

            localFolderAddedSolver.Verify(
                s => s.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>(),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>()),
                Times.Once());
            this.VerifyThatListenerIsInformed();
        }

        [Test, Category("Fast")]
        public void ThrowNotImplementedOnMissingSolver([Values(true, false)]bool pwcSupported) {
            Assert.Throws<NotImplementedException>(() => this.TriggerNonExistingSolver(pwcSupported));
        }

        [Test, Category("Fast")]
        public void RequestFullSyncOnMissingSolver([Values(true, false)]bool pwcSupported) {
            try {
                this.TriggerNonExistingSolver(pwcSupported);
            } catch (Exception) {
                // Just Swallow
            }

            this.queue.Verify(q => q.AddEvent(It.Is<StartNextSyncEvent>(e => e.FullSyncRequested == true)));
        }

        [Test, Category("Fast")]
        public void AddingEventBackToQueueOnRetryExceptionInSolverAndIncrementRetryCounter([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var retryProducer = new Mock<ISolver>();
            retryProducer.Setup(
                r =>
                r.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>(),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>())).Throws(new RetryException("reason"));
            solver[(int)SituationType.NOCHANGE, (int)SituationType.NOCHANGE] = retryProducer.Object;
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported, solver);
            localDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            remoteDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            var folderEvent = new FolderEvent(Mock.Of<IDirectoryInfo>(), Mock.Of<IFolder>()) { Local = MetaDataChangeType.NONE, Remote = MetaDataChangeType.NONE };

            Assert.That(mechanism.Handle(folderEvent), Is.True);

            this.queue.Verify(q => q.AddEvent(folderEvent), Times.Once());
            Assert.That(folderEvent.RetryCount, Is.EqualTo(1));
        }

        [Test, Category("Fast")]
        public void AddingInteractionNeededEventToQueueOnInteractionNeededException([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var interactionNeededProducer = new Mock<ISolver>();
            var exception = new InteractionNeededException("reason");
            interactionNeededProducer.Setup(
                r =>
                r.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>(),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>())).Throws(exception);
            solver[(int)SituationType.NOCHANGE, (int)SituationType.NOCHANGE] = interactionNeededProducer.Object;
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported, solver);
            localDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            remoteDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            var folderEvent = new FolderEvent(Mock.Of<IDirectoryInfo>(), Mock.Of<IFolder>()) { Local = MetaDataChangeType.NONE, Remote = MetaDataChangeType.NONE };

            var ex = Assert.Throws<InteractionNeededException>(() => mechanism.Handle(folderEvent));

            Assert.That(ex, Is.EqualTo(exception));
            this.queue.Verify(q => q.AddEvent(It.Is<InteractionNeededEvent>(e => e.Exception == exception)), Times.Once());
            this.queue.VerifyThatNoOtherEventIsAddedThan<InteractionNeededEvent>();
        }

        [Test, Category("Fast")]
        public void ThrowExceptionOnCmisConnectionExceptionOccurence([Values(true, false)]bool pwcSupported) {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var interactionNeededProducer = new Mock<ISolver>();
            var exception = new CmisConnectionException("reason");
            interactionNeededProducer.Setup(
                r =>
                r.Solve(
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>(),
                It.IsAny<ContentChangeType>(),
                It.IsAny<ContentChangeType>())).Throws(exception);
            solver[(int)SituationType.NOCHANGE, (int)SituationType.NOCHANGE] = interactionNeededProducer.Object;
            var mechanism = this.CreateMechanism(localDetection.Object, remoteDetection.Object, pwcSupported, solver);
            localDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            remoteDetection.Setup(d => d.Analyse(this.storage.Object, It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            var folderEvent = new FolderEvent(Mock.Of<IDirectoryInfo>(), Mock.Of<IFolder>()) { Local = MetaDataChangeType.NONE, Remote = MetaDataChangeType.NONE };

            Assert.Throws<CmisConnectionException>(() => mechanism.Handle(folderEvent));

            this.queue.VerifyThatNoEventIsAdded();
        }

        [Test, Category("Fast")]
        public void DecorateDefaultSolverBySpecializedPWCSolverIfSessionSupportsPWC([Values(true, false)]bool pwcUpdateable) {
            var underTest = this.CreateMechanism(Mock.Of<ISituationDetection<AbstractFolderEvent>>(), Mock.Of<ISituationDetection<AbstractFolderEvent>>(), pwcUpdateable);

            if (pwcUpdateable) {
                Assert.That(underTest.Solver[(int)SituationType.ADDED, (int)SituationType.NOCHANGE] is LocalObjectAddedWithPWC);
                Assert.That(underTest.Solver[(int)SituationType.CHANGED, (int)SituationType.NOCHANGE] is LocalObjectChangedWithPWC);
                Assert.That(underTest.Solver[(int)SituationType.CHANGED, (int)SituationType.CHANGED] is LocalObjectChangedRemoteObjectChangedWithPWC);
            } else {
                Assert.That(underTest.Solver[(int)SituationType.ADDED, (int)SituationType.NOCHANGE] is LocalObjectAdded);
                Assert.That(underTest.Solver[(int)SituationType.CHANGED, (int)SituationType.NOCHANGE] is LocalObjectChanged);
                Assert.That(underTest.Solver[(int)SituationType.CHANGED, (int)SituationType.CHANGED] is LocalObjectChangedRemoteObjectChanged);
            }
        }

        private void TriggerNonExistingSolver(bool pwcSupported) {
            var detection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];

            detection.Setup(d => d.Analyse(
                It.Is<IMetaDataStorage>(db => db == this.storage.Object),
                It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);

            var mechanism = this.CreateMechanism(detection.Object, detection.Object, pwcSupported, solver);

            var localFolder = Mock.Of<IDirectoryInfo>();
            var folderEvent = new FolderEvent(localFolder: localFolder) { Local = MetaDataChangeType.NONE, Remote = MetaDataChangeType.NONE };

            mechanism.Handle(folderEvent);
        }

        private SyncMechanism CreateMechanism(
            ISituationDetection<AbstractFolderEvent> localDetection,
            ISituationDetection<AbstractFolderEvent> remoteDetection,
            bool pwcSupported,
            ISolver[,] solver = null)
        {
            if (solver != null) {
                return new SyncMechanism(
                    localDetection,
                    remoteDetection,
                    this.queue.Object,
                    this.session.Object,
                    this.storage.Object,
                    this.fileTransmissionStorage.Object,
                    this.listener,
                    this.filters.Object,
                    this.transmissionFactory,
                    pwcSupported,
                    solver);
            } else {
                return new SyncMechanism(
                    localDetection,
                    remoteDetection,
                    this.queue.Object,
                    this.session.Object,
                    this.storage.Object,
                    this.fileTransmissionStorage.Object,
                    this.listener,
                    this.filters.Object,
                    this.transmissionFactory,
                    pwcSupported);
            }
        }

        private void VerifyThatListenerIsInformed() {
            this.activityListener.Verify(l => l.ActivityStarted(), Times.Once());
            this.activityListener.Verify(l => l.ActivityStopped(), Times.Once());
        }
    }
}