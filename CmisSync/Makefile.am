
if HAVE_MAC_OS
  SUBDIRS = Common Mac TestLibrary
else
  SUBDIRS = Common Linux TestLibrary
endif

EXTRA_DIST =  \
	Program.cs \
	AboutController.cs \
	BubblesController.cs \
	ControllerBase.cs \
	EditController.cs \
	SetupController.cs \
	StatusIconController.cs \
	CertPolicyHandler.cs \
	Properties

install-data-hook:
	test -f $(datadir)/applications/defaults.list && \
	test -z $(DESTDIR) && update-desktop-database $(datadir)/applications || :

MAINTAINERCLEAN_FILES = Makefile.in
