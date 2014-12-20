using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace AgFx
{
    internal class AutoRefreshService
    {
        private static AutoRefreshService _current;
        private Timer _timer;
        private readonly List<CacheEntry> _entriesToRefresh;

        public AutoRefreshService()
        {
            _entriesToRefresh = new List<CacheEntry>();
        }

        public static AutoRefreshService Current
        {
            get
            {
                if(_current == null)
                {
                    _current = new AutoRefreshService();
                }
                return _current;
            }
        }

        public void ScheduleRefresh(CacheEntry entry)
        {
            bool startTimer;
            lock(_entriesToRefresh)
            {
                var isAutoRefresh = entry.CachePolicy == CachePolicy.AutoRefresh;

                var index = _entriesToRefresh.IndexOf(entry);

                if(index == -1 && isAutoRefresh)
                {
                    _entriesToRefresh.Add(entry);
                }
                else if(index != -1 && !isAutoRefresh)
                {
                    _entriesToRefresh.RemoveAt(index);
                }

                startTimer = _entriesToRefresh.Count > 0;
            }

            if(startTimer)
            {
                EnsureTimer();
            }
        }

        private void EnsureTimer()
        {
            if(_timer == null)
            {
                _timer = new Timer(
                    state =>
                    {
                        // protect against reentrancy
                        //
                        lock(_timer)
                        {
                            IEnumerable<CacheEntry> entriesToRefresh;

                            // Find all the items that need refreshing.
                            //
                            lock(_entriesToRefresh)
                            {
                                entriesToRefresh = (from e in _entriesToRefresh
                                    where DateTime.Now >= e.ExpirationTime && e.CachePolicy == CachePolicy.AutoRefresh
                                    select e).ToArray();
                            }


                            foreach(var e in entriesToRefresh)
                            {
                                // remove from list, then kick off the refresh.
                                //

                                lock(_entriesToRefresh)
                                {
                                    if(_entriesToRefresh.Contains(e))
                                    {
                                        _entriesToRefresh.Remove(e);
                                    }
                                }
                                Debug.WriteLine("{0}: Auto refreshing {1} id={2}", DateTime.Now, e.ObjectType.Name,
                                    e.LoadContext.Identity);
                                e.DoRefresh();
                            }

                            if(_entriesToRefresh.Count == 0)
                            {
                                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                            }
                        }
                    },
                    null,
                    1000,
                    1000);
            }
            else if(_entriesToRefresh.Count > 0)
            {
                _timer.Change(1000, 1000);
            }
        }
    }
}