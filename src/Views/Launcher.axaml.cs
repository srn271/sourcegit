using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views {
    public partial class Launcher : Window, Models.INotificationReceiver {
        public Launcher() {
            DataContext = new ViewModels.Launcher();
            InitializeComponent();
        }

        public void OnReceiveNotification(string ctx, Models.Notification notice) {
            if (DataContext is ViewModels.Launcher vm) {
                foreach (var page in vm.Pages) {
                    var pageId = page.Node.Id.Replace("\\", "/");
                    if (pageId == ctx) {
                        page.Notifications.Add(notice);
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            var vm = DataContext as ViewModels.Launcher;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                if (e.Key == Key.W) {
                    vm.CloseTab(null);
                    e.Handled = true;
                    return;
                } else if (e.Key == Key.Tab) {
                    vm.GotoNextTab();
                    e.Handled = true;
                    return;
                }
            } else if (e.Key == Key.Escape) {
                vm.ActivePage.CancelPopup();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosing(WindowClosingEventArgs e) {
            ViewModels.Preference.Save();
            base.OnClosing(e);
        }

        private void MaximizeOrRestoreWindow(object sender, TappedEventArgs e) {
            if (WindowState == WindowState.Maximized) {
                WindowState = WindowState.Normal;
            } else {
                WindowState = WindowState.Maximized;
            }
            e.Handled = true;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e) {
            BeginMoveDrag(e);
        }

        private void ScrollTabs(object sender, PointerWheelEventArgs e) {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift)) {
                if (e.Delta.Y < 0) launcherTabsScroller.LineRight();
                else launcherTabsScroller.LineLeft();
                e.Handled = true;
            }
        }

        private void ScrollTabsLeft(object sender, RoutedEventArgs e) {
            launcherTabsScroller.LineLeft();
            e.Handled = true;
        }

        private void ScrollTabsRight(object sender, RoutedEventArgs e) {
            launcherTabsScroller.LineRight();
            e.Handled = true;
        }

        private void UpdateScrollIndicator(object sender, SizeChangedEventArgs e) {
            if (launcherTabsBar.Bounds.Width > launcherTabsContainer.Bounds.Width) {
                leftScrollIndicator.IsVisible = true;
                rightScrollIndicator.IsVisible = true;
            } else {
                leftScrollIndicator.IsVisible = false;
                rightScrollIndicator.IsVisible = false;
            }
            e.Handled = true;
        }

        private void SetupDragAndDrop(object sender, RoutedEventArgs e) {
            if (sender is Border border) {
                DragDrop.SetAllowDrop(border, true);
                border.AddHandler(DragDrop.DropEvent, DropTab);
            }
            e.Handled = true;
        }

        private void OnPointerPressedTab(object sender, PointerPressedEventArgs e) {
            _pressedTab = true;
            _startDrag = false;
            _pressedTabPosition = e.GetPosition(sender as Border);
        }

        private void OnPointerReleasedTab(object sender, PointerReleasedEventArgs e) {
            _pressedTab = false;
            _startDrag = false;
        }

        private void OnPointerMovedOverTab(object sender, PointerEventArgs e) {
            if (_pressedTab && !_startDrag && sender is Border border) {
                var delta = e.GetPosition(border) - _pressedTabPosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64) return;

                _startDrag = true;

                var data = new DataObject();
                data.Set("MovedTab", border.DataContext);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
            e.Handled = true;
        }

        private void DropTab(object sender, DragEventArgs e) {
            if (e.Data.Contains("MovedTab") && sender is Border border) {
                var to = border.DataContext as ViewModels.LauncherPage;
                var moved = e.Data.Get("MovedTab") as ViewModels.LauncherPage;
                if (to != null && moved != null && to != moved && DataContext is ViewModels.Launcher vm) {
                    vm.MoveTab(moved, to);
                }
            }

            _pressedTab = false;
            _startDrag = false;
            e.Handled = true;
        }

        private void OnPopupSure(object sender, RoutedEventArgs e) {
            if (DataContext is ViewModels.Launcher vm) {
                vm.ActivePage.ProcessPopup();
            }
            e.Handled = true;
        }

        private void OnPopupCancel(object sender, RoutedEventArgs e) {
            if (DataContext is ViewModels.Launcher vm) {
                vm.ActivePage.CancelPopup();
            }
            e.Handled = true;
        }

        private void OnPopupCancelByClickMask(object sender, PointerPressedEventArgs e) {
            OnPopupCancel(sender, e);
        }

        private async void OpenPreference(object sender, RoutedEventArgs e) {
            var dialog = new Preference();
            await dialog.ShowDialog(this);
            e.Handled = true;
        }

        private async void OpenAboutDialog(object sender, RoutedEventArgs e) {
            var dialog = new About();
            await dialog.ShowDialog(this);
            e.Handled = true;
        }

        private bool _pressedTab = false;
        private Point _pressedTabPosition = new Point();
        private bool _startDrag = false;  
    }
}