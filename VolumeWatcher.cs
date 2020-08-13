using System;
using System.IO;
using System.Collections.Generic;
using MonoMac;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

namespace VimFastFind
{
    public enum VolumeWatcherEvent
    {
        DidMount,
        WillUnmount,
        DidUnmount
    }

    // volume will resemble "C:" on windows and "/" or "/Volumes/xxx" on mac
    public delegate void VolumeWatcherEventDelegate(VolumeWatcher sender, VolumeWatcherEvent evt, string volume);


    // on mac, VolumeWatcher uses NSWorkspace notifications: to get notifications,
    // your application's main thread must be in a CoreFoundation run loop.
    //
    public class VolumeWatcher : IDisposable
    {
#pragma warning disable 0414
        VolumeWatcherHelper _helper;
#pragma warning restore 0414

        public event VolumeWatcherEventDelegate VolumeChanged;

        public VolumeWatcher()
        {
            _helper = new VolumeWatcherHelper(this);
        }

        // returns the list of connected volumes.
        public static IList<string> ScanVolumes()
        {
            var list = new List<string>();
            using (NSAutoreleasePool pool = new NSAutoreleasePool())
            {
                foreach (string path in NSWorkspace.SharedWorkspace.MountedLocalVolumePaths)
                    list.Add(path);
            }
            return list;
        }

        void ev_VolumeChanged(VolumeWatcherEvent evt, string volume)
        {
            Trace("ev_VolumeChanged " + evt + ": " + volume);
            if (VolumeChanged != null)
                VolumeChanged(this, evt, volume);
        }

        void Trace(string s)
        {
            // Console.WriteLine(s);
        }

        public void Dispose()
        {
            _helper.Dispose();
        }

        [Register]
        public class VolumeWatcherHelper : NSObject
        {
            VolumeWatcher outer;
            object _disposelock = new object();
            bool _disposed;

            public VolumeWatcherHelper(VolumeWatcher vw)
            {
                outer = vw;
                OSXUtils.ApplicationHelper.ExecuteWhenLaunched(delegate
                {
                    using (NSAutoreleasePool pool = new NSAutoreleasePool ())
                    {
                        NSNotificationCenter nc = NSWorkspace.SharedWorkspace.NotificationCenter;
                        nc.AddObserver(this, new Selector("ev_VolumeDidMount:"), new NSString("NSWorkspaceDidMountNotification"), null);
                        nc.AddObserver(this, new Selector("ev_VolumeDidUnmount:"), new NSString("NSWorkspaceDidUnmountNotification"), null);
                        nc.AddObserver(this, new Selector("ev_VolumeWillUnmount:"), new NSString("NSWorkspaceWillUnmountNotification"), null);
                    }
                });
            }

            protected override void Dispose(bool disposing)
            {
                if (_disposed) return;
                lock (_disposelock)
                {
                    if (_disposed) return;
                    OSXUtils.ApplicationHelper.ExecuteWhenLaunched(delegate
                    {
                        using (NSAutoreleasePool pool = new NSAutoreleasePool ())
                            NSWorkspace.SharedWorkspace.NotificationCenter.RemoveObserver (this);
                    });
                    _disposed = true;
                }
                base.Dispose(disposing);
            }

            [Export("ev_VolumeDidMount:")]
            public void ev_VolumeDidMount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.DidMount, mntpt);
                }
            }

            [Export("ev_VolumeWillUnmount:")]
            public void ev_VolumeWillUnmount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.WillUnmount, mntpt);
                }
            }

            [Export("ev_VolumeDidUnmount:")]
            public void ev_VolumeDidUnmount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.DidUnmount, mntpt);
                }
            }
        }
    }
}
