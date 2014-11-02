using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace AgFx
{

    /// <summary>
    /// Provides a container for the access stats for a CacheEntry
    /// </summary>
    internal class EntryStats
    {

        internal CacheEntry _cacheEntry;

        private List<double> _fetchTimes;
        private List<double> _deserializeTimes;
        private List<int> _deserializeSizes;
        private List<double> _updateTimes;

        public int RequestCount { get; private set; }
        public int FetchCount { get; private set; }
        public int FetchFailCount { get; set; }
        public int DeserializeFailCount { get; set; }

        public double CacheHitRatio
        {
            get
            {
                if (RequestCount == 0) return 0;
                return (RequestCount - FetchCount) / (double)RequestCount;
            }
        }

        public double MaxFetchTime
        {
            get
            {
                if (_fetchTimes == null)
                {
                    return 0;
                }
                return _fetchTimes.Max();
            }
        }

        public double MinFetchTime
        {
            get
            {
                if (_fetchTimes == null) return 0;
                return _fetchTimes.Min();
            }
        }

        public double AverageFetchTime
        {
            get
            {
                if (_fetchTimes == null) return 0;
                return _fetchTimes.Average();
            }
        }

        public double MaxDeserializeTime
        {
            get
            {
                if (_deserializeTimes == null)
                {
                    return 0;
                }
                return _deserializeTimes.Max();
            }
        }

        public double MinDeserializeTime
        {
            get
            {
                if (_deserializeTimes == null) return 0;
                return _deserializeTimes.Min();
            }
        }

        public double AverageDeserializeTime
        {
            get
            {
                if (_deserializeTimes == null) return 0;
                return _deserializeTimes.Average();
            }
        }

        public int MinDataSize
        {
            get
            {
                if (_deserializeSizes == null) return 0;
                return _deserializeSizes.Min();
            }
        }

        public int MaxDataSize
        {
            get
            {
                if (_deserializeSizes == null) return 0;
                return _deserializeSizes.Max();
            }
        }

        public double AverageDataSize
        {
            get
            {
                if (_deserializeSizes == null) return 0;
                return _deserializeSizes.Average();
            }
        }

        public double UpdateCount
        {
            get
            {
                if (_updateTimes == null) return 0;
                return _updateTimes.Count();
            }
        }

        public double AverageUpdateTime
        {
            get
            {
                if (_updateTimes == null) return 0;
                return _updateTimes.Average();
            }
        }

        public double MaxUpdateTime
        {
            get
            {
                if (_updateTimes == null) return 0;
                return _updateTimes.Max();
            }
        }

        public EntryStats(CacheEntry entry)
        {
            _cacheEntry = entry;
        }

        DateTime? _deserializeStartTime;

        public void OnStartDeserialize()
        {

            if (!DataManager.ShouldCollectStatistics) return;

            _deserializeStartTime = DateTime.Now;
        }

        public void OnCompleteDeserialize(int dataSize)
        {

            if (!DataManager.ShouldCollectStatistics) return;

            if (_deserializeStartTime == null)
            {
                return;
            }
            var time = DateTime.Now.Subtract(_deserializeStartTime.Value).TotalMilliseconds;
            _deserializeStartTime = null;
            if (_deserializeTimes == null)
            {
                _deserializeTimes = new List<double>();
            }

            _deserializeTimes.Add(time);

            if (_deserializeSizes == null)
            {
                _deserializeSizes = new List<int>();
            }
            _deserializeSizes.Add(dataSize);
        }

        public void Reset()
        {
            RequestCount = 0;
            FetchCount = 0;
            DeserializeFailCount = 0;
            _fetchTimes = null;
            _deserializeTimes = null;
            _deserializeSizes = null;
            _updateTimes = null;
        }

        public void OnRequest()
        {

            if (!DataManager.ShouldCollectStatistics) return;

            RequestCount++;
        }

        DateTime? _fetchStartTime;

        public void OnStartFetch()
        {

            if (!DataManager.ShouldCollectStatistics) return;
            _fetchStartTime = DateTime.Now;
        }

        public void OnCompleteFetch(bool success)
        {

            if (!DataManager.ShouldCollectStatistics) return;

            if (!_fetchStartTime.HasValue)
            {
                return;
            }

            var time = DateTime.Now.Subtract(_fetchStartTime.Value).TotalMilliseconds;
            _fetchStartTime = null;
            FetchCount++;
            if (!success)
            {
                FetchFailCount++;
            }
            if (_fetchTimes == null)
            {
                _fetchTimes = new List<double>();
            }
            _fetchTimes.Add(time);
        }


        internal void OnDeserializeFail()
        {
            DeserializeFailCount++;
        }

        DateTime? _updateStart;

        internal void OnStartUpdate()
        {
            _updateStart = DateTime.Now;
        }

        internal void OnCompleteUpdate()
        {
            if (_updateStart == null)
            {
                return;
            }

            var time = DateTime.Now.Subtract(_updateStart.Value).TotalMilliseconds;
            _updateStart = null;
            if (_updateTimes == null)
            {
                _updateTimes = new List<double>();
            }
            _updateTimes.Add(time);
        }
        
        public static void GenerateStats(string groupName, IEnumerable<EntryStats> flatStats, TextWriter writer)
        {
            var totalInstances = flatStats.Count();
            var totalRequests = flatStats.Sum(s => s.RequestCount);
            var totalFetchReqeusts = flatStats.Sum(s => s.FetchCount);
            var totalFetchFail = flatStats.Sum(s => s.FetchFailCount);
            var totalDeserializeFail = flatStats.Sum(s => s.DeserializeFailCount);

            bool skipAvg = flatStats.Count() == 0;

            double cacheHitRate = skipAvg ? 0.0 : flatStats.Average(s => s.CacheHitRatio);
            double avgFetch = skipAvg ? 0.0 : flatStats.Average(s => s.AverageFetchTime);
            double avgDeserialize = skipAvg ? 0.0 : flatStats.Average(s => s.AverageDeserializeTime);
            double avgSize = skipAvg ? 0.0 : flatStats.Average(s => s.AverageDataSize);
            double avgUpdate = skipAvg ? 0.0 : flatStats.Average(s => s.AverageUpdateTime);

            var maxFetch = from s in flatStats
                           where s.MaxFetchTime == flatStats.Max(s2 => s2.MaxFetchTime)
                           select s;

            var maxDeserialize = from s in flatStats
                                 where s.MaxDeserializeTime == flatStats.Max(s2 => s2.MaxDeserializeTime)
                                 select s;

            var maxSize = from s in flatStats
                          where s.MaxDataSize == flatStats.Max(s2 => s2.MaxDataSize)
                          select s;

            var maxUpdate = from s in flatStats
                            where s.MaxUpdateTime == flatStats.Max(s2 => s2.MaxUpdateTime)
                            select s;

            writer.WriteLine("{0}:", groupName);
            writer.WriteLine("\tTotal Instances: {0}", totalInstances);
            writer.WriteLine("\tTotal Requests: {0}", totalRequests);
            writer.WriteLine("\tTotal Fetches: {0}", totalFetchReqeusts);
            writer.WriteLine("\tTotal Fetch Failures: {0}", totalFetchFail);
            writer.WriteLine("\tTotal Deserialization Failures: {0}", totalDeserializeFail);
            writer.WriteLine("\tCache Hit Rate: {0:0.00}", cacheHitRate);
            writer.WriteLine("\tAverage Fetch Time: {0:0.0}ms", avgFetch);

            if (maxFetch.Any())
            {
                var mf = maxFetch.First();
                writer.WriteLine("\tMaximum Fetch Time: {0:0.0}ms (Object Type={1}, ID={2})", mf.MaxFetchTime, mf._cacheEntry.ObjectType.Name, mf._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\tAverage Deserialize Time: {0:0.0}ms", avgDeserialize);
            if (maxDeserialize.Any())
            {
                var md = maxDeserialize.First();
                writer.WriteLine("\tMaximum Deserialize Time: {0:0.0}ms (Object Type={1}, ID={2})", md.MaxDeserializeTime, md._cacheEntry.ObjectType.Name, md._cacheEntry.LoadContext.Identity);
            }


            writer.WriteLine("\tAverage Deserialize Size: {0:0.0} bytes", avgSize);
            if (maxSize.Any())
            {
                var ms = maxSize.First();
                writer.WriteLine("\tMaximum Deserialize Size: {0} bytes (Object Type={1}, ID={2})", ms.MaxDataSize, ms._cacheEntry.ObjectType.Name, ms._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\tAverage Update Time (UI Thread): {0:0.0}ms", avgUpdate);
            if (maxUpdate.Any())
            {
                var mu = maxUpdate.First();
                writer.WriteLine("\tMaximum Update Time (UI Thread): {0:0.0}ms (Object Type={1}, ID={2})", mu.MaxUpdateTime, mu._cacheEntry.ObjectType.Name, mu._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\r\n\r\n");
        }
    }
}
