﻿// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AgFx;

namespace NWSWeather.Sample.ViewModels
{
    /// <summary>
    ///     Our wrapper around fetching location information for a zipcode - city, state, etc.
    /// </summary>
    [CachePolicy(CachePolicy.Forever)]
    public class ZipCodeVm : ModelItemBase<ZipCodeLoadContext>
    {
        public ZipCodeVm()
        {
        }

        public ZipCodeVm(string zipcode)
            : base(new ZipCodeLoadContext(zipcode))
        {
        }

        /// <summary>
        ///     Loaders know how to do two things:
        ///     1. Request new data
        ///     2. Process the response of that request into an object of the containing type (ZipCodeVm in this case)
        /// </summary>
        public class ZipCodeVmDataLoader : IDataLoader<ZipCodeLoadContext>
        {
            private const string ZipCodeUriFormat = "http://www.webservicex.net/uszip.asmx/GetInfoByZIP?USZip={0}";

            public LoadRequest GetLoadRequest(ZipCodeLoadContext loadContext, Type objectType)
            {
                // build the URI, return a WebLoadRequest.
                var uri = String.Format(ZipCodeUriFormat, loadContext.ZipCode);
                return new WebLoadRequest(loadContext, new Uri(uri));
            }

            public object Deserialize(ZipCodeLoadContext loadContext, Type objectType, Stream stream)
            {
                // the XML will look like hte following, so we parse it.

                //<?xml version="1.0" encoding="utf-8"?>
                //<NewDataSet>
                //  <Table>
                //    <CITY>Kirkland</CITY>
                //    <STATE>WA</STATE>
                //    <ZIP>98033</ZIP>
                //    <AREA_CODE>425</AREA_CODE>
                //    <TIME_ZONE>P</TIME_ZONE>
                //  </Table>
                //</NewDataSet>                

                var xml = XElement.Load(stream);


                var table = (
                    from t in xml.Elements("Table")
                    select t).FirstOrDefault();

                if (table == null)
                {
                    throw new ArgumentException("Unknown zipcode " + loadContext.ZipCode);
                }

                var vm = new ZipCodeVm(loadContext.ZipCode)
                {
                    City = table.Element("CITY").Value,
                    State = table.Element("STATE").Value,
                    AreaCode = table.Element("AREA_CODE").Value,
                    TimeZone = table.Element("TIME_ZONE").Value
                };
                return vm;
            }
        }

        private string _city;
        public string City
        {
            get { return _city; }
            set
            {
                if (_city != value)
                {
                    _city = value;
                    RaisePropertyChanged("City");
                }
            }
        }

        private string _state;
        public string State
        {
            get { return _state; }
            set
            {
                if (_state != value)
                {
                    _state = value;
                    RaisePropertyChanged("State");
                }
            }
        }

        private string _areaCode;
        public string AreaCode
        {
            get { return _areaCode; }
            set
            {
                if (_areaCode != value)
                {
                    _areaCode = value;
                    RaisePropertyChanged("AreaCode");
                }
            }
        }
        
        private string _timeZone;
        public string TimeZone
        {
            get { return _timeZone; }
            set
            {
                if (_timeZone != value)
                {
                    _timeZone = value;
                    RaisePropertyChanged("TimeZone");
                }
            }
        }
    }
}