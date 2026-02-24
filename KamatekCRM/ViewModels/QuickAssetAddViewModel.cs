using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    public class QuickAssetAddViewModel : ViewModelBase
    {
        private readonly int _customerId;
        private string _selectedCategoryTag;
        private string _brand;
        private string _model;
        private string _serialNumber;
        private string _location;

        public string SelectedCategoryTag
        {
            get => _selectedCategoryTag;
            set => SetProperty(ref _selectedCategoryTag, value);
        }

        public string Brand
        {
            get => _brand;
            set => SetProperty(ref _brand, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        public CustomerAsset? CreatedAsset { get; private set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        // Action to close the window (View Service / Interaction Request pattern)
        public Action<bool>? RequestClose { get; set; }

        public QuickAssetAddViewModel(int customerId)
        {
            _customerId = customerId;
            _selectedCategoryTag = "CCTV"; // Default
            _brand = string.Empty;
            _model = string.Empty;
            _serialNumber = string.Empty;
            _location = string.Empty;

            SaveCommand = new RelayCommand(_ => ExecuteSave(), _ => CanExecuteSave());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        private bool CanExecuteSave()
        {
            return !string.IsNullOrWhiteSpace(Brand) && !string.IsNullOrWhiteSpace(Model);
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(false);
        }

        private void ExecuteSave()
        {
            if (string.IsNullOrWhiteSpace(Brand))
            {
                MessageBox.Show("Lütfen marka girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                MessageBox.Show("Lütfen model girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var category = SelectedCategoryTag switch
            {
                "CCTV" => JobCategory.CCTV,
                "VideoIntercom" => JobCategory.VideoIntercom,
                "FireAlarm" => JobCategory.FireAlarm,
                "BurglarAlarm" => JobCategory.BurglarAlarm,
                "SmartHome" => JobCategory.SmartHome,
                "AccessControl" => JobCategory.AccessControl,
                "SatelliteSystem" => JobCategory.SatelliteSystem,
                "FiberOptic" => JobCategory.FiberOptic,
                _ => JobCategory.CCTV
            };

            try
            {
                using var context = new AppDbContext();

                var asset = new CustomerAsset
                {
                    CustomerId = _customerId,
                    Category = category,
                    Brand = Brand.Trim(),
                    Model = Model.Trim(),
                    SerialNumber = SerialNumber?.Trim(),
                    Location = Location?.Trim(),
                    Status = AssetStatus.Active,
                    CreatedDate = DateTime.Now
                };

                context.CustomerAssets.Add(asset);
                context.SaveChanges();

                CreatedAsset = asset;
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
