//-----------------------------------------------------------------------
// <copyright file="CreateDocumentTests.cs" company="GRAU DATA AG">
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

namespace TestLibrary.IntegrationTests.PrivateWorkingCopyTests {
    using System;
    using System.Linq;

    using CmisSync.Lib.Cmis.ConvenienceExtenders;

    using DotCMIS.Client;

    using NUnit.Framework;

    using TestUtils;

    [TestFixture, Timeout(180000), TestName("PWC")]
    public class CreateDocumentTests : BaseFullRepoTest {
        private readonly string fileName = "fileName.bin";
        private readonly string content = "content";

        [Test, Category("Slow"), MaxTime(180000)]
        public void CreateCheckedOutDocument() {
            this.EnsureThatPrivateWorkingCopySupportIsAvailable();

            var doc = this.remoteRootDir.CreateDocument(this.fileName, (string)null, checkedOut: true);
            this.remoteRootDir.Refresh();
            doc.SetContent(this.content);
            var newObjectId = doc.CheckIn(true, null, null, string.Empty);
            var newDocument = this.session.GetObject(newObjectId) as IDocument;

            this.remoteRootDir.Refresh();
            Assert.That(this.remoteRootDir.GetChildren().First().Name, Is.EqualTo(this.fileName));
            Assert.That(newDocument.Name, Is.EqualTo(this.fileName));
            Assert.That(newDocument.ContentStreamLength, Is.EqualTo(this.content.Length));
        }

        [Test, Category("Slow"), MaxTime(180000)]
        public void CreateCheckedOutDocumentAndCancelCheckout([Values(true, false)]bool settingContent) {
            this.EnsureThatPrivateWorkingCopySupportIsAvailable();

            var doc = this.remoteRootDir.CreateDocument(this.fileName, (string)null, checkedOut: true);
            if (settingContent) {
                doc.SetContent(this.content);
            }

            doc.CancelCheckOut();

            this.remoteRootDir.Refresh();
            Assert.That(this.remoteRootDir.GetChildren().TotalNumItems, Is.EqualTo(0));
        }

        [Test, Category("Slow"), MaxTime(180000)]
        public void CreateCheckedOutDocumentAndDoNotCheckIn() {
            this.EnsureThatPrivateWorkingCopySupportIsAvailable();
            this.remoteRootDir.CreateDocument(this.fileName, (string)null, checkedOut: true);
            this.remoteRootDir.Refresh();
            Assert.That(this.remoteRootDir.GetChildren().TotalNumItems, Is.EqualTo(0));
        }

        private void EnsureThatPrivateWorkingCopySupportIsAvailable() {
            if (!this.session.ArePrivateWorkingCopySupported()) {
                Assert.Ignore("This session does not support updates on private working copies");
            }
        }
    }
}