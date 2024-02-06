﻿using Avalonia.Collections;
using System;

namespace SourceGit.ViewModels {
    public class LauncherPage : PopupHost {
        public RepositoryNode Node {
            get => _node;
            set => SetProperty(ref _node, value);
        }

        public object View {
            get => _view;
            set => SetProperty(ref _view, value);
        }

        public AvaloniaList<Models.Notification> Notifications {
            get;
            set;
        } = new AvaloniaList<Models.Notification>();

        public LauncherPage() {
            _node = new RepositoryNode() {
                Id = Guid.NewGuid().ToString(),
                Name = "WelcomePage",
                Bookmark = 0,
                IsRepository = false,
            };
            _view = new Views.Welcome() { DataContext = new Welcome() };
        }

        public LauncherPage(RepositoryNode node, Repository repo) {
            _node = node;
            _view = new Views.Repository() { DataContext = repo };
        }

        public override string GetId() {
            return _node.Id;
        }

        public void CopyPath() {
            if (_node.IsRepository) App.CopyText(_node.Id);
        }

        public void DismissNotification(object param) {
            if (param is Models.Notification notice) {
                Notifications.Remove(notice);
            }
        }

        private RepositoryNode _node = null;
        private object _view = null;
    }
}
