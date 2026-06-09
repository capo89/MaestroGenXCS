using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;
using MaestroGenXcs.ViewModels;
using MaestroGenXcs.Views;

namespace MaestroGenXcs;

public partial class MainWindow : Window
{
    private bool _syncingTreeSelection;

    /// <summary>Po kliknutí na uzol „Zostava …“ v strome neprepísať výber na dielec.</summary>
    private string? _treeZostavaNodeHighlight;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new AssemblyViewModel();
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) =>
        {
            Assembly3DView.Attach(vm);
            SyncTreeSelectionToSelectedPart();
        };
        Closed += (_, _) => TemplateStore.Instance.SaveNow();
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

    private void OnTreeItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void OnShowPartOperations_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: PartTreeEntry entry })
            return;

        Vm.SelectedPart = entry.Part;
        var win = new PartOperationsListWindow(entry.Part, Vm) { Owner = this };
        win.Show();
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

        if (e.NewValue is SufelTreeNode sufelNode)
        {
            _treeZostavaNodeHighlight = null;
            Vm.SelectedPart = sufelNode.PartEntries.FirstOrDefault()?.Part;
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

            if (tree.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem nodeItem)
                continue;

            foreach (var child in node.Children)
            {
                if (child is PartTreeEntry entry && ReferenceEquals(entry.Part, part))
                {
                    nodeItem.IsExpanded = true;
                    nodeItem.UpdateLayout();
                    if (nodeItem.ItemContainerGenerator.ContainerFromItem(entry) is TreeViewItem leaf)
                    {
                        leaf.IsSelected = true;
                        leaf.BringIntoView();
                    }

                    return;
                }

                if (child is not SufelTreeNode sufelNode)
                    continue;

                var inSufel = sufelNode.PartEntries.FirstOrDefault(e => ReferenceEquals(e.Part, part));
                if (inSufel == null)
                    continue;

                nodeItem.IsExpanded = true;
                nodeItem.UpdateLayout();
                if (nodeItem.ItemContainerGenerator.ContainerFromItem(sufelNode) is not TreeViewItem sufelItem)
                    return;

                sufelItem.IsExpanded = true;
                sufelItem.UpdateLayout();
                if (sufelItem.ItemContainerGenerator.ContainerFromItem(inSufel) is TreeViewItem sufelLeaf)
                {
                    sufelLeaf.IsSelected = true;
                    sufelLeaf.BringIntoView();
                }

                return;
            }
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
}
