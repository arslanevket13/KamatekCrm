using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Teknisyen Planlama ViewModel
    /// </summary>
    public class SchedulerViewModel : ViewModelBase, IDropTarget
    {
        private readonly AppDbContext _context;

        public ObservableCollection<User> Technicians { get; } = new();
        public ObservableCollection<ServiceJob> UnassignedJobs { get; } = new();
        
        // Basit bir gösterim için: Teknisyen listesi ve onların işleri
        // UI tarafında Grid veya Custom Control kullanılabilir. 
        // Burada veriyi sağlıyoruz.
        
        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadData();
                }
            }
        }

        public SchedulerViewModel()
        {
            _context = new AppDbContext();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                Technicians.Clear();
                UnassignedJobs.Clear();

                // Teknisyen Rolündeki Kullanıcılar (veya hepsi)
                var techs = _context.Users.Where(u => u.IsActive).ToList(); // Rol filtresi eklenebilir
                foreach (var t in techs) Technicians.Add(t);

                // Atanmamış İşler (veya o günkü işler değil, genel havuz)
                var pendingJobs = _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.AssignedUserId == null && j.Status != JobStatus.Completed)
                    .ToList();

                foreach (var j in pendingJobs) UnassignedJobs.Add(j);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Veri yükleme hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region IDropTarget

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ServiceJob)
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ServiceJob job && dropInfo.TargetItem is User technician)
            {
                // İş teknisyene atandı
                if (MessageBox.Show($"'{job.Description}' işi {technician.AdSoyad} kullanıcısına atansın mı?", "Atama Onayı", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    AssignJob(job, technician);
                }
            }
        }

        private void AssignJob(ServiceJob job, User technician)
        {
            try
            {
                var dbJob = _context.ServiceJobs.Find(job.Id);
                if (dbJob != null)
                {
                    dbJob.AssignedUserId = technician.Id;
                    dbJob.AssignedTechnician = technician.AdSoyad; // Backward compatibility
                    dbJob.ScheduledDate = SelectedDate; // Seçili tarihe ata

                    _context.SaveChanges();
                    LoadData(); // Listeleri yenile
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Atama hatası: {ex.Message}");
            }
        }

        #endregion
    }
}
