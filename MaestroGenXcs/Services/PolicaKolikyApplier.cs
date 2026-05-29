using MaestroGenXcs.Domain;

using MaestroGenXcs.Operations;



namespace MaestroGenXcs.Services;



/// <summary>

/// Pevná / polohovateľná polica – kolíky a skrutky podľa starého <c>Polica.PripravVrtanieDoBokov</c>.

/// </summary>

public static class PolicaKolikyApplier

{

    public static bool IsPolicaPart(Part part) => part.Kind == PartKind.Polica;

    /// <summary>Operácie generované z panelu Polica – nepatria do zoznamu Operácie (ako v starom Maestri).</summary>
    public static bool IsManagedPolicaOperation(CncOperation op) =>
        op is DrillOperation d && (
            d.TemplateLabel == PolicaDrillingDefaults.BundleTemplateLabel
            || d.IsPolicaMaster);



    public static bool BelongsToPolicaBundle(DrillOperation op, Part polica) =>
        op.TemplateLabel == PolicaDrillingDefaults.BundleTemplateLabel
        && op.Name.Contains(polica.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Majster balíka – na polici (pevná) alebo na boku (polohovateľná).</summary>
    public static DrillOperation? FindPolicaMaster(PartsStore store, Part polica) =>
        polica.Operations.OfType<DrillOperation>().FirstOrDefault(o => o.IsPolicaMaster && BelongsToPolicaBundle(o, polica))
        ?? store.Parts
            .Where(p => p.Zostava == polica.Zostava)
            .SelectMany(p => p.Operations.OfType<DrillOperation>())
            .FirstOrDefault(o => o.IsPolicaMaster && BelongsToPolicaBundle(o, polica));

    public static bool HasPolicaBundle(PartsStore store, Part polica) =>
        store.Parts
            .Where(p => p.Zostava == polica.Zostava)
            .SelectMany(p => p.Operations.OfType<DrillOperation>())
            .Any(o => BelongsToPolicaBundle(o, polica));

    public static void RemoveExistingBundle(PartsStore store, Part polica)
    {
        var masterIds = store.Parts
            .Where(p => p.Zostava == polica.Zostava)
            .SelectMany(p => p.Operations.OfType<DrillOperation>())
            .Where(o => o.IsPolicaMaster && BelongsToPolicaBundle(o, polica))
            .Select(o => o.Id)
            .ToHashSet();

        foreach (var part in store.Parts.Where(p => p.Zostava == polica.Zostava).ToList())
        {
            var toRemove = part.Operations.OfType<DrillOperation>()
                .Where(o =>
                    (o.IsPolicaMaster && BelongsToPolicaBundle(o, polica))
                    || BelongsToPolicaBundle(o, polica)
                    || (o.SourceOperationId is Guid sid && masterIds.Contains(sid)))
                .ToList();

            foreach (var op in toRemove)
                part.Operations.Remove(op);
        }
    }



    public static DrillOperation Apply(PartsStore store, Part polica)

    {

        if (!IsPolicaPart(polica))

            throw new InvalidOperationException("Operácia je len pre dielec typu Polica.");



        polica.EnsurePolicaSerie();



        var bokL = store.Parts.FirstOrDefault(p => p.Kind == PartKind.BokL && p.Zostava == polica.Zostava);

        var bokP = store.Parts.FirstOrDefault(p => p.Kind == PartKind.BokP && p.Zostava == polica.Zostava);

        if (bokL == null || bokP == null)

            throw new InvalidOperationException("V zostave chýba Bok L alebo Bok P.");



        RemoveExistingBundle(store, polica);



        return polica.PolicaJePevna

            ? ApplyPevna(polica, bokL, bokP)

            : ApplyPolohovatelna(polica, bokL, bokP);

    }



    private static DrillOperation ApplyPevna(Part polica, Part bokL, Part bokP)

    {

        var stredHruba = Math.Round(polica.Dz / 2.0, 3);

        var screwDepthPolica = polica.Dz + 1.0;

        DrillOperation? master = null;



        for (var s = 0; s < polica.PolicaSerie.Count; s++)

        {

            var seria = polica.PolicaSerie[s];

            var pocet = Math.Max(1, seria.PocetKolikov > 0 ? seria.PocetKolikov : polica.PolicaPocetKolikovVRade);

            var roztec = Math.Max(1, seria.RoztecMm);

            var vyska = Math.Max(0, seria.OdSpoduMm);

            var prvyPolica = Math.Max(0, seria.OdPrednejHranyMm);

            var prvyPolicaL = Math.Max(0, prvyPolica + bokL.FrontOffsetMm);

            var prvyPolicaP = Math.Max(0, prvyPolica + bokP.FrontOffsetMm);

            var prvyBok = Math.Max(0, seria.OdPrednejHranyMm - polica.PolicaFrontOffsetMm);

            var suffix = $"_s{s + 1}";

            var screwCount = Math.Max(1, polica.PolicaPocetSkrutiek);

            var screwPitch = Math.Max(1, polica.PolicaRozostupSkrutiekMm);



            var left = BuildEdgeDrill(

                polica, PartFace.Left, 2,

                name: $"{polica.Name}_pevna_l{suffix}",

                countY: pocet, pitchY: roztec, xStart: prvyPolicaL, yStart: stredHruba);



            if (master == null)

            {

                left.IsPolicaMaster = true;

                master = left;

            }



            polica.Operations.Add(left);



            polica.Operations.Add(BuildEdgeDrill(

                polica, PartFace.Right, 0,

                name: $"{polica.Name}_pevna_p{suffix}",

                countY: pocet, pitchY: roztec, xStart: prvyPolicaP, yStart: stredHruba,

                sourceId: master.Id));



            if (polica.PolicaSkrutkyZapnute)

            {

                polica.Operations.Add(BuildEdgeScrew(

                    polica, PartFace.Left, 2,

                    name: $"{polica.Name}_skrutky_l{suffix}",

                    countY: screwCount, pitchY: screwPitch, xStart: prvyPolicaL, yStart: stredHruba,

                    depth: screwDepthPolica, sourceId: master.Id));



                polica.Operations.Add(BuildEdgeScrew(

                    polica, PartFace.Right, 0,

                    name: $"{polica.Name}_skrutky_p{suffix}",

                    countY: screwCount, pitchY: screwPitch, xStart: prvyPolicaP, yStart: stredHruba,

                    depth: screwDepthPolica, sourceId: master.Id));

            }



            AddBokTopKolik(bokL, vyska, pocet, roztec, prvyBok, refPos: 2, polica, suffix, master.Id);

            AddBokTopKolik(bokP, vyska, pocet, roztec, prvyBok, refPos: 0, polica, suffix, master.Id);



            if (polica.PolicaSkrutkyZapnute)

            {

                AddBokTopScrew(bokL, vyska, screwCount, screwPitch, prvyBok, refPos: 2, bokL.Dz + 1.0, polica, suffix, master.Id);

                AddBokTopScrew(bokP, vyska, screwCount, screwPitch, prvyBok, refPos: 0, bokP.Dz + 1.0, polica, suffix, master.Id);

            }

        }



        return master ?? throw new InvalidOperationException("Polica nemá žiadnu sériu.");

    }



    private static DrillOperation ApplyPolohovatelna(Part polica, Part bokL, Part bokP)

    {

        DrillOperation? master = null;



        for (var i = 0; i < polica.PolicaSerie.Count; i++)

        {

            var seria = polica.PolicaSerie[i];

            var n = Math.Max(1, seria.PocetPoloh);

            var pocetRadov = polica.PolicaIbaJedenRad ? 1 : 2;

            var vyska = Math.Max(0, seria.OdSpoduMm);

            var odPrednej = Math.Max(0, seria.OdPrednejHranyMm);

            var roztec = Math.Max(1, seria.RoztecMm);

            var suffix = $"_s{i + 1}";



            var left = BuildBokPodpera(bokL, polica, vyska, odPrednej, n, pocetRadov, roztec, refPos: 2,

                name: $"{bokL.Name}_podpery_{polica.Name}{suffix}");



            if (master == null)

            {

                left.IsPolicaMaster = true;

                master = left;

            }



            bokL.Operations.Add(left);

            bokP.Operations.Add(BuildBokPodpera(bokP, polica, vyska, odPrednej, n, pocetRadov, roztec, refPos: 0,

                name: $"{bokP.Name}_podpery_{polica.Name}{suffix}",

                sourceId: master.Id));

        }



        return master ?? throw new InvalidOperationException("Polica nemá žiadnu sériu.");

    }



    private static void AddBokTopKolik(

        Part bok, double vyska, int pocet, double roztec, double prvyOdPrednej,

        int refPos, Part polica, string suffix, Guid masterId)

    {

        bok.Operations.Add(new DrillOperation

        {

            Name = $"{bok.Name}_pevna_{polica.Name}{suffix}",

            Face = PartFace.Top,

            RefPos = refPos,

            CountX = pocet,

            CountY = 1,

            PitchX = roztec,

            PitchY = 32,

            XStart = vyska,

            YStart = prvyOdPrednej,

            Depth = PolicaDrillingDefaults.FaceDepthMm,

            Diameter = PolicaDrillingDefaults.DiameterMm,

            Tool = PolicaDrillingDefaults.DefaultTool,

            DischargerStep = PolicaDrillingDefaults.DischargerStep,

            TemplateLabel = PolicaDrillingDefaults.BundleTemplateLabel,

            Description = $"Bok · polica {polica.Name}",

            IsPropagated = true,

            SourceOperationId = masterId,

        });

    }



    private static void AddBokTopScrew(

        Part bok, double vyska, int pocet, double roztec, double prvyOdPrednej,

        int refPos, double depth, Part polica, string suffix, Guid masterId)

    {

        bok.Operations.Add(new DrillOperation

        {

            Name = $"{bok.Name}_skrutky_pevna_{polica.Name}{suffix}",

            Face = PartFace.Top,

            RefPos = refPos,

            CountX = pocet,

            CountY = 1,

            PitchX = roztec,

            PitchY = 32,

            XStart = vyska,

            YStart = prvyOdPrednej,

            Depth = depth,

            Diameter = PolicaDrillingDefaults.ScrewDiameterMm,

            KindOfHole = PolicaDrillingDefaults.ScrewKindOfHole,

            DischargerStep = PolicaDrillingDefaults.ScrewDischargerStep,

            TemplateLabel = PolicaDrillingDefaults.BundleTemplateLabel,

            Description = $"Bok skrutky · polica {polica.Name}",

            IsPropagated = true,

            SourceOperationId = masterId,

        });

    }



    private static DrillOperation BuildBokPodpera(

        Part bok, Part polica, double vyska, double odPrednej, int pocetPoloh, int pocetRadov, double roztec,

        int refPos, string name, Guid? sourceId = null) => new()

    {

        Name = name,

        Face = PartFace.Top,

        RefPos = refPos,

        CountX = pocetRadov,

        CountY = pocetPoloh,

        PitchX = roztec,

        PitchY = 32,

        XStart = vyska,

        YStart = odPrednej,

        Depth = PolicaDrillingDefaults.FaceDepthMm,

        Diameter = PolicaDrillingDefaults.DiameterMm,

        Tool = PolicaDrillingDefaults.DefaultTool,

        DischargerStep = PolicaDrillingDefaults.DischargerStep,

        TemplateLabel = PolicaDrillingDefaults.BundleTemplateLabel,

        Description = $"Podpery · polica {polica.Name}",

        IsPropagated = sourceId.HasValue,

        SourceOperationId = sourceId,

    };



    private static DrillOperation BuildEdgeDrill(

        Part polica, PartFace face, int refPos, string name,

        int countY, double pitchY, double xStart, double yStart, Guid? sourceId = null) => new()

    {

        Name = name,

        Face = face,

        RefPos = refPos,

        CountX = 1,

        CountY = countY,

        PitchX = 32,

        PitchY = pitchY,

        XStart = xStart,

        YStart = yStart,

        Depth = PolicaDrillingDefaults.EdgeDepthMm,

        Diameter = PolicaDrillingDefaults.DiameterMm,

        Tool = PolicaDrillingDefaults.DefaultTool,

        DischargerStep = PolicaDrillingDefaults.DischargerStep,

        TemplateLabel = PolicaDrillingDefaults.BundleTemplateLabel,

        Description = $"Polica {polica.Name} · hrana {face}",

        IsPropagated = sourceId.HasValue,

        SourceOperationId = sourceId,

    };



    private static DrillOperation BuildEdgeScrew(

        Part polica, PartFace face, int refPos, string name,

        int countY, double pitchY, double xStart, double yStart, double depth, Guid sourceId) => new()

    {

        Name = name,

        Face = face,

        RefPos = refPos,

        CountX = 1,

        CountY = countY,

        PitchX = 32,

        PitchY = pitchY,

        XStart = xStart,

        YStart = yStart,

        Depth = depth,

        Diameter = PolicaDrillingDefaults.ScrewDiameterMm,

        KindOfHole = PolicaDrillingDefaults.ScrewKindOfHole,

        DischargerStep = PolicaDrillingDefaults.ScrewDischargerStep,

        TemplateLabel = PolicaDrillingDefaults.BundleTemplateLabel,

        Description = $"Polica {polica.Name} · skrutky {face}",

        IsPropagated = true,

        SourceOperationId = sourceId,

    };

}


