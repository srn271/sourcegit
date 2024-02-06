﻿using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class CountSelectedCommits {
        public int Count { get; set; }
    }

    public class Histories : ObservableObject {
        public bool IsLoading {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public double DataGridRowHeight {
            get => _dataGridRowHeight;
        }

        public List<Models.Commit> Commits {
            get => _commits;
            set {
                if (SetProperty(ref _commits, value)) {
                    Graph = null;
                    Task.Run(() => {
                        var graph = Models.CommitGraph.Parse(value, DataGridRowHeight, 8);
                        Dispatcher.UIThread.Invoke(() => {
                            Graph = graph;
                        });
                    });
                }
            }
        }

        public Models.CommitGraph Graph {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public Models.Commit AutoSelectedCommit {
            get => _autoSelectedCommit;
            private set => SetProperty(ref _autoSelectedCommit, value);
        }

        public object DetailContext {
            get => _detailContext;
            private set => SetProperty(ref _detailContext, value);
        }

        public Histories(Repository repo) {
            _repo = repo;
        }

        public void NavigateTo(string commitSHA) {
            var commit = _commits.Find(x => x.SHA.StartsWith(commitSHA));
            if (commit != null) {
                AutoSelectedCommit = commit;

                if (_detailContext is CommitDetail detail) {
                    detail.Commit = commit;
                } else {
                    var commitDetail = new CommitDetail(_repo.FullPath);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
        }

        public void Select(IList commits) {
            if (commits.Count == 0) {
                DetailContext = null;
            } else if (commits.Count == 1) {
                var commit = commits[0] as Models.Commit;
                AutoSelectedCommit = commit;

                if (_detailContext is CommitDetail detail) {
                    detail.Commit = commit;
                } else {
                    var commitDetail = new CommitDetail(_repo.FullPath);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            } else if (commits.Count == 2) {
                var end = commits[0] as Models.Commit;
                var start = commits[1] as Models.Commit;
                DetailContext = new RevisionCompare(_repo.FullPath, start, end);
            } else {
                DetailContext = new CountSelectedCommits() { Count = commits.Count };
            }
        }

        public ContextMenu MakeContextMenu() {
            var detail = _detailContext as CommitDetail;
            if (detail == null) return null;

            var current = _repo.Branches.Find(x => x.IsCurrent);
            if (current == null) return null;

            var commit = detail.Commit;
            var menu = new ContextMenu();
            var tags = new List<Models.Tag>();

            if (commit.HasDecorators) {
                foreach (var d in commit.Decorators) {
                    if (d.Type == Models.DecoratorType.CurrentBranchHead) {
                        FillCurrentBranchMenu(menu, current);
                    } else if (d.Type == Models.DecoratorType.LocalBranchHead) {
                        var b = _repo.Branches.Find(x => x.IsLocal && d.Name == x.Name);
                        FillOtherLocalBranchMenu(menu, b, current, commit.IsMerged);
                    } else if (d.Type == Models.DecoratorType.RemoteBranchHead) {
                        var b = _repo.Branches.Find(x => !x.IsLocal && d.Name == $"{x.Remote}/{x.Name}");
                        FillRemoteBranchMenu(menu, b, current, commit.IsMerged);
                    } else if (d.Type == Models.DecoratorType.Tag) {
                        var t = _repo.Tags.Find(x => x.Name == d.Name);
                        if (t != null) tags.Add(t);
                    }
                }

                if (menu.Items.Count > 0) menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (tags.Count > 0) {
                foreach (var tag in tags) FillTagMenu(menu, tag);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (current.Head != commit.SHA) {
                var reset = new MenuItem();
                reset.Header = App.Text("CommitCM.Reset", current.Name);
                reset.Icon = CreateMenuIcon("Icons.Reset");
                reset.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Reset(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reset);
            } else {
                var reword = new MenuItem();
                reword.Header = App.Text("CommitCM.Reword");
                reword.Icon = CreateMenuIcon("Icons.Edit");
                reword.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Reword(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reword);

                var squash = new MenuItem();
                squash.Header = App.Text("CommitCM.Squash");
                squash.Icon = CreateMenuIcon("Icons.SquashIntoParent");
                squash.IsEnabled = commit.Parents.Count == 1;
                squash.Click += (o, e) => {
                    if (commit.Parents.Count == 1) {
                        var parent = _commits.Find(x => x.SHA == commit.Parents[0]);
                        if (parent != null && PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Squash(_repo, commit, parent));
                    }
                    
                    e.Handled = true;
                };
                menu.Items.Add(squash);
            }

            if (!commit.IsMerged) {
                var rebase = new MenuItem();
                rebase.Header = App.Text("CommitCM.Rebase", current.Name);
                rebase.Icon = CreateMenuIcon("Icons.Rebase");
                rebase.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Rebase(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(rebase);

                var cherryPick = new MenuItem();
                cherryPick.Header = App.Text("CommitCM.CherryPick");
                cherryPick.Icon = CreateMenuIcon("Icons.CherryPick");
                cherryPick.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CherryPick(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(cherryPick);
            } else {
                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Icon = CreateMenuIcon("Icons.Undo");
                revert.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Revert(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            var createBranch = new MenuItem();
            createBranch.Icon = CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Icon = CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateTag(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = CreateMenuIcon("Icons.Diff");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) => {
                var topLevel = App.GetTopLevel();
                if (topLevel == null) return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1) {
                    var succ = new Commands.FormatPatch(_repo.FullPath, commit.SHA, selected[0].Path.LocalPath).Exec();
                    if (succ) App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Archive(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Icon = CreateMenuIcon("Icons.Copy");
            copySHA.Click += (o, e) => {
                App.CopyText(commit.SHA);
                e.Handled = true;
            };
            menu.Items.Add(copySHA);
            return menu;
        }

        private void FillCurrentBranchMenu(ContextMenu menu, Models.Branch current) {
            var submenu = new MenuItem();
            submenu.Icon = CreateMenuIcon("Icons.Branch");
            submenu.Header = current.Name;

            var dirty = !string.IsNullOrEmpty(current.UpstreamTrackStatus);
            if (!string.IsNullOrEmpty(current.Upstream)) {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                fastForward.Icon = CreateMenuIcon("Icons.FastForward");
                fastForward.IsEnabled = dirty;
                fastForward.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Merge(_repo, upstream, current.Name));
                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.Pull", upstream);
                pull.Icon = CreateMenuIcon("Icons.Pull");
                pull.IsEnabled = dirty;
                pull.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Pull(_repo, null));
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", current.Name);
            push.Icon = CreateMenuIcon("Icons.Push");
            push.IsEnabled = _repo.Remotes.Count > 0 && dirty;
            push.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Push(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(push);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var type = _repo.GitFlow.GetBranchType(current.Name);
            if (type != Models.GitFlowBranchType.None) {
                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", current.Name);
                finish.Icon = CreateMenuIcon("Icons.Flow");
                finish.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowFinish(_repo, current, type));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", current.Name);
            rename.Icon = CreateMenuIcon("Icons.Rename");
            rename.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new RenameBranch(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged) {
            var submenu = new MenuItem();
            submenu.Icon = CreateMenuIcon("Icons.Branch");
            submenu.Header = branch.Name;

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
            checkout.Icon = CreateMenuIcon("Icons.Check");
            checkout.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Checkout(_repo, branch.Name));
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
            merge.Icon = CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Merge(_repo, branch.Name, current.Name));
                e.Handled = true;
            };
            submenu.Items.Add(merge);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var type = _repo.GitFlow.GetBranchType(branch.Name);
            if (type != Models.GitFlowBranchType.None) {
                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", branch.Name);
                finish.Icon = CreateMenuIcon("Icons.Flow");
                finish.Click += (o, e) => {
                    if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new GitFlowFinish(_repo, branch, type));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = CreateMenuIcon("Icons.Rename");
            rename.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new RenameBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged) {
            var name = $"{branch.Remote}/{branch.Name}";

            var submenu = new MenuItem();
            submenu.Icon = CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", name);
            checkout.Icon = CreateMenuIcon("Icons.Check");
            checkout.Click += (o, e) => {
                foreach (var b in _repo.Branches) {
                    if (b.IsLocal && b.Upstream == branch.FullName) {
                        if (b.IsCurrent) return;
                        if (PopupHost.CanCreatePopup()) PopupHost.ShowAndStartPopup(new Checkout(_repo, b.Name));
                        return;
                    }
                }

                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new CreateBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", name, current.Name);
            merge.Icon = CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new Merge(_repo, name, current.Name));
                e.Handled = true;
            };

            submenu.Items.Add(merge);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", name);
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, Models.Tag tag) {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            var push = new MenuItem();
            push.Header = App.Text("TagCM.Push", tag.Name);
            push.Icon = CreateMenuIcon("Icons.Push");
            push.IsEnabled = _repo.Remotes.Count > 0;
            push.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new PushTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = App.Text("TagCM.Delete", tag.Name);
            delete.Icon = CreateMenuIcon("Icons.Clear");
            delete.Click += (o, e) => {
                if (PopupHost.CanCreatePopup()) PopupHost.ShowPopup(new DeleteTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private Avalonia.Controls.Shapes.Path CreateMenuIcon(string key) {
            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 12;
            icon.Height = 12;
            icon.Stretch = Stretch.Uniform;
            icon.Data = App.Current?.FindResource(key) as StreamGeometry;
            return icon;
        }

        private Repository _repo = null;
        private double _dataGridRowHeight = 28;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private Models.Commit _autoSelectedCommit = null;
        private object _detailContext = null;
    }
}
