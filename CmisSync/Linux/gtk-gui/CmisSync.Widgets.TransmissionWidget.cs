
// This file has been generated by the GUI designer. Do not modify.
namespace CmisSync.Widgets
{
	public partial class TransmissionWidget
	{
		private global::Gtk.EventBox eventBox;
		
		private global::Gtk.HBox mainBox;
		
		private global::Gtk.Image fileTypeImage;
		
		private global::Gtk.VBox midbox;
		
		private global::Gtk.Label fileNameLabel;
		
		private global::Gtk.ProgressBar transmissionProgressBar;
		
		private global::Gtk.HBox statusBox;
		
		private global::Gtk.Label statusDetailsLabel;
		
		private global::Gtk.Label bandwidthLabel;
		
		private global::Gtk.Label repoLabel;
		
		private global::Gtk.Label lastModificationLabel;
		
		private global::Gtk.Button openFileInFolderButton;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget CmisSync.Widgets.TransmissionWidget
			global::Stetic.BinContainer.Attach (this);
			this.CanFocus = true;
			this.Name = "CmisSync.Widgets.TransmissionWidget";
			// Container child CmisSync.Widgets.TransmissionWidget.Gtk.Container+ContainerChild
			this.eventBox = new global::Gtk.EventBox ();
			this.eventBox.CanFocus = true;
			this.eventBox.Name = "eventBox";
			// Container child eventBox.Gtk.Container+ContainerChild
			this.mainBox = new global::Gtk.HBox ();
			this.mainBox.CanFocus = true;
			this.mainBox.Name = "mainBox";
			this.mainBox.Spacing = 6;
			// Container child mainBox.Gtk.Box+BoxChild
			this.fileTypeImage = new global::Gtk.Image ();
			this.fileTypeImage.Name = "fileTypeImage";
			this.fileTypeImage.Xalign = 0F;
			this.mainBox.Add (this.fileTypeImage);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.mainBox [this.fileTypeImage]));
			w1.Position = 0;
			w1.Expand = false;
			// Container child mainBox.Gtk.Box+BoxChild
			this.midbox = new global::Gtk.VBox ();
			this.midbox.Name = "midbox";
			this.midbox.Spacing = 6;
			// Container child midbox.Gtk.Box+BoxChild
			this.fileNameLabel = new global::Gtk.Label ();
			this.fileNameLabel.Name = "fileNameLabel";
			this.fileNameLabel.Xalign = 0F;
			this.fileNameLabel.Selectable = true;
			this.midbox.Add (this.fileNameLabel);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.midbox [this.fileNameLabel]));
			w2.Position = 0;
			w2.Expand = false;
			w2.Fill = false;
			// Container child midbox.Gtk.Box+BoxChild
			this.transmissionProgressBar = new global::Gtk.ProgressBar ();
			this.transmissionProgressBar.Name = "transmissionProgressBar";
			this.midbox.Add (this.transmissionProgressBar);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.midbox [this.transmissionProgressBar]));
			w3.Position = 1;
			w3.Fill = false;
			// Container child midbox.Gtk.Box+BoxChild
			this.statusBox = new global::Gtk.HBox ();
			this.statusBox.Name = "statusBox";
			this.statusBox.Spacing = 10;
			// Container child statusBox.Gtk.Box+BoxChild
			this.statusDetailsLabel = new global::Gtk.Label ();
			this.statusDetailsLabel.Sensitive = false;
			this.statusDetailsLabel.Name = "statusDetailsLabel";
			this.statusDetailsLabel.Xalign = 0F;
			this.statusDetailsLabel.UseMarkup = true;
			this.statusDetailsLabel.Selectable = true;
			this.statusDetailsLabel.SingleLineMode = true;
			this.statusBox.Add (this.statusDetailsLabel);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.statusBox [this.statusDetailsLabel]));
			w4.Position = 0;
			w4.Expand = false;
			w4.Fill = false;
			// Container child statusBox.Gtk.Box+BoxChild
			this.bandwidthLabel = new global::Gtk.Label ();
			this.bandwidthLabel.Sensitive = false;
			this.bandwidthLabel.Name = "bandwidthLabel";
			this.bandwidthLabel.UseMarkup = true;
			this.statusBox.Add (this.bandwidthLabel);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.statusBox [this.bandwidthLabel]));
			w5.Position = 2;
			w5.Expand = false;
			w5.Fill = false;
			// Container child statusBox.Gtk.Box+BoxChild
			this.repoLabel = new global::Gtk.Label ();
			this.repoLabel.Sensitive = false;
			this.repoLabel.Name = "repoLabel";
			this.repoLabel.Xalign = 0F;
			this.repoLabel.UseMarkup = true;
			this.statusBox.Add (this.repoLabel);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.statusBox [this.repoLabel]));
			w6.Position = 3;
			w6.Expand = false;
			w6.Fill = false;
			// Container child statusBox.Gtk.Box+BoxChild
			this.lastModificationLabel = new global::Gtk.Label ();
			this.lastModificationLabel.Sensitive = false;
			this.lastModificationLabel.Name = "lastModificationLabel";
			this.lastModificationLabel.Xalign = 0F;
			this.lastModificationLabel.UseMarkup = true;
			this.statusBox.Add (this.lastModificationLabel);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.statusBox [this.lastModificationLabel]));
			w7.PackType = ((global::Gtk.PackType)(1));
			w7.Position = 5;
			w7.Expand = false;
			w7.Fill = false;
			this.midbox.Add (this.statusBox);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.midbox [this.statusBox]));
			w8.Position = 2;
			w8.Expand = false;
			w8.Fill = false;
			this.mainBox.Add (this.midbox);
			global::Gtk.Box.BoxChild w9 = ((global::Gtk.Box.BoxChild)(this.mainBox [this.midbox]));
			w9.Position = 1;
			// Container child mainBox.Gtk.Box+BoxChild
			this.openFileInFolderButton = new global::Gtk.Button ();
			this.openFileInFolderButton.Name = "openFileInFolderButton";
			this.openFileInFolderButton.UseStock = true;
			this.openFileInFolderButton.UseUnderline = true;
			this.openFileInFolderButton.FocusOnClick = false;
			this.openFileInFolderButton.Relief = ((global::Gtk.ReliefStyle)(2));
			this.openFileInFolderButton.Label = "gtk-open";
			this.mainBox.Add (this.openFileInFolderButton);
			global::Gtk.Box.BoxChild w10 = ((global::Gtk.Box.BoxChild)(this.mainBox [this.openFileInFolderButton]));
			w10.Position = 2;
			w10.Expand = false;
			w10.Fill = false;
			this.eventBox.Add (this.mainBox);
			this.Add (this.eventBox);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Show ();
		}
	}
}
