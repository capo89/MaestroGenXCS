using System.IO;
using System.Text.Json;

namespace MaestroGenXcs.Services;

/// <summary>
/// Trvalo uložené hodnoty parametrov pre šablóny operácií
/// (Nastavenie operácií). Uloženie do JSON v <c>%APPDATA%\MaestroGenXcs</c>.
/// <para>
/// Štruktúra JSON:
/// </para>
/// <code>
/// {
///   "Kolík plocha": { "depth": "13", "tool": "E071", ... },
///   "Kolík hrana":  { "depth": "23", ... }
/// }
/// </code>
/// <para>
/// Pri zápise sa používa debounce (default 500 ms), aby sa pri rýchlom písaní
/// nevolal disk pre každý stlačený znak.
/// </para>
/// </summary>
public sealed class TemplateStore
{
    private static readonly Lazy<TemplateStore> _instance = new(() => new TemplateStore());
    public static TemplateStore Instance => _instance.Value;

    private readonly object _gate = new();
    private readonly System.Timers.Timer _saveDebounce;
    private bool _loaded;

    public string FilePath { get; }

    /// <summary>Všetky hodnoty, kľúč = názov šablóny, vnútorný kľúč = názov parametra.</summary>
    public Dictionary<string, Dictionary<string, string>> Templates { get; private set; }
        = new(StringComparer.Ordinal);

    private TemplateStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MaestroGenXcs");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, "operation-templates.json");

        _saveDebounce = new System.Timers.Timer(500) { AutoReset = false };
        _saveDebounce.Elapsed += (_, _) => SaveNow();
    }

    public void Load()
    {
        lock (_gate)
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                if (!File.Exists(FilePath)) return;
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                if (data != null) Templates = new(data, StringComparer.Ordinal);
            }
            catch
            {
                // Súbor je pokazený alebo nedostupný – ignorujeme a začneme čistý.
                Templates = new(StringComparer.Ordinal);
            }
        }
    }

    public string? Get(string template, string param)
    {
        lock (_gate)
        {
            if (Templates.TryGetValue(template, out var bag)
                && bag.TryGetValue(param, out var v))
                return v;
            return null;
        }
    }

    public IReadOnlyDictionary<string, string> GetAll(string template)
    {
        lock (_gate)
        {
            if (Templates.TryGetValue(template, out var bag))
                return new Dictionary<string, string>(bag, StringComparer.Ordinal);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public void Set(string template, string param, string value)
    {
        lock (_gate)
        {
            if (!Templates.TryGetValue(template, out var bag))
            {
                bag = new Dictionary<string, string>(StringComparer.Ordinal);
                Templates[template] = bag;
            }
            bag[param] = value ?? string.Empty;
        }
        ScheduleSave();
    }

    public Dictionary<string, string> EnsureBag(string template)
    {
        lock (_gate)
        {
            if (!Templates.TryGetValue(template, out var bag))
            {
                bag = new Dictionary<string, string>(StringComparer.Ordinal);
                Templates[template] = bag;
            }
            return bag;
        }
    }

    public void ScheduleSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    public void SaveNow()
    {
        Dictionary<string, Dictionary<string, string>> snapshot;
        lock (_gate)
        {
            snapshot = Templates.ToDictionary(
                kv => kv.Key,
                kv => new Dictionary<string, string>(kv.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);
        }
        try
        {
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Disk read-only / žiadne práva – tichý fallback.
        }
    }
}
