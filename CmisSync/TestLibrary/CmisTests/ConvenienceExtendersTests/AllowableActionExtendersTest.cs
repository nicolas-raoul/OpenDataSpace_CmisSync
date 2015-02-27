//-----------------------------------------------------------------------
// <copyright file="AllowableActionExtendersTest.cs" company="GRAU DATA AG">
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
﻿
namespace TestLibrary.CmisTests.ConvenienceExtendersTests {
    using System;

    using CmisSync.Lib.Cmis.ConvenienceExtenders;

    using DotCMIS.Client;

    using Moq;

    using NUnit.Framework;

    using TestUtils;

    [TestFixture]
    public class AllowableActionExtendersTests {
        [Test, Category("Fast")]
        public void CanCreateDocument([Values(true, false)]bool readOnly) {
            var underTest = new Mock<IFolder>();
            underTest.SetupReadOnly(readOnly);
            Assert.That(underTest.Object.CanCreateDocument(), Is.EqualTo(!readOnly));
        }

        [Test, Category("Fast")]
        public void CanGetChildren([Values(true, false)]bool readOnly) {
            var underTest = new Mock<IFolder>();
            underTest.SetupReadOnly(readOnly);
            Assert.That(underTest.Object.CanGetChildren(), Is.True);
        }

        [Test, Category("Fast")]
        public void CanGetChildrenIfNoActionIsAvailable() {
            var underTest = new Mock<IFolder>();
            Assert.That(underTest.Object.CanGetChildren(), Is.Null);
        }

        [Test, Category("Fast")]
        public void CanDeleteObject([Values(true, false)]bool readOnly) {
            var underTest = new Mock<IDocument>();
            underTest.SetupReadOnly(readOnly);
            Assert.That(underTest.Object.CanDeleteObject(), Is.EqualTo(!readOnly));
        }
    }
}