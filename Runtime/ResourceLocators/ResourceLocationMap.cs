using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.AddressableAssets.ResourceLocators
{
    /// <summary>
    /// Simple implementation of an IResourceLocator
    /// </summary>
    public class ResourceLocationMap : IResourceLocator
    {
        /// <summary>
        /// Construct a new ResourceLocationMap object.
        /// </summary>
        /// <param name="id">The locator id.</param>
        /// <param name="capacity">The expected number of items.</param>
        public ResourceLocationMap(int capacity = 0)
        {
            locations = new Dictionary<object, IList<IResourceLocation>>(capacity == 0 ? 100 : capacity);
        }

        // <summary>
        /// Construct a new ResourceLocationMap object with a list of locations.
        /// </summary>
        /// <param name="id">The locator id.</param>
        /// <param name="locations">The list of locations to initialize with.</param>
        public ResourceLocationMap(IList<ResourceLocationData> locations)
        {
            if (locations == null)
                return;
            this.locations = new Dictionary<object, IList<IResourceLocation>>(locations.Count * 2);
            var locMap = new Dictionary<string, ResourceLocationBase>();
            var dataMap = new Dictionary<string, ResourceLocationData>();
            //create and collect locations
            for (int i = 0; i < locations.Count; i++)
            {
                var rlData = locations[i];
                if (rlData.Keys == null)
                {
                    Addressables.LogErrorFormat("Address with id '{0}' does not have any valid keys, skipping...", rlData.InternalId);
                    continue;
                }

                if (locMap.ContainsKey(rlData.Keys))
                {
                    Addressables.LogErrorFormat("Duplicate address '{0}' with id '{1}' found, skipping...", rlData.Keys, rlData.InternalId);
                    continue;
                }

                var loc = new ResourceLocationBase(rlData.Keys, Addressables.ResolveInternalId(rlData.InternalId), rlData.Provider, rlData.ResourceType);
                locMap.Add(rlData.Keys, loc);
                dataMap.Add(rlData.Keys, rlData);
            }

            //fix up dependencies between them
            foreach (var kvp in locMap)
            {
                var data = dataMap[kvp.Key];
                if (data.Dependencies != null)
                {
                    foreach (var d in data.Dependencies)
                        kvp.Value.Dependencies.Add(locMap[d]);
                    kvp.Value.ComputeDependencyHash();
                }
            }

            foreach (KeyValuePair<string, ResourceLocationBase> kvp in locMap)
            {
                ResourceLocationData rlData = dataMap[kvp.Key];
                Add(rlData.Keys, kvp.Value);
            }
        }

        /// <summary>
        /// The mapping of key to location lists.
        /// </summary>
        Dictionary<object, IList<IResourceLocation>> locations;

        /// <summary>
        /// Enumeration of all locations for this locator.
        /// </summary>
        public IEnumerable<IResourceLocation> AllLocations => locations.SelectMany(k => k.Value);

        /// <summary>
        /// Map of all locations for this locator.
        /// </summary>
        public Dictionary<object, IList<IResourceLocation>> Locations => locations;

        /// <summary>
        /// The keys available in this locator.
        /// </summary>
        public IEnumerable<object> Keys
        {
            get { return locations.Keys; }
        }

        /// <summary>
        /// Locate all of the locations that match the given key.
        /// </summary>
        /// <param name="key">The key used to locate the locations.</param>
        /// <param name="type">The resource type.</param>
        /// <param name="locations">The list of found locations.  This list is shared so it should not be modified.</param>
        /// <returns>Returns true if a location was found. Returns false otherwise.</returns>
        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            if (!this.locations.TryGetValue(key, out var locs))
            {
                locations = null;
                return false;
            }

            if (type == null)
            {
                locations = locs;
                return true;
            }

            var validTypeCount = 0;
            foreach (var l in locs)
                if (type.IsAssignableFrom(l.ResourceType))
                    validTypeCount++;

            if (validTypeCount == 0)
            {
                locations = null;
                return false;
            }

            if (validTypeCount == locs.Count)
            {
                locations = locs;
                return true;
            }

            locations = new List<IResourceLocation>();
            foreach (var l in locs)
            {
                if (type.IsAssignableFrom(l.ResourceType))
                    locations.Add(l);
            }

            return true;
        }

        /// <summary>
        /// Add a new location.
        /// </summary>
        /// <param name="key">The key to reference the location.</param>
        /// <param name="location">The location to add.</param>
        public void Add(string key, IResourceLocation location)
        {
            IList<IResourceLocation> locations;
            if (!this.locations.TryGetValue(key, out locations))
                this.locations.Add(key, locations = new List<IResourceLocation>());
            locations.Add(location);
        }

        /// <summary>
        /// Add a list of locations.
        /// </summary>
        /// <param name="key">The key to reference the locations with.</param>
        /// <param name="locations">The list of locations to store at the given key.</param>
        public void Add(object key, IList<IResourceLocation> locations)
        {
            this.locations.Add(key, locations);
        }
    }
}
