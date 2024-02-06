﻿using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models {
    public interface IAvatarHost {
        void OnAvatarResourceReady(string md5, Bitmap bitmap);
    }

    public static class AvatarManager {
        static AvatarManager() {
            _storePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SourceGit", "avatars");
            if (!Directory.Exists(_storePath)) Directory.CreateDirectory(_storePath);

            Task.Run(() => {
                while (true) {
                    var md5 = null as string;

                    lock (_synclock) {
                        foreach (var one in _requesting) {
                            md5 = one;
                            break;
                        }
                    }

                    if (md5 == null) {
                        Thread.Sleep(100);
                        continue;
                    }

                    var localFile = Path.Combine(_storePath, md5);
                    var img = null as Bitmap;
                    try {
                        var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(2) };
                        var task = client.GetAsync($"https://cravatar.cn/avatar/{md5}?d=404");
                        task.Wait();

                        var rsp = task.Result;
                        if (rsp.IsSuccessStatusCode) {
                            using (var stream = rsp.Content.ReadAsStream()) {
                                using (var writer = File.OpenWrite(localFile)) {
                                    stream.CopyTo(writer);
                                }
                            }

                            using (var reader = File.OpenRead(localFile)) {
                                img = Bitmap.DecodeToWidth(reader, 128);
                            }
                        }
                    } catch { }

                    lock (_synclock) {
                        _requesting.Remove(md5);
                    }

                    Dispatcher.UIThread.InvokeAsync(() => {
                        if (_resources.ContainsKey(md5)) _resources[md5] = img;
                        else _resources.Add(md5, img);
                        if (img != null) NotifyResourceReady(md5, img);
                    });
                }
            });
        }

        public static void Subscribe(IAvatarHost host) {
            _avatars.Add(new WeakReference<IAvatarHost>(host));
        }

        public static Bitmap Request(string md5, bool forceRefetch = false) {
            if (forceRefetch) {
                if (_resources.ContainsKey(md5)) _resources.Remove(md5);                
            } else {
                if (_resources.ContainsKey(md5)) return _resources[md5];

                var localFile = Path.Combine(_storePath, md5);
                if (File.Exists(localFile)) {
                    try {
                        using (var stream = File.OpenRead(localFile)) {
                            var img = Bitmap.DecodeToWidth(stream, 128);
                            _resources.Add(md5, img);
                            return img;
                        }
                    } catch { }
                }
            }

            lock (_synclock) {
                if (!_requesting.Contains(md5)) _requesting.Add(md5);
            }

            return null;
        }

        private static void NotifyResourceReady(string md5, Bitmap bitmap) {
            List<WeakReference<IAvatarHost>> invalids = new List<WeakReference<IAvatarHost>>();
            foreach (var avatar in _avatars) {
                IAvatarHost retrived = null;
                if (avatar.TryGetTarget(out retrived)) {
                    retrived.OnAvatarResourceReady(md5, bitmap);
                    break;
                } else {
                    invalids.Add(avatar);
                }
            }

            foreach (var invalid in invalids) _avatars.Remove(invalid);
        }

        private static object _synclock = new object();
        private static string _storePath = string.Empty;
        private static List<WeakReference<IAvatarHost>> _avatars = new List<WeakReference<IAvatarHost>>();
        private static Dictionary<string, Bitmap> _resources = new Dictionary<string, Bitmap>();
        private static HashSet<string> _requesting = new HashSet<string>();
    }
}
