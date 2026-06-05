using System.ComponentModel;
using System.Linq;
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

    /// <summary>Po kliknutí na uzol „Zostava …“ v strome neprepísať výber na dielec.</summary>
    private string? _treeZostavaNodeHighlight;

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
        _ = sender;
        if (e.PropertyName is not nameof(AssemblyViewModel.SelectedPart)
            and not nameof(AssemblyViewModel.SelectedZostava))
            return;

        if (PartsTreeView == null)
            return;

        _syncingTreeSelection = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(_treeZostavaNodeHighlight))
                SelectZostavaNodeInTree(PartsTreeView, _treeZostavaNodeHighlight);
            else
                SyncTreeSelectionToSelectedPart();
        }
        finally
        {
            _syncingTreeSelection = false;
        }
    }

    private void OnPartsTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_syncingTreeSelection)
            return;

        if (e.NewValue is ZostavaTreeNode node)
        {
            _treeZostavaNodeHighlight = node.Zostava;
            Vm.SelectZostava(node.Zostava);
            return;
        }

        if (e.NewValue is PartTreeEntry entry)
        {
            _treeZostavaNodeHighlight = null;
            if (!ReferenceEquals(Vm.SelectedPart, entry.Part))
                Vm.SelectedPart = entry.Part;
            return;
        }

        if (e.NewValue is not Part part)
            return;

        _treeZostavaNodeHighlight = null;
        if (!ReferenceEquals(Vm.SelectedPart, part))
            Vm.SelectedPart = part;
    }

    private void SyncTreeSelectionToSelectedPart()
    {
        var part = Vm.SelectedPart;
        if (part == null || PartsTreeView == null)
            return;

        SelectPartInTree(PartsTreeView, part);
    }

    private static void SelectPartInTree(TreeView tree, Part part)
    {
        tree.UpdateLayout();
        foreach (var item in tree.Items)
        {
            if (item is not ZostavaTreeNode node)
                continue;

            var entry = node.PartEntries.FirstOrDefault(e => ReferenceEquals(e.Part, part));
            if (entry == null)
                continue;

            if (tree.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem nodeItem)
                return;

            nodeItem.IsExpanded = true;
            nodeItem.UpdateLayout();
            if (nodeItem.ItemContainerGenerator.ContainerFromItem(entry) is TreeViewItem leaf)
            {
                leaf.IsSelected = true;
                leaf.BringIntoView();
            }

            return;
        }
    }

    private static void SelectZostavaNodeInTree(TreeView tree, string zostava)
    {
        tree.UpdateLayout();
        foreach (var item in tree.Items)
        {
            if (item is not ZostavaTreeNode node
                || !string.Equals(node.Zostava, zostava, StringComparison.OrdinalIgnoreCase))
                continue;

            if (tree.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem nodeItem)
                return;

            nodeItem.IsExpanded = true;
            nodeItem.IsSelected = true;
            nodeItem.BringIntoView();
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
