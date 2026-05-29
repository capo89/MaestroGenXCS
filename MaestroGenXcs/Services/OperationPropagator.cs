using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Propagácia <see cref="DrillOperation"/> (kolíky) podľa pravidiel v <see cref="ConnectionMap"/>.
/// Každý spoj má vlastnú plochu, refpos a prípadne výmenu osí – nie globálne pravidlo.
/// </summary>
public sealed class OperationPropagator
{
    private readonly PartsStore _store;
    private readonly HashSet<Part> _hookedParts = new();
    private readonly HashSet<CncOperation> _hookedSources = new();
    private int _suppressDepth;

    public event EventHandler<string>? StatusMessage;

    /// <summary>
    /// Dávková úprava operácií (traverza, import) bez spúšťania propagácie
    /// počas <see cref="ObservableCollection{T}.CollectionChanged"/>.
    /// </summary>
    public void ExecuteWithoutPropagation(Action action)
    {
        _suppressDepth++;
        try
        {
            action();
        }
        finally
        {
            _suppressDepth--;
        }
    }

    private bool IsSuppressed => _suppressDepth > 0;

    public OperationPropagator(PartsStore store)
    {
        _store = store;

        _store.Parts.CollectionChanged += OnPartsChanged;
        _store.Connections.CollectionChanged += OnConnectionsChanged;

        foreach (var p in _store.Parts)
            HookPart(p);
    }

    private void OnPartsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Part p in e.NewItems)
                HookPart(p);

        if (e.OldItems != null)
            foreach (Part p in e.OldItems)
                UnhookPart(p);
    }

    private void HookPart(Part p)
    {
        if (!_hookedParts.Add(p))
            return;

        p.Operations.CollectionChanged += (s, ev) => OnOperationsChanged(p, ev);
        foreach (var op in p.Operations)
            HookSource(op);
    }

    private void UnhookPart(Part p)
    {
        _hookedParts.Remove(p);
        foreach (var op in p.Operations)
            UnhookSource(op);
    }

    private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Connection c in e.NewItems)
                BackfillForNewConnection(c);
        }

        if (e.OldItems != null)
        {
            foreach (Connection c in e.OldItems)
                RemoveMirrorsForConnection(c);
        }
    }

    private void BackfillForNewConnection(Connection c)
    {
        if (!c.AutoPropagate) return;
        if (c.PartA == null || c.PartB == null) return;

        foreach (var src in c.PartA.Operations.Where(o => o.Face == c.FaceA).OfType<DrillOperation>().ToList())
        {
            if (ShouldPropagate(c, src))
                CreateMirror(c, src, srcOrigin: c.PartA);
        }

        foreach (var src in c.PartB.Operations.Where(o => o.Face == c.FaceB).OfType<DrillOperation>().ToList())
        {
            if (ShouldPropagate(c, src))
                CreateMirror(c, src, srcOrigin: c.PartB);
        }
    }

    private void RemoveMirrorsForConnection(Connection c)
    {
        foreach (var part in _store.Parts.ToList())
        {
            var toRemove = part.Operations
                .Where(op => op.SourceConnectionId == c.Id)
                .ToList();
            foreach (var op in toRemove)
                part.Operations.Remove(op);
        }
    }

    private void OnOperationsChanged(Part part, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CncOperation op in e.NewItems)
            {
                HookSource(op);
                if (!IsSuppressed)
                    PropagateOnAdd(part, op);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CncOperation op in e.OldItems)
            {
                UnhookSource(op);
                if (!IsSuppressed && !op.IsPropagated)
                    PropagateOnRemove(part, op);
            }
        }
    }

    private void PropagateOnAdd(Part srcPart, CncOperation srcOp)
    {
        if (srcOp is not DrillOperation drill) return;
        if (srcPart.Kind == PartKind.Traverza || drill.IsTraverzaMaster)
            return;
        if (drill.IsPolicaMaster || drill.TemplateLabel == PolicaDrillingDefaults.BundleTemplateLabel)
            return;

        foreach (var conn in MatchingConnections(srcPart, srcOp.Face))
        {
            if (ShouldPropagate(conn, drill))
                CreateMirror(conn, drill, srcOrigin: srcPart);
        }
    }

    private static bool ShouldPropagate(Connection conn, DrillOperation src)
    {
        if (!conn.PropagateOnUserDrill)
            return false;
        if (conn.RequiresOppositeBokOptIn)
            return src.PreniestNaDruhyBok;
        return true;
    }

    private void PropagateOnRemove(Part srcPart, CncOperation srcOp)
    {
        _ = srcPart;
        foreach (var part in _store.Parts.ToList())
        {
            var toRemove = part.Operations
                .Where(op => op.SourceOperationId == srcOp.Id)
                .ToList();
            foreach (var m in toRemove)
                part.Operations.Remove(m);
        }
    }

    private void CreateMirror(Connection conn, DrillOperation src, Part? srcOrigin = null)
    {
        var owner = srcOrigin ?? FindOwner(src);

        Part? target;
        PartFace targetFace;
        int targetRefpos;

        if (owner == conn.PartA && src.Face == conn.FaceA)
        {
            target       = conn.PartB;
            targetFace   = conn.FaceB;
            targetRefpos = conn.RefposB;
        }
        else if (owner == conn.PartB && src.Face == conn.FaceB)
        {
            target       = conn.PartA;
            targetFace   = conn.FaceA;
            targetRefpos = conn.RefposA;
        }
        else
        {
            return;
        }

        if (target == null) return;

        var rootId = GetRootSourceId(src);
        var rootPart = FindPartWithOperationId(rootId);
        if (rootPart != null && target == rootPart)
            return;

        if (target.Operations.OfType<DrillOperation>().Any(o =>
                GetRootSourceId(o) == rootId && o.SourceConnectionId == conn.Id))
            return;

        var mirror = WorkplaneMapper.MapDrillPattern(
            src, targetFace, targetRefpos, conn.Id, conn.IdentityCoordinates);
        target.Operations.Add(mirror);
        StatusMessage?.Invoke(this, $"Operácia premietnutá na {target.Name} ({targetFace}).");
    }

    private Part? FindOwner(CncOperation op)
    {
        foreach (var part in _store.Parts)
            if (part.Operations.Contains(op))
                return part;
        return null;
    }

    private IEnumerable<Connection> MatchingConnections(Part part, PartFace face)
    {
        foreach (var c in _store.Connections)
        {
            if (!c.AutoPropagate) continue;
            if ((c.PartA == part && c.FaceA == face) ||
                (c.PartB == part && c.FaceB == face))
                yield return c;
        }
    }

    private void HookSource(CncOperation op)
    {
        if (op.IsPropagated) return;
        if (!_hookedSources.Add(op)) return;
        op.PropertyChanged += OnSourceOperationPropertyChanged;
    }

    private void UnhookSource(CncOperation op)
    {
        if (!_hookedSources.Remove(op)) return;
        op.PropertyChanged -= OnSourceOperationPropertyChanged;
    }

    private void OnSourceOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DrillOperation src) return;
        _ = e;

        var owner = FindOwner(src);
        if (owner == null) return;

        if (src.IsTraverzaMaster)
        {
            TraverzaKolikyApplier.RefreshMirrors(_store, src);
            return;
        }

        if (src.IsPolicaMaster)
            return;

        foreach (var conn in _store.Connections.ToList())
        {
            if (!conn.AutoPropagate) continue;

            bool fromA = owner == conn.PartA && src.Face == conn.FaceA;
            bool fromB = owner == conn.PartB && src.Face == conn.FaceB;
            if (!fromA && !fromB) continue;
            if (!ShouldPropagate(conn, src)) continue;

            var target       = fromA ? conn.PartB : conn.PartA;
            var targetFace   = fromA ? conn.FaceB : conn.FaceA;
            var targetRefpos = fromA ? conn.RefposB : conn.RefposA;
            if (target == null) continue;

            var rootId = GetRootSourceId(src);
            var existing = target.Operations.OfType<DrillOperation>()
                .FirstOrDefault(o => GetRootSourceId(o) == rootId && o.SourceConnectionId == conn.Id);

            var fresh = WorkplaneMapper.MapDrillPattern(
                src, targetFace, targetRefpos, conn.Id, conn.IdentityCoordinates);

            if (existing == null)
                target.Operations.Add(fresh);
            else
                CopyDrillState(fresh, existing);
        }
    }

    private static void CopyDrillState(DrillOperation from, DrillOperation to)
    {
        to.Name = from.Name;
        to.Face = from.Face;
        to.RefPos = from.RefPos;
        to.CountX = from.CountX;
        to.CountY = from.CountY;
        to.PitchX = from.PitchX;
        to.PitchY = from.PitchY;
        to.XStart = from.XStart;
        to.YStart = from.YStart;
        to.Diameter = from.Diameter;
        to.Depth = from.Depth;
        to.Tool = from.Tool;
        to.KindOfHole = from.KindOfHole;
        to.DischargerStep = from.DischargerStep;
        to.TemplateLabel = from.TemplateLabel;
        to.Description = from.Description;
        to.IsEnabled = from.IsEnabled;
        to.TraverzaBokXStart = from.TraverzaBokXStart;
        to.TraverzaBokYStart = from.TraverzaBokYStart;
        to.TraverzaPatternAlongX = from.TraverzaPatternAlongX;
        to.IsTraverzaMaster = from.IsTraverzaMaster;
        to.PreniestNaDruhyBok = from.PreniestNaDruhyBok;
        to.IsPolicaMaster = from.IsPolicaMaster;
    }

    private static Guid GetRootSourceId(DrillOperation op) => op.SourceOperationId ?? op.Id;

    private Part? FindPartWithOperationId(Guid operationId)
    {
        foreach (var part in _store.Parts)
        {
            if (part.Operations.Any(o => o.Id == operationId))
                return part;
        }

        return null;
    }
}
