// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using AgFx;
using NWSWeather.Sample.Services;

namespace NWSWeather.Sample.ViewModels
{
    /// <summary>
    ///     Main ViewModel for the weather forecast.  Given a zipcode, it does the rest.
    ///     The caching attribute says that this data is valid for 15 mintutes (15*60 seconnds).  ValidCacheOnly
    ///     says that only valid cached data should be shown - we don't want to show last weeks forecast while waiting
    ///     for this weeks.  That's not helpful.
    /// </summary>
    [CachePolicy(CachePolicy.ValidCacheOnly, 60*15)]
    public class WeatherForecastVm : ModelItemBase<ZipCodeLoadContext>
    {
        /// <summary>
        ///     Our collection of weather periods.
        /// </summary>
        private readonly ObservableCollection<WeatherPeriod> _wp = new ObservableCollection<WeatherPeriod>();

        public WeatherForecastVm()
        {
        }

        public WeatherForecastVm(string zipcode) : base(new ZipCodeLoadContext(zipcode))
        {
        }

        /// <summary>
        ///     ZipCode info is the name of the city for the zipcode.  This is a sepearate
        ///     service lookup, so we treat it seperately.
        /// </summary>
        public ZipCodeVm ZipCodeInfo
        {
            get
            {
                return (Application.Current as App).DataManager.Load<ZipCodeVm>(LoadContext.ZipCode,
                    null,
                    ex => { MessageBox.Show("Can't find zip code " + LoadContext.ZipCode); });
            }
        }

        public ObservableCollection<WeatherPeriod> WeatherPeriods
        {
            get { return _wp; }
            set
            {
                if (_wp != null)
                {
                    _wp.Clear();

                    if (value != null)
                    {
                        foreach (var wp in value)
                        {
                            _wp.Add(wp);
                        }
                    }
                }
                RaisePropertyChanged("WeatherPeriods");
            }
        }


//        public override void UpdateFrom(object source)
//        {
//            var sourceObject = source as WeatherForecastVm;
//            if(sourceObject == null)
//            {
//                return;
//            }
//
//            NotificationContext.Post(state => WeatherPeriods = sourceObject.WeatherPeriods, null);
//        }

        /// <summary>
        ///     Our loader, which knows how to do two things:
        ///     1. Build the URI for requesting data for a given zipcode
        ///     2. Parse the return value from that URI
        /// </summary>
        public class WeatherForecastVmLoader : IDataLoader<ZipCodeLoadContext>
        {
            private const string NwsRestFormat =
                "http://www.weather.gov/forecasts/xml/sample_products/browser_interface/ndfdBrowserClientByDay.php?zipCodeList={0}&format=12+hourly&startDate={1:yyyy-MM-dd}";

            /// <summary>
            ///     Build a LoadRequest that knows how to fetch new data for our object.
            ///     In this case, it's just a URL so we construct the URL and then pass it to the
            ///     default WebLoadRequest object, along with our LoadContext
            /// </summary>
            public LoadRequest GetLoadRequest(ZipCodeLoadContext lc, Type objectType)
            {
                var uri = String.Format(NwsRestFormat, lc.ZipCode, DateTime.Now.Date);
                return new WebLoadRequest(lc, new Uri(uri));
            }

            /// <summary>
            ///     Once our LoadRequest has executed, we'll be handed back a stream containing the response from the
            ///     above URI, which we'll parse.
            ///     Note this will execute in two cases:
            ///     1. When we fetch fresh data from the Internet
            ///     2. When we are deserializing cached data off the disk.  The operation is equivelent at this point.
            /// </summary>
            public object Deserialize(ZipCodeLoadContext lc, Type objectType, Stream stream)
            {
                // Parse the XML out of the stream.
                var locs = NWSParser.ParseWeatherXml(new[] {lc.ZipCode}, stream);

                // make sure we got the right data
                var loc = locs.FirstOrDefault();

                if (loc == null)
                {
                    throw new FormatException("Didn't get any weather data.");
                }

                // Create our VM.  Note this is the same type as our containing object
                var vm = new WeatherForecastVm(lc.ZipCode);

                // push in the weather periods
                foreach (var wp in loc.WeatherPeriods)
                {
                    vm.WeatherPeriods.Add(wp);
                }

                return vm;
            }
        }
    }
}