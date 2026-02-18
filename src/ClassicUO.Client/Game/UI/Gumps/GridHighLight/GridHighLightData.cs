using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    public class GridHighlightData
    {
        private static GridHighlightData[] allConfigs;
        private readonly GridHighlightSetupEntry _entry;

        private static readonly Queue<uint> _queue = new();
        private static readonly HashSet<uint> _queuedItems = new();
        private static bool hasQueuedItems;
        // Ensures only one background matching task runs at a time so non-thread-safe
        // GridHighlightData caches (_normalizeCache, EnsureCache dictionaries) are
        // never accessed concurrently.
        private static readonly SemaphoreSlim _bgMatchSemaphore = new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, string> _normalizeCache = new();

        public static GridHighlightData[] AllConfigs
        {
            get
            {
                if (allConfigs != null)
                    return allConfigs;

                List<GridHighlightSetupEntry> setup = ProfileManager.CurrentProfile.GridHighlightSetup;
                allConfigs = setup.Select(entry => new GridHighlightData(entry)).ToArray();
                return allConfigs;
            }
            set => allConfigs = value;
        }

        public string Name
        {
            get => _entry.Name;
            set => _entry.Name = value;
        }

        public List<string> ItemNames
        {
            get => _entry.ItemNames;
            set => _entry.ItemNames = value;
        }

        public ushort Hue
        {
            get => _entry.Hue;
            set => _entry.Hue = value;
        }

        public Color HighlightColor
        {
            get => _entry.GetHighlightColor();
            set => _entry.SetHighlightColor(value);
        }

        public List<GridHighlightProperty> Properties
        {
            get => _entry.Properties;
            set
            {
                _entry.Properties = value;
                InvalidateCache();
            }
        }

        public bool AcceptExtraProperties
        {
            get => _entry.AcceptExtraProperties;
            set => _entry.AcceptExtraProperties = value;
        }

        public int MinimumProperty
        {
            get => _entry.MinimumProperty;
            set => _entry.MinimumProperty = value;
        }

        public int MaximumProperty
        {
            get => _entry.MaximumProperty;
            set => _entry.MaximumProperty = value;
        }

        public int MinimumMatchingProperty
        {
            get => _entry.MinimumMatchingProperty;
            set => _entry.MinimumMatchingProperty = value;
        }

        public int MaximumMatchingProperty
        {
            get => _entry.MaximumMatchingProperty;
            set => _entry.MaximumMatchingProperty = value;
        }

        public List<string> ExcludeNegatives
        {
            get => _entry.ExcludeNegatives;
            set
            {
                _entry.ExcludeNegatives = value;
                InvalidateCache();
            }
        }

        public bool Overweight
        {
            get => _entry.Overweight;
            set => _entry.Overweight = value;
        }

        public int MinimumWeight
        {
            get => _entry.MinimumWeight;
            set => _entry.MinimumWeight = value;
        }

        public int MaximumWeight
        {
            get => _entry.MaximumWeight;
            set => _entry.MaximumWeight = value;
        }

        public List<string> RequiredRarities
        {
            get => _entry.RequiredRarities;
            set
            {
                _entry.RequiredRarities = value;
                InvalidateCache();
            }
        }

        public GridHighlightSlot EquipmentSlots
        {
            get => _entry.GridHighlightSlot;
            set => _entry.GridHighlightSlot = value;
        }

        public bool LootOnMatch
        {
            get => _entry.LootOnMatch;
            set => _entry.LootOnMatch = value;
        }

        public uint DestinationContainer
        {
            get => _entry.DestinationContainer;
            set
            {
                _entry.DestinationContainer = value;
                _cachedLootEntry = null; // Invalidate cache when container changes
            }
        }

        private AutoLootManager.AutoLootConfigEntry _cachedLootEntry;

        private AutoLootManager.AutoLootConfigEntry GetLootEntry()
        {
            if (DestinationContainer == 0)
                return null;

            if (_cachedLootEntry == null || _cachedLootEntry.DestinationContainer != DestinationContainer)
            {
                _cachedLootEntry = new AutoLootManager.AutoLootConfigEntry
                {
                    DestinationContainer = DestinationContainer
                };
            }

            return _cachedLootEntry;
        }

        private List<string> _cachedNormalizedRulesExcludeNegatives;
        private HashSet<string> _cachedNormalizedRulesRequiredRarities;
        private HashSet<string> _cachedNormalizedAllRarities;
        private HashSet<string> _cachedNormalizedAllProperties;
        private HashSet<string> _cachedNormalizedAllNegatives;
        private Dictionary<string, (int MinValue, bool IsOptional)> _cachedNormalizedRulesProperties;
        private static readonly List<ItemPropertiesData> _reusableItemData = new(3);
        private static readonly List<uint> _reusableRequeueItems = new();
        private bool _cacheValid = false;

        private GridHighlightData(GridHighlightSetupEntry entry)
        {
            _entry = entry;
        }

        public void Delete()
        {
            ProfileManager.CurrentProfile.GridHighlightSetup.Remove(_entry);
            allConfigs = null;
        }

        public void Move(bool up)
        {
            List<GridHighlightSetupEntry> list = ProfileManager.CurrentProfile.GridHighlightSetup;
            int index = list.IndexOf(_entry);
            if (index == -1) return; // Not found

            // Prevent moving out of bounds
            if (up && index == 0) return;
            if (!up && index == list.Count - 1) return;

            list.RemoveAt(index);
            list.Insert(up ? index - 1 : index + 1, _entry);
        }

        public static void ProcessItemOpl(World world, Item item)
        {
            if (item.HighlightChecked) return;

            ProcessItemOpl(world, item.Serial);
        }


        public static void ProcessItemOpl(World world, uint serial)
        {
            // Only queue items if the server supports tooltips
            if (!world.ClientFeatures.TooltipsEnabled)
                return;

            // Check if already queued to avoid duplicates
            if (!_queuedItems.Add(serial))
                return;

            // Enqueue for processing - validation happens in ProcessQueue
            _queue.Enqueue(serial);
            hasQueuedItems = true;
        }

        public static void ProcessQueue(World World)
        {
            if (!hasQueuedItems)
                return;

            _reusableItemData.Clear();
            _reusableRequeueItems.Clear();

            for (int i = 0; i < 3 && _queue.Count > 0; i++)
            {
                uint ser = _queue.Dequeue();

                // Check if item still exists
                if (!World.Items.TryGetValue(ser, out Item item))
                {
                    // Item was removed, remove from hashset and skip
                    _queuedItems.Remove(ser);
                    continue;
                }

                // Check if item is still valid for highlighting
                if (item.OnGround || item.IsMulti || item.HighlightChecked)
                {
                    // Item moved to ground or is multi, remove from hashset and skip
                    _queuedItems.Remove(ser);
                    continue;
                }

                // Check if OPL data exists
                if (!World.OPL.TryGetNameAndData(ser, out _, out _))
                {
                    // OPL data not available yet, requeue for later processing
                    _reusableRequeueItems.Add(ser);
                    continue;
                }

                // OPL data exists — snapshot data and mark as being processed so it
                // isn't re-queued before the background task finishes.
                _queuedItems.Remove(ser);
                item.HighlightChecked = true;
                _reusableItemData.Add(new ItemPropertiesData(World, item));
            }

            // Requeue items that don't have OPL data yet
            foreach (uint ser in _reusableRequeueItems)
            {
                _queue.Enqueue(ser);
            }

            if (_queue.Count == 0)
            {
                hasQueuedItems = false;
                _queuedItems.Clear(); // Clear hashset when queue is empty
            }

            if (_reusableItemData.Count == 0)
                return;

            // Snapshot configs reference and item data list before handing off to
            // the background thread so the main thread's _reusableItemData can be
            // safely cleared next frame.
            GridHighlightData[] configs = AllConfigs;
            var batch = new List<ItemPropertiesData>(_reusableItemData);

            // Offload the expensive property-matching computation to a background
            // thread. The semaphore ensures only one task runs at a time, keeping
            // _normalizeCache and EnsureCache dictionaries single-threaded.
            Task.Run(async () =>
            {
                await _bgMatchSemaphore.WaitAsync();
                try
                {
                    foreach (ItemPropertiesData data in batch)
                    {
                        Item item = data.item;
                        GridHighlightData bestMatch = GetBestMatch(data, configs);

                        // Apply results on the main thread.
                        MainThreadQueue.EnqueueAction(() =>
                        {
                            if (item == null || item.IsDestroyed) return;

                            if (bestMatch != null)
                            {
                                item.MatchesHighlightData = true;
                                item.HighlightColor = bestMatch.HighlightColor;
                                item.HighlightName = bestMatch.Name;

                                if (bestMatch.LootOnMatch)
                                {
                                    Item root = World.Items.Get(item.RootContainer);
                                    if (root != null && root.IsCorpse)
                                    {
                                        AutoLootManager.Instance.LootItem(item, bestMatch.GetLootEntry());
                                        item.ShouldAutoLoot = true;
                                    }
                                }
                            }
                        });
                    }
                }
                finally
                {
                    _bgMatchSemaphore.Release();
                }
            });
        }

        public static GridHighlightData GetGridHighlightData(int index)
        {
            List<GridHighlightSetupEntry> list = ProfileManager.CurrentProfile.GridHighlightSetup;
            GridHighlightData data = index >= 0 && index < list.Count ? new GridHighlightData(list[index]) : null;

            if (data == null)
            {
                list.Add(new GridHighlightSetupEntry());
                data = new GridHighlightData(list[index]);
            }

            return data;
        }

        public static void RecheckMatchStatus()
        {
            AllConfigs = null; // Reset configs

            World world = World.Instance;
            if (world == null)
                return;

            // Then re-queue all valid items for OPL processing
            foreach (KeyValuePair<uint, Item> kvp in world.Items)
            {
                Item item = kvp.Value;
                if (item.OnGround || item.IsMulti)
                    continue;

                item.MatchesHighlightData = false;
                item.HighlightName = null;
                item.HighlightColor = Color.Transparent;
                item.ShouldAutoLoot = false;
                item.HighlightChecked = false;

                ProcessItemOpl(world, kvp.Key);
            }
        }

        public bool IsMatch(ItemPropertiesData itemData) => AcceptExtraProperties
                ? IsMatchFromProperties(itemData)
                : IsMatchFromItemPropertiesData(itemData);

        public bool DoesPropertyMatch(ItemPropertiesData.SinglePropertyData property)
        {
            foreach (GridHighlightProperty rule in Properties)
            {
                string nProp = Normalize(property.Name);
                string nRule = Normalize(rule.Name);

                bool nameMatch = nProp.Equals(nRule, StringComparison.OrdinalIgnoreCase) ||
                                 nProp.Contains(nRule, StringComparison.OrdinalIgnoreCase) ||
                                 Normalize(property.OriginalString).Contains(nRule, StringComparison.OrdinalIgnoreCase);

                bool valueMatch = rule.MinValue == -1 || property.FirstValue >= rule.MinValue;

                if (nameMatch && valueMatch)
                    return true;
            }

            // rarities
            string normalizedPropName = Normalize(property.Name);
            foreach (string r in RequiredRarities)
            {
                if (normalizedPropName.Equals(Normalize(r), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void InvalidateCache()
        {
            _cacheValid = false;
            RecheckMatchStatus();
        }

        private void EnsureCache()
        {
            if (_cacheValid) return;

            // All
            _cachedNormalizedAllRarities = new HashSet<string>(
                GridHighlightRules.RarityProperties.Select(Normalize), StringComparer.OrdinalIgnoreCase) ?? new();
            _cachedNormalizedAllProperties = new HashSet<string>(
                GridHighlightRules.Properties.Concat(GridHighlightRules.SlayerProperties).Concat(GridHighlightRules.SuperSlayerProperties).Select(Normalize), StringComparer.OrdinalIgnoreCase) ?? new();
            _cachedNormalizedAllNegatives = new HashSet<string>(
                GridHighlightRules.NegativeProperties.Select(Normalize), StringComparer.OrdinalIgnoreCase) ?? new();

            // Rules
            _cachedNormalizedRulesExcludeNegatives = ExcludeNegatives.Select(Normalize).ToList() ?? new List<string>();
            _cachedNormalizedRulesRequiredRarities = new HashSet<string>(
                RequiredRarities.Select(Normalize), StringComparer.OrdinalIgnoreCase) ?? new();
            _cachedNormalizedRulesProperties = Properties
                .GroupBy(p => Normalize(p.Name)) // dedupe if config had repeats
                .ToDictionary(g => g.Key,
                              g =>
                              {
                                  // if duplicates exist, keep the strictest (highest MinValue) and required if any non-optional
                                  int minValue = g.Max(x => x.MinValue);
                                  bool isOptional = g.All(x => x.IsOptional); // any required makes it required
                                  return (minValue, isOptional);
                              },
                              StringComparer.OrdinalIgnoreCase) ?? new();

            _cacheValid = true;
        }

        private bool IsMatchFromProperties(ItemPropertiesData itemData)
        {
            EnsureCache();

            if (!IsItemNameMatch(itemData.Name) || (itemData.item != null && !MatchesSlot(itemData.item.ItemData.Layer)))
                return false;

            // Rules
            Dictionary<string, (int MinValue, bool IsOptional)> normalizedRulesProperties = _cachedNormalizedRulesProperties;
            List<string> normalizedRulesExcludeNegatives = _cachedNormalizedRulesExcludeNegatives;
            HashSet<string> normalizedRulesRequiredRarities = _cachedNormalizedRulesRequiredRarities;

            // All
            HashSet<string> normalizedAllRarities = _cachedNormalizedAllRarities;
            HashSet<string> normalizedAllProperties = _cachedNormalizedAllProperties;


            // --- Preprocess item data once (normalize both Name and OriginalString)
            var normalizedItemProperties = new Dictionary<string, (string Original, double Value)>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemPropertiesData.SinglePropertyData p in itemData.singlePropertyData)
            {
                string key = Normalize(p.Name);
                if (normalizedItemProperties.TryGetValue(key, out var existing))
                {
                    if (p.FirstValue > existing.Value)
                        normalizedItemProperties[key] = (existing.Original, p.FirstValue);
                }
                else
                {
                    normalizedItemProperties[key] = (Normalize(p.OriginalString), p.FirstValue);
                }
            }

            // --- Combined overweight, exclusion, and rarity scan
            bool hasRequiredRarity = normalizedRulesRequiredRarities.Count == 0;
            foreach (KeyValuePair<string, (string Original, double Value)> normalizedItemProperty in normalizedItemProperties)
            {
                string propertyName = normalizedItemProperty.Key;
                string original = normalizedItemProperty.Value.Original;

                // weight check
                if (Overweight && !IsWeightInRange(original, MinimumWeight, MaximumWeight))
                    return false;

                // exclusion check
                bool excluded = false;
                foreach (string pattern in normalizedRulesExcludeNegatives)
                {
                    if (propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase) || original.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded)
                    return false;

                // rarity check
                if (!hasRequiredRarity && normalizedAllRarities.Contains(propertyName) && normalizedRulesRequiredRarities.Contains(propertyName))
                    hasRequiredRarity = true;
            }

            if (!hasRequiredRarity)
                return false;

            // --- Property matching
            var matchedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchedRequiredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, (int MinValue, bool IsOptional)> normalizedRulesProperty in normalizedRulesProperties)
            {
                string normalizedPropertyName = normalizedRulesProperty.Key;
                int propertyMinValue = normalizedRulesProperty.Value.MinValue;
                bool isPropertyOptional = normalizedRulesProperty.Value.IsOptional;

                if (!normalizedItemProperties.TryGetValue(normalizedPropertyName, out (string Original, double Value) normalizedItemProperty))
                    continue;

                if (propertyMinValue == -1 || normalizedItemProperty.Value >= propertyMinValue)
                {
                    matchedProperties.Add(normalizedPropertyName);
                    if (!isPropertyOptional)
                        matchedRequiredProperties.Add(normalizedPropertyName);
                }
            }

            // --- Validate required properties
            foreach (KeyValuePair<string, (int MinValue, bool IsOptional)> p in normalizedRulesProperties)
            {
                if (!p.Value.IsOptional && !matchedRequiredProperties.Contains(p.Key))
                    return false;
            }


            if (!IsMatchingCount(matchedProperties.Count, MinimumMatchingProperty, MaximumMatchingProperty))
                return false;

            // --- Included property count
            var includedProps = new HashSet<string>(normalizedItemProperties.Keys.Intersect(normalizedAllProperties), StringComparer.OrdinalIgnoreCase);

            if (!IsMatchingCount(includedProps.Count, MinimumProperty, MaximumProperty))
                return false;

            return true;
        }

        private bool IsMatchFromItemPropertiesData(ItemPropertiesData itemData)
        {
            EnsureCache();

            if (!IsItemNameMatch(itemData.Name))
                return false;

            if (itemData.item != null && !MatchesSlot(itemData.item.ItemData.Layer))
                return false;

            var normalizedItemLines = new Dictionary<string, (string Original, double Value)>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemPropertiesData.SinglePropertyData p in itemData.singlePropertyData)
            {
                string key = Normalize(p.Name);
                if (normalizedItemLines.TryGetValue(key, out var existing))
                {
                    if (p.FirstValue > existing.Value)
                        normalizedItemLines[key] = (existing.Original, p.FirstValue);
                }
                else
                {
                    normalizedItemLines[key] = (Normalize(p.OriginalString), p.FirstValue);
                }
            }

            // Rules
            Dictionary<string, (int MinValue, bool IsOptional)> normalizedRulesProperties = _cachedNormalizedRulesProperties;
            List<string> normalizedRulesExcludeNegatives = _cachedNormalizedRulesExcludeNegatives;
            HashSet<string> normalizedRulesRequiredRarities = _cachedNormalizedRulesRequiredRarities;

            // All
            HashSet<string> normalizedAllProperties = _cachedNormalizedAllProperties;

            // Classify item properties using hash lookups
            bool hasNegative = false;
            bool hasRarity = false;
            bool hasProperty = false;

            foreach (KeyValuePair<string, (string Original, double Value)> kvp in normalizedItemLines)
            {
                if (normalizedAllProperties.Contains(kvp.Key)) { hasProperty = true; }
                if (normalizedRulesRequiredRarities.Contains(kvp.Key)) { hasRarity = true; }
                foreach (string rule in normalizedRulesExcludeNegatives)
                {
                    if (rule.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)) { hasNegative = true; break; }
                }
            }

            if (!hasProperty && !hasNegative && !hasRarity)
                return false;

            if (Overweight)
            {
                foreach (KeyValuePair<string, (string Original, double Value)> prop in normalizedItemLines)
                {
                    if (!IsWeightInRange(prop.Value.Original, MinimumWeight, MaximumWeight))
                        return false;
                }
            }

            // Exclusion check
            foreach (string excludePattern in normalizedRulesExcludeNegatives)
            {
                foreach (KeyValuePair<string, (string Original, double Value)> kvp in normalizedItemLines)
                {
                    if (normalizedAllProperties.Contains(kvp.Key) || normalizedRulesExcludeNegatives.Contains(kvp.Key))
                    {
                        if (kvp.Key.IndexOf(excludePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                            return false;
                    }
                }
            }

            if (normalizedRulesRequiredRarities.Count > 0 && !hasRarity)
                return false;

            int matchingPropertiesCount = 0;
            int filteredItemLineCount = 0;

            // Build filtered item lines (properties that are in the allProperties set)
            // and check that all item lines are in a rule (no extra properties allowed)
            foreach (KeyValuePair<string, (string Original, double Value)> kvp in normalizedItemLines)
            {
                if (!normalizedAllProperties.Contains(kvp.Key))
                    continue;

                filteredItemLineCount++;

                if (!normalizedRulesProperties.ContainsKey(kvp.Key))
                    return false;
            }

            // Checking if all the required properties are present and counting matches
            foreach (KeyValuePair<string, (int MinValue, bool IsOptional)> rule in normalizedRulesProperties)
            {
                bool found = normalizedItemLines.TryGetValue(rule.Key, out var itemLine);

                if (!rule.Value.IsOptional)
                {
                    if (!found || (rule.Value.MinValue != -1 && itemLine.Value < rule.Value.MinValue))
                        return false;

                    matchingPropertiesCount++;
                }
                else
                {
                    if (found && (rule.Value.MinValue == -1 || itemLine.Value >= rule.Value.MinValue))
                        matchingPropertiesCount++;
                }
            }

            if (!IsMatchingCount(matchingPropertiesCount, MinimumMatchingProperty, MaximumMatchingProperty))
                return false;

            if (!IsMatchingCount(filteredItemLineCount, MinimumProperty, MaximumProperty))
                return false;

            return true;
        }

        public static GridHighlightData GetBestMatch(ItemPropertiesData itemData, GridHighlightData[] configs = null)
        {
            GridHighlightData best = null;
            double bestScore = -1;

            foreach (GridHighlightData config in configs ?? AllConfigs)
            {
                if (!config.IsMatch(itemData))
                    continue;

                double score = 0;
                int totalRules = config.Properties.Count;
                int matchedRules = 0;
                int requiredCount = 0;
                int optionalCount = 0;

                // Pre-normalize rules into a dictionary for O(1) lookup
                var normalizedRules = new Dictionary<string, GridHighlightProperty>(StringComparer.OrdinalIgnoreCase);
                foreach (GridHighlightProperty rule in config.Properties)
                {
                    string nRule = config.Normalize(rule.Name);
                    normalizedRules.TryAdd(nRule, rule);
                    if (!rule.IsOptional)
                        requiredCount++;
                    else
                        optionalCount++;
                }

                foreach (ItemPropertiesData.SinglePropertyData prop in itemData.singlePropertyData)
                {
                    string nProp = config.Normalize(prop.Name);

                    // Try exact match first (O(1) lookup)
                    if (normalizedRules.TryGetValue(nProp, out GridHighlightProperty exactRule))
                    {
                        double delta = prop.FirstValue >= exactRule.MinValue + 5 ? 3.0 : 2.0;
                        score += delta;
                        matchedRules++;
                    }
                    else
                    {
                        // Fallback to partial match
                        foreach (GridHighlightProperty rule in config.Properties)
                        {
                            string nRule = config.Normalize(rule.Name);
                            if (nProp.Contains(nRule, StringComparison.OrdinalIgnoreCase) ||
                                config.Normalize(prop.OriginalString).Contains(nRule, StringComparison.OrdinalIgnoreCase))
                            {
                                score += 1.0;
                                matchedRules++;
                                break;
                            }
                        }
                    }
                }

                if (totalRules > 0)
                {
                    score /= totalRules;
                }

                if (requiredCount > 0)
                {
                    double bonus = (double)matchedRules / requiredCount * 0.2;
                    score += bonus;
                }

                double specificity = (1.0 - (optionalCount / (double)Math.Max(1, totalRules))) * 0.1;
                score += specificity;

                if (best == null || score > bestScore)
                {
                    best = config;
                    bestScore = score;
                }
            }

            return best;
        }

        private bool IsMatchingCount(int count, int minPropertyCount, int maxPropertyCount)
        {
            if (minPropertyCount > 0 && count < minPropertyCount)
            {
                return false;
            }
            if (maxPropertyCount > 0 && count > maxPropertyCount)
            {
                return false;
            }

            return true;
        }

        private string Normalize(string input)
        {
            input ??= string.Empty;

            if (_normalizeCache.TryGetValue(input, out string cached))
                return cached;

            string result = StripHtmlTags(input);
            _normalizeCache[input] = result;
            return result;
        }

        private string CleanItemName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            int index = 0;
            // Skip leading digits
            while (index < name.Length && char.IsDigit(name[index])) index++;

            // Skip following whitespace
            while (index < name.Length && char.IsWhiteSpace(name[index])) index++;

            return name.Substring(index).Trim().ToLowerInvariant();
        }

        private string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            char[] output = new char[input.Length];
            int outputIndex = 0;
            bool insideTag = false;

            foreach (char c in input)
            {
                if (c == '<') { insideTag = true; continue; }
                if (c == '>') { insideTag = false; continue; }
                if (!insideTag) output[outputIndex++] = c;
            }

            return new string(output, 0, outputIndex).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKC);
        }

        private bool IsItemNameMatch(string itemName)
        {
            if (ItemNames.Count == 0)
                return true;

            string cleanedUpItemName = CleanItemName(itemName);
            return ItemNames.Any(name => string.Equals(cleanedUpItemName, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsWeightInRange(string propertyString, int minWeight, int maxWeight)
        {
            // Look for "weight: X stones" pattern
            int weightIndex = propertyString.IndexOf("weight:", StringComparison.OrdinalIgnoreCase);
            if (weightIndex < 0)
                return true; // No weight property found, so it passes the check

            // Extract the weight value
            int startIndex = weightIndex + 7; // length of "weight:"
            int endIndex = propertyString.IndexOf("stone", startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
                return true; // Malformed weight string, allow it

            string weightStr = propertyString.Substring(startIndex, endIndex - startIndex).Trim();
            if (!int.TryParse(weightStr, out int weight))
            {
                Log.Debug($"FAILED TO PARSE: {weightStr}");
                return true; // Couldn't parse weight, allow it
            }

            // Check if weight is in range
            // If minWeight is 0, no minimum check; if maxWeight is 0, no maximum check
            bool passesMin = minWeight == 0 || weight >= minWeight;
            bool passesMax = maxWeight == 0 || weight <= maxWeight;

            return passesMin && passesMax;
        }

        private bool MatchesSlot(byte layer)
        {
            if (EquipmentSlots.Other)
            {
                return true;
            }

            return layer switch
            {
                (byte)Layer.Talisman => EquipmentSlots.Talisman,
                (byte)Layer.OneHanded => EquipmentSlots.RightHand,
                (byte)Layer.TwoHanded => EquipmentSlots.LeftHand,
                (byte)Layer.Helmet => EquipmentSlots.Head,
                (byte)Layer.Earrings => EquipmentSlots.Earring,
                (byte)Layer.Necklace => EquipmentSlots.Neck,
                (byte)Layer.Torso or (byte)Layer.Tunic => EquipmentSlots.Chest,
                (byte)Layer.Shirt => EquipmentSlots.Shirt,
                (byte)Layer.Cloak => EquipmentSlots.Back,
                (byte)Layer.Robe => EquipmentSlots.Robe,
                (byte)Layer.Arms => EquipmentSlots.Arms,
                (byte)Layer.Gloves => EquipmentSlots.Hands,
                (byte)Layer.Bracelet => EquipmentSlots.Bracelet,
                (byte)Layer.Ring => EquipmentSlots.Ring,
                (byte)Layer.Waist => EquipmentSlots.Belt,
                (byte)Layer.Skirt => EquipmentSlots.Skirt,
                (byte)Layer.Legs => EquipmentSlots.Legs,
                (byte)Layer.Pants => EquipmentSlots.Legs,
                (byte)Layer.Shoes => EquipmentSlots.Footwear,

                (byte)Layer.Hair or
                (byte)Layer.Beard or
                (byte)Layer.Face or
                (byte)Layer.Mount or
                (byte)Layer.Backpack or
                (byte)Layer.ShopBuy or
                (byte)Layer.ShopBuyRestock or
                (byte)Layer.ShopSell or
                (byte)Layer.Bank or
                (byte)Layer.Invalid => false,

                _ => true
            };
        }
    }
}
