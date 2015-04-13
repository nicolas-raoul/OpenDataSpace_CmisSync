﻿
using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync {
    public partial class TransmissionWidgetItem : MonoMac.AppKit.NSTableCellView {
        #region Constructors

        // Called when created from unmanaged code
        public TransmissionWidgetItem(IntPtr handle) : base(handle) {
            Initialize();
        }
        
        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public TransmissionWidgetItem(NSCoder coder) : base(coder) {
            Initialize();
        }

        public TransmissionWidgetItem() {
        }

        // Shared initialization code
        void Initialize() {
        }

        #endregion
    }
}

