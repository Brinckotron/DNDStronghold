using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public class TradeDestinationService
    {
        private static TradeDestinationService? _instance;
        private List<TradeDestination> _destinations = new List<TradeDestination>();
        private string _jsonPath = string.Empty;

        public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private TradeDestinationService()
        {
            Reload();
        }

        public static TradeDestinationService GetInstance()
        {
            _instance ??= new TradeDestinationService();
            return _instance;
        }

        public string GetJsonPath() => _jsonPath;

        public IReadOnlyList<TradeDestination> GetDestinations()
        {
            return _destinations;
        }

        public TradeDestination? GetById(string id)
        {
            return _destinations.Find(d => d.Id == id);
        }

        public void Reload()
        {
            _jsonPath = ResolveEditableJsonPath();
            if (string.IsNullOrEmpty(_jsonPath) || !File.Exists(_jsonPath))
            {
                _destinations = new List<TradeDestination>();
                return;
            }

            try
            {
                string json = File.ReadAllText(_jsonPath);
                var catalog = JsonSerializer.Deserialize<TradeDestinationCatalog>(json, JsonOptions);
                _destinations = catalog?.Destinations ?? new List<TradeDestination>();
                foreach (var dest in _destinations)
                    EnsureDefaultRates(dest);
            }
            catch
            {
                _destinations = new List<TradeDestination>();
            }
        }

        public void AddOrUpdate(TradeDestination dest)
        {
            EnsureDefaultRates(dest);
            var list = _destinations.ToList();
            int index = list.FindIndex(d => d.Id == dest.Id);
            if (index >= 0)
                list[index] = dest;
            else
                list.Add(dest);
            Save(list);
        }

        public void Save(List<TradeDestination> destinations)
        {
            string editable = ResolveEditableJsonPath();
            WriteCatalog(editable, destinations);

            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TradeDestinations.json");
            if (!PathsEqual(editable, output))
                WriteCatalog(output, destinations);

            _jsonPath = editable;
            _destinations = destinations;
        }

        private static void WriteCatalog(string path, List<TradeDestination> destinations)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            foreach (var dest in destinations)
                EnsureDefaultRates(dest);

            var catalog = new TradeDestinationCatalog { Destinations = destinations };
            File.WriteAllText(path, JsonSerializer.Serialize(catalog, JsonOptions));
        }

        private static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureDefaultRates(TradeDestination dest)
        {
            dest.Resources ??= new List<TradeResourceRate>();
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                if (dest.Resources.Any(r => r.ResourceType == type)) continue;
                bool specialty = dest.Specialty?.Contains(type) == true;
                bool sells = type == ResourceType.Gold || specialty;
                dest.Resources.Add(new TradeResourceRate
                {
                    ResourceType = type,
                    BuyRate = TradeService.SuggestedBuyRate(dest.SettlementType, type, specialty),
                    SellRate = TradeService.SuggestedSellRate(dest.SettlementType, type, specialty, sells),
                    CanBuy = sells,
                    Available = DefaultStockFor(dest, type, sells, specialty)
                });
            }

            foreach (var rate in dest.Resources)
            {
                bool specialty = dest.Specialty?.Contains(rate.ResourceType) == true;
                if (rate.ResourceType == ResourceType.Gold)
                {
                    rate.BuyRate = 1m;
                    rate.SellRate = 1m;
                    rate.CanBuy = true;
                }
                if (rate.Available <= 0 && (rate.CanBuy || rate.ResourceType == ResourceType.Gold || specialty))
                    rate.Available = DefaultStockFor(dest, rate.ResourceType, rate.CanBuy, specialty);
            }
        }

        private static int DefaultStockFor(TradeDestination dest, ResourceType type, bool canBuy, bool specialty)
        {
            return TradeService.DefaultStock(dest.SettlementType, type, canBuy, specialty);
        }

        public static string ResolveEditableJsonPath()
        {
            foreach (var path in CandidatePaths())
            {
                if (File.Exists(path) && !IsOutputCopy(path))
                    return path;
            }

            foreach (var path in CandidatePaths())
            {
                if (File.Exists(path))
                    return path;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TradeDestinations.json");
        }

        public static string? ResolveJsonPath() =>
            CandidatePaths().FirstOrDefault(File.Exists) ?? ResolveEditableJsonPath();

        private static bool IsOutputCopy(string path)
        {
            string full = Path.GetFullPath(path);
            return full.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || full.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> CandidatePaths()
        {
            var paths = new List<string>();
            string? dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                paths.Add(Path.Combine(dir, "Data", "TradeDestinations.json"));
                dir = Directory.GetParent(dir)?.FullName;
            }
            paths.Add(Path.Combine(Directory.GetCurrentDirectory(), "Data", "TradeDestinations.json"));
            paths.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "TradeDestinations.json"));
            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
