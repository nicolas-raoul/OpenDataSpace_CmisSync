//-----------------------------------------------------------------------
// <copyright file="EventTransformerTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.SelectiveIgnoreTests
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;

    using CmisSync.Lib.Events;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.SelectiveIgnore;
    using CmisSync.Lib.Storage.FileSystem;

    using DotCMIS.Client;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.TestUtils;

    [TestFixture]
    public class EventTransformerTest
    {
        private readonly string ignoredFolderId = "ignoredId";
        private readonly string ignoredLocalPath = Path.Combine(Path.GetTempPath(), "ignoredlocalpath");
        private Mock<ISyncEventQueue> queue;
        private SelectiveIgnoreEventTransformer underTest;
        private ObservableCollection<IIgnoredEntity> ignores;

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void ContructorFailsIfQueueIsNull() {
            var ignores = new ObservableCollection<IIgnoredEntity>();
            Assert.Throws<ArgumentNullException>(() => new SelectiveIgnoreEventTransformer(ignores, null));
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void ConstructorFailsIfIgnoresAreNull() {
            Assert.Throws<ArgumentNullException>(() => new SelectiveIgnoreEventTransformer(null,  Mock.Of<ISyncEventQueue>()));
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void ContructorTakesIgnoresAndQueue() {
            var ignores = new ObservableCollection<IIgnoredEntity>();
            new SelectiveIgnoreEventTransformer(ignores, Mock.Of<ISyncEventQueue>());
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void TransformFileMovedEventToAddedEvent() {
            this.SetupMocks();
            string fileName = "file.txt";
            var oldFile = Path.Combine(this.ignoredLocalPath, fileName);
            var newFile = Path.Combine(Path.GetTempPath(), fileName);
            var moveFile = new FSMovedEvent(oldFile, newFile, false);

            Assert.That(this.underTest.Handle(moveFile), Is.True);

            this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => !e.IsDirectory && e.LocalPath == newFile && e.Type == WatcherChangeTypes.Created)), Times.Once);
            this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void TransformFSMovedEventToDeletedEvent() {
            this.SetupMocks();
            string fileName = "file.txt";
            var oldFile = Path.Combine(Path.GetTempPath(), fileName);
            var newFile = Path.Combine(this.ignoredLocalPath, fileName);
            var moveFile = new FSMovedEvent(oldFile, newFile, false);

            Assert.That(this.underTest.Handle(moveFile), Is.True);

            this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => !e.IsDirectory && e.LocalPath == oldFile && e.Type == WatcherChangeTypes.Deleted)), Times.Once);
            this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void TransformFSFolderMovedEventToAddedEvent() {
            this.SetupMocks();
            string fileName = "folder";
            var oldFile = Path.Combine(this.ignoredLocalPath, fileName);
            var newFile = Path.Combine(Path.GetTempPath(), fileName);
            var moveFile = new FSMovedEvent(oldFile, newFile, true);

            Assert.That(this.underTest.Handle(moveFile), Is.True);

            this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory && e.LocalPath == newFile && e.Type == WatcherChangeTypes.Created)), Times.Once);
            this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
        }

        [Test, Category("Fast"), Category("SelectiveIgnore")]
        public void TransformFSFolderMovedEventToDeletedEvent() {
            this.SetupMocks();
            string fileName = "folder";
            var oldFile = Path.Combine(Path.GetTempPath(), fileName);
            var newFile = Path.Combine(this.ignoredLocalPath, fileName);
            var moveFile = new FSMovedEvent(oldFile, newFile, true);

            Assert.That(this.underTest.Handle(moveFile), Is.True);

            this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory && e.LocalPath == oldFile && e.Type == WatcherChangeTypes.Deleted)), Times.Once);
            this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
        }

        private void SetupMocks() {
            this.queue = new Mock<ISyncEventQueue>();
            this.ignores = new ObservableCollection<IIgnoredEntity>();
            var ignoredEntity = Mock.Of<IIgnoredEntity>(i => i.LocalPath == this.ignoredLocalPath && i.ObjectId == this.ignoredFolderId);
            this.ignores.Add(ignoredEntity);
            this.ignores.CollectionChanged += (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Assert.Fail();
            this.underTest = new SelectiveIgnoreEventTransformer(this.ignores, this.queue.Object);
        }
    }
}