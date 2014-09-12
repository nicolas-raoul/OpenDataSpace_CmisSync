//-----------------------------------------------------------------------
// <copyright file="CreatedChangedDeletedFileSystemEventHandlerTest.cs" company="GRAU DATA AG">
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
#if ! __COCOA__
namespace TestLibrary.ProducerTests.WatcherTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Timers;

    using CmisSync.Lib.Events;
    using CmisSync.Lib.Producer.Watcher;
    using CmisSync.Lib.Queueing;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.FileSystem;

    using Moq;

    using NUnit.Framework;

    using TestUtils;

    [TestFixture]
    public class CreatedChangedDeletedFileSystemEventHandlerTest : IsTestWithConfiguredLog4Net
    {
        private static readonly string Name = "Cat";
        private static readonly string Directory = Path.GetTempPath();
        private static readonly long Threshold = 100;
        private string path;
        private Mock<ISyncEventQueue> queue;
        private Mock<IMetaDataStorage> storage;
        private Mock<IFileSystemInfoFactory> fsFactory;

        [SetUp]
        public void SetUp() {
            this.queue = new Mock<ISyncEventQueue>();
            this.storage = new Mock<IMetaDataStorage>();
            this.fsFactory = new Mock<IFileSystemInfoFactory>();
            this.path = Path.Combine(Directory, Name);
        }

        [Test, Category("Fast")]
        public void ConstructorTakesQueueAndStorageAndFileSystemInfoFactory() {
            using (new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
            }
        }

        [Test, Category("Fast")]
        public void ConstructorTakesQueueAndStorage() {
            using (new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object)) {
            }
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfQueueIsNull() {
            using (new CreatedChangedDeletedFileSystemEventHandler(null, this.storage.Object)) {
            }
        }

        [Test, Category("Fast")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfStorageIsNull() {
            using (new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, null)) {
            }
        }

        [Test, Category("Medium")]
        public void HandlesFileCreatedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                this.fsFactory.AddFile(this.path, true);
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Created)), Times.Once());
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandlesTwoFileCreatedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                this.fsFactory.AddFile(this.path, true);
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Created)), Times.Exactly(2));
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandlesCreatedAndChangedEventsInOrder() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                this.fsFactory.AddFile(this.path, true);
                this.queue.Setup(q => q.AddEvent(It.Is<FSEvent>(e => e.Type != WatcherChangeTypes.Created))).Throws<ArgumentException>();
                this.queue.Setup(q => q.AddEvent(It.Is<FSEvent>(e => e.Type == WatcherChangeTypes.Created))).Callback(
                    () => {
                    this.queue.Setup(q => q.AddEvent(It.Is<FSEvent>(e => e.Type == WatcherChangeTypes.Created))).Throws<ArgumentException>();
                    this.queue.Setup(q => q.AddEvent(It.Is<FSEvent>(e => e.Type == WatcherChangeTypes.Changed)));
                });

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Created)), Times.Exactly(1));
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Changed)), Times.Exactly(3));
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandlesFileChangedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                this.fsFactory.AddFile(this.path, Guid.Empty, true);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));
                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Changed)), Times.Once());
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandlesFileDeletedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object, Threshold)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)null);
                var file = this.fsFactory.AddFile(this.path, false);
                this.storage.AddLocalFile(file.Object.FullName, "id", Guid.NewGuid());

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never);
                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.LocalPath == this.path && e.Name == Name && e.Type == WatcherChangeTypes.Deleted)));
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandlesTwoFileDeletedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object, Threshold)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)null);
                var file = this.fsFactory.AddFile(this.path, false);
                this.storage.AddLocalFile(file.Object.FullName, "id", Guid.NewGuid());

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never);
                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.LocalPath == this.path && e.Name == Name && e.Type == WatcherChangeTypes.Deleted)), Times.Exactly(2));
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void IgnoresEventOnNonExistingPath() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)null);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());
            }
        }

        [Test, Category("Medium")]
        public void IgnoresDeletionOfPathWithNoEntryInStorage() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());
            }
        }

        [Test, Category("Medium")]
        public void HandlesFolderCreatedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)true);
                this.fsFactory.AddDirectory(this.path, true);
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == true && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Created)), Times.Once());
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void AggregatesFolderDeletedAndCreatedEventToFSMovedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                string newName = "new";
                string oldName = "old";
                Guid guid = Guid.NewGuid();
                string newPath = Path.Combine(this.path, newName);
                string oldPath = Path.Combine(Directory, oldName);
                this.fsFactory.Setup(f => f.IsDirectory(newPath)).Returns((bool?)true);
                this.fsFactory.AddDirectory(newPath, guid, true);
                this.storage.AddLocalFolder(oldPath, "id", guid);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, oldName));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, this.path, newName));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSMovedEvent>(e => e.IsDirectory == true && e.Name == newName && e.OldPath == oldPath && e.LocalPath == newPath)), Times.Once());

                this.WaitForThreshold();
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSMovedEvent>();
            }
        }

        [Test, Category("Medium")]
        public void DoesNotAggregateFolderChangedEventsToFSMovedEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                Guid guid = Guid.NewGuid();
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)true);

                this.fsFactory.AddDirectory(this.path, guid, true);
                this.storage.AddLocalFolder(this.path, "id", guid);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.IsAny<FSMovedEvent>()), Times.Never());
            }
        }

        [Test, Category("Medium")]
        public void AggregatesFolderDeletedAndCreatedEventToFSMovedEventIfTheyOccurInDifferentOrder() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                string newName = "new";
                string oldName = "old";
                Guid guid = Guid.NewGuid();
                string newPath = Path.Combine(this.path, newName);
                string oldPath = Path.Combine(Directory, oldName);
                this.fsFactory.Setup(f => f.IsDirectory(newPath)).Returns((bool?)true);
                this.fsFactory.AddDirectory(newPath, guid, true);
                this.storage.AddLocalFolder(oldPath, "id", guid);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, this.path, newName));
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, oldName));

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSMovedEvent>(e => e.IsDirectory == true && e.Name == newName && e.OldPath == oldPath && e.LocalPath == newPath)), Times.Once());

                this.WaitForThreshold();
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSMovedEvent>();
            }
        }

        [Test, Category("Medium")]
        public void TestThatCalculateIntervalAlwaysReturnsPositiveNumbers() {
            using (var underTest = new HandlerMockWithoutTimerAction(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)null);
                var file = this.fsFactory.AddFile(this.path, false);
                this.storage.AddLocalFile(file.Object.FullName, "id", Guid.NewGuid());

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));
                System.Threading.Thread.Sleep(20);
                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Directory, Name));

                // negativ numbers lead to ArgumentException so this would break here
            }
        }

        [Test, Category("Medium")]
        public void HandleExceptionsOnProcessingByInvokingCrawlSync() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Throws(new Exception("Generic exception"));

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.queue.Verify(q => q.AddEvent(It.Is<StartNextSyncEvent>(e => e.FullSyncRequested == true)));
                this.queue.VerifyThatNoOtherEventIsAddedThan<StartNextSyncEvent>();
                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<StartNextSyncEvent>(e => e.FullSyncRequested == true)));
                this.queue.VerifyThatNoOtherEventIsAddedThan<StartNextSyncEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandleExceptionsOnUuidReadingByJustPassingEmptyUuid() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                var filemock = this.fsFactory.AddFile(this.path, Guid.Empty, true);
                filemock.Setup(f => f.Uuid).Throws<ExtendedAttributeException>();

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));

                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never);
                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(e => e.IsDirectory == false && e.Name == Name && e.LocalPath == this.path && e.Type == WatcherChangeTypes.Created)), Times.Once());
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        [Test, Category("Medium")]
        public void HandleExceptionsOnTransformingByInvokingCrawlSync() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                this.fsFactory.AddFile(this.path, Guid.Empty, true);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never());

                this.fsFactory.Setup(f => f.CreateDirectoryInfo(It.IsAny<string>())).Throws(new Exception("Generic exception"));
                this.fsFactory.Setup(f => f.CreateFileInfo(It.IsAny<string>())).Throws(new Exception("Generic exception"));

                this.WaitForThreshold();
                this.queue.VerifyThatNoOtherEventIsAddedThan<StartNextSyncEvent>();
                this.queue.Verify(q => q.AddEvent(It.Is<StartNextSyncEvent>(e => e.FullSyncRequested == true)));
            }
        }

        [Test, Category("Medium")]
        public void HandleFileNotFoundExceptionOnExtendedAttributeByJustIgnoringTheEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)false);
                var fileInfo = this.fsFactory.AddFile(this.path, Guid.Empty, true);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                fileInfo.Setup(f => f.Uuid).Throws(new FileNotFoundException());

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never);
            }
        }

        [Test, Category("Medium")]
        public void HandleDirectoryNotFoundExceptionOnExtendedAttributeByJustIgnoringTheEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)true);
                var dirInfo = this.fsFactory.AddDirectory(this.path, Guid.Empty, true);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Created, Directory, Name));
                dirInfo.Setup(f => f.Uuid).Throws(new DirectoryNotFoundException());

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.IsAny<ISyncEvent>()), Times.Never);
            }
        }

        [Test, Category("Medium")]
        public void HandleChangeEventOnNoMoreExistingFileOrFolderByJustPassingTheEvent() {
            using (var underTest = new CreatedChangedDeletedFileSystemEventHandler(this.queue.Object, this.storage.Object, this.fsFactory.Object)) {
                this.fsFactory.Setup(f => f.IsDirectory(this.path)).Returns((bool?)true);
                var dirInfo = this.fsFactory.AddDirectory(this.path, Guid.Empty, true);

                underTest.Handle(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Directory, Name));
                dirInfo.Setup(d => d.Exists).Returns(false);

                this.WaitForThreshold();
                this.queue.Verify(q => q.AddEvent(It.Is<FSEvent>(f => f.IsDirectory == true && f.LocalPath == this.path && f.Name == Name && f.Type == WatcherChangeTypes.Changed)));
                this.queue.VerifyThatNoOtherEventIsAddedThan<FSEvent>();
            }
        }

        private void WaitForThreshold() {
            System.Threading.Thread.Sleep((int)Threshold * 3);
        }

        private class HandlerMockWithoutTimerAction : CreatedChangedDeletedFileSystemEventHandler {
            public HandlerMockWithoutTimerAction(
                ISyncEventQueue queue,
                IMetaDataStorage storage,
                IFileSystemInfoFactory fsFactory) : base(queue, storage, fsFactory, 10) {
                this.timer.Dispose();
                this.timer = new Timer();
                this.timer.AutoReset = false;
            }
        }
    }
}
#endif
