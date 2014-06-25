//-----------------------------------------------------------------------
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

namespace TestLibrary.SyncStrategiesTests
{
    using System;
    using System.IO;

    using CmisSync.Lib.Data;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Storage;
    using CmisSync.Lib.Sync.Solver;
    using CmisSync.Lib.Sync.Strategy;

    using DotCMIS.Client;
    using DotCMIS.Client.Impl;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.TestUtils;

    [TestFixture]
    public class SyncMechanismTest
    {
        private Mock<ISession> session;
        private Mock<ISyncEventQueue> queue;
        private Mock<IMetaDataStorage> storage;

        [SetUp]
        public void SetUp()
        {
            this.session = new Mock<ISession>();
            this.queue = new Mock<ISyncEventQueue>();
            this.storage = new Mock<IMetaDataStorage>();
        }

        [Test, Category("Fast")]
        public void ConstructorWorksWithValidInput()
        {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var mechanism = new SyncMechanism(localDetection.Object, remoteDetection.Object, this.queue.Object, this.session.Object, this.storage.Object);
            Assert.AreEqual(localDetection.Object, mechanism.LocalSituation);
            Assert.AreEqual(remoteDetection.Object, mechanism.RemoteSituation);
            Assert.AreEqual(Math.Pow(Enum.GetNames(typeof(SituationType)).Length, 2), mechanism.Solver.Length);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorFailsWithLocalDetectionNull()
        {
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            new SyncMechanism(null, remoteDetection.Object, this.queue.Object, this.session.Object, this.storage.Object);
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorFailsWithRemoteDetectionNull()
        {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            new SyncMechanism(localDetection.Object, null, this.queue.Object, this.session.Object, this.storage.Object);
        }

        [Test, Category("Fast")]
        public void ConstructorForTestWorksWithValidInput()
        {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var mechanism = new SyncMechanism(localDetection.Object, remoteDetection.Object, this.queue.Object, this.session.Object, this.storage.Object, solver);
            Assert.AreEqual(localDetection.Object, mechanism.LocalSituation);
            Assert.AreEqual(remoteDetection.Object, mechanism.RemoteSituation);
            Assert.AreEqual(Math.Pow(Enum.GetNames(typeof(SituationType)).Length, 2), mechanism.Solver.Length);
            Assert.AreEqual(solver, mechanism.Solver);
        }

        [Test, Category("Fast")]
        public void ChooseCorrectSolverForNoChange()
        {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            int numberOfSolver = Enum.GetNames(typeof(SituationType)).Length;
            IObjectId remoteId = new ObjectId("RemoteId");
            string path = "path";
            ISolver[,] solver = new ISolver[numberOfSolver, numberOfSolver];
            var noChangeSolver = new Mock<ISolver>();
            noChangeSolver.Setup(s => s.Solve(
                It.IsAny<ISession>(),
                It.IsAny<IMetaDataStorage>(),
                It.IsAny<IFileSystemInfo>(),
                It.Is<IObjectId>(id => id == remoteId)));
            localDetection.Setup(d => d.Analyse(
                It.Is<IMetaDataStorage>(db => db == this.storage.Object),
                It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            remoteDetection.Setup(d => d.Analyse(
                It.Is<IMetaDataStorage>(db => db == this.storage.Object),
                It.IsAny<AbstractFolderEvent>())).Returns(SituationType.NOCHANGE);
            solver[(int)SituationType.NOCHANGE, (int)SituationType.NOCHANGE] = noChangeSolver.Object;
            var mechanism = new SyncMechanism(
                localDetection.Object,
                remoteDetection.Object,
                this.queue.Object,
                this.session.Object,
                this.storage.Object,
                solver);
            var remoteDoc = new Mock<IDocument>();
            remoteDoc.Setup(doc => doc.Id).Returns(remoteId.Id);
            var noChangeEvent = new Mock<FileEvent>(new FileInfoWrapper(new FileInfo(path)), remoteDoc.Object) { CallBase = true }.Object;
            Assert.True(mechanism.Handle(noChangeEvent));
            noChangeSolver.Verify(
                s => s.Solve(
                It.Is<ISession>(se => se == this.session.Object),
                It.IsAny<IMetaDataStorage>(),
                It.IsAny<IFileSystemInfo>(),
                It.Is<IObjectId>(id => id.Id == remoteId.Id)),
                Times.Once());
        }

        [Test, Category("Fast")]
        public void IgnoreNonFileOrFolderEvents()
        {
            var localDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var remoteDetection = new Mock<ISituationDetection<AbstractFolderEvent>>();
            var mechanism = new SyncMechanism(localDetection.Object, remoteDetection.Object, this.queue.Object, this.session.Object, this.storage.Object);
            var invalidEvent = new Mock<ISyncEvent>().Object;
            Assert.IsFalse(mechanism.Handle(invalidEvent));
            localDetection.Verify(d => d.Analyse(It.IsAny<IMetaDataStorage>(), It.IsAny<AbstractFolderEvent>()), Times.Never());
            remoteDetection.Verify(d => d.Analyse(It.IsAny<IMetaDataStorage>(), It.IsAny<AbstractFolderEvent>()), Times.Never());
        }

        [Test, Category("Fast"), Category("IT")]
        public void RemoteFolderAddedSituation()
        {
            var remoteFolder = Mock.Of<IFolder>(f =>
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

            var mechanism = new SyncMechanism(localDetection, remoteDetection, this.queue.Object, this.session.Object, this.storage.Object);
            mechanism.Solver[(int)SituationType.NOCHANGE, (int)SituationType.ADDED] = remoteFolderAddedSolver.Object;

            Assert.IsTrue(mechanism.Handle(folderEvent));

            remoteFolderAddedSolver.Verify(
                s => s.Solve(
                It.Is<ISession>(session => session == this.session.Object),
                It.Is<IMetaDataStorage>(storage => storage == this.storage.Object),
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>()),
                Times.Once());
        }

        [Test, Category("Fast"), Category("IT")]
        public void LocalFolderAddedSituation()
        {
            var localFolder = Mock.Of<IDirectoryInfo>();
            var localFolderAddedSolver = new Mock<ISolver>();
            var localDetection = new LocalSituationDetection();
            var remoteDetection = new RemoteSituationDetection();
            var folderEvent = new FolderEvent(localFolder: localFolder) { Local = MetaDataChangeType.CREATED, Remote = MetaDataChangeType.NONE };

            var mechanism = new SyncMechanism(localDetection, remoteDetection, this.queue.Object, this.session.Object, this.storage.Object);
            mechanism.Solver[(int)SituationType.ADDED, (int)SituationType.NOCHANGE] = localFolderAddedSolver.Object;

            Assert.IsTrue(mechanism.Handle(folderEvent));

            localFolderAddedSolver.Verify(
                s => s.Solve(
                It.Is<ISession>(session => session == this.session.Object),
                It.Is<IMetaDataStorage>(storage => storage == this.storage.Object),
                It.IsAny<IFileSystemInfo>(),
                It.IsAny<IObjectId>()),
                Times.Once());
        }

        [Ignore]
        [Test, Category("Fast")]
        public void HandleErrorOnSolver()
        {
            // If a solver fails to solve the situation, the situation should be rescanned and if it not changed an Exception Event should be published on Queue
            Assert.Fail("TODO");
        }
    }
}
