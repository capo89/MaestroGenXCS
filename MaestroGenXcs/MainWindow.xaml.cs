using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;
using MaestroGenXcs.ViewModels;
using MaestroGenXcs.Views;

namespace MaestroGenXcs;

public partial class MainWindow : Window
{
    private Assembly3DWindow? _assembly3DWindow;

    private bool _syncingTreeSelection;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new AssemblyViewModel();
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => SyncTreeSelectionToSelectedPart();
        Closed += (_, _) =>
        {
            _assembly3DWindow?.Close();
            TemplateStore.Instance.SaveNow();
        };
    }

    private AssemblyViewModel Vm => (AssemblyViewModel)DataContext;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssemblyViewModel.SelectedPart))
            SyncTreeSelectionToSelectedPart();
    }

    private void OnPartsTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_syncingTreeSelection || e.NewValue is not Part part)
            return;
        if (!ReferenceEquals(Vm.SelectedPart, part))
            Vm.SelectedPart = part;
    }

    private void SyncTreeSelectionToSelectedPart()
    {
        var part = Vm.SelectedPart;
        if (part == null || PartsTreeView == null)
            return;

        _syncingTreeSelection = true;
        try
        {
            SelectPartInTree(PartsTreeView, part);
        }
        finally
        {
            _syncingTreeSelection = false;
        }
    }

    private static void SelectPartInTree(TreeView tree, Part part)
    {
        tree.UpdateLayout();
        foreach (var item in tree.Items)
        {
            if (item is not ZostavaTreeNode node || !node.Parts.Contains(part))
                continue;

            if (tree.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem nodeItem)
                return;

            nodeItem.IsExpanded = true;
            nodeItem.UpdateLayout();
            if (nodeItem.ItemContainerGenerator.ContainerFromItem(part) is TreeViewItem leaf)
            {
                leaf.IsSelected = true;
                leaf.BringIntoView();
            }

            return;
        }
    }

    private void OnOpen3D_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        if (_assembly3DWindow == null)
        {
            _assembly3DWindow = new Assembly3DWindow { Owner = this };
            _assembly3DWindow.Closed += (_, _) => _assembly3DWindow = null;
            _assembly3DWindow.Attach(Vm);
            _assembly3DWindow.Show();
        }
        else
        {
            if (_assembly3DWindow.WindowState == WindowState.Minimized)
                _assembly3DWindow.WindowState = WindowState.Normal;
            _assembly3DWindow.Activate();
        }
    }

}
