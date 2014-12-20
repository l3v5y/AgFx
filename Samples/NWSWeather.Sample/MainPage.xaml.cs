// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Navigation;
using AgFx;
using Microsoft.Phone.Controls;
using NWSWeather.Sample.ViewModels;

namespace NWSWeather.Sample
{
    public partial class MainPage : PhoneApplicationPage
    {
        private readonly DataManager _dataManager;
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            var app = Application.Current as App;
            if (app != null)
            {
                _dataManager = app.DataManager;
            }
        }

        private void btnAddZipCode_Click(object sender, RoutedEventArgs e)
        {
            // Load up a new ViewModel based on the zip.
            // This will either fetch new data from the Internet, or load the cached data off disk
            // as appropriate.
            //
            DataContext = _dataManager.Load<WeatherForecastVm>(txtZipCode.Text,
                vm =>
                {
                    // upon a succesful load, show the info panel.
                    // this is a bit of a hack, but we can't databind against
                    // a non-existant data context...
                    info.Visibility = Visibility.Visible;
                },
                ex => { MessageBox.Show("Failed to get data for " + txtZipCode.Text); }
                );
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // when we navigage away, save the zip.
            IsolatedStorageSettings.ApplicationSettings["zip"] = txtZipCode.Text;
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                // if we have a saved zip, automatically load that up.
                string zip;
                if (IsolatedStorageSettings.ApplicationSettings.TryGetValue("zip", out zip) &&
                    !String.IsNullOrEmpty(zip))
                {
                    txtZipCode.Text = zip;
                    btnAddZipCode_Click(null, null);

                    // remove it in case of failure, we'll re-add it later.
                    IsolatedStorageSettings.ApplicationSettings.Remove("zip");
                }
            }
            catch
            {
            }
            base.OnNavigatedTo(e);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _dataManager.Refresh<WeatherForecastVm>(DataContext, null, null);
        }
    }
}