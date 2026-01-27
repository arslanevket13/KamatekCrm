using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kanban Satış Boru Hattı ViewModel
    /// GongSolutions.WPF.DragDrop implementasyonu
    /// </summary>
    public class PipelineViewModel : ViewModelBase, IDropTarget
    {
        private readonly AppDbContext _context;

        #region Kanban Columns

        public ObservableCollection<ServiceProject> Leads { get; } = new();
        public ObservableCollection<ServiceProject> Quoted { get; } = new();
        public ObservableCollection<ServiceProject> Negotiating { get; } = new();
        public ObservableCollection<ServiceProject> Won { get; } = new();
        public ObservableCollection<ServiceProject> Lost { get; } = new();

        #endregion

        public PipelineViewModel()
        {
            _context = new AppDbContext();
            LoadData();
        }

        private void LoadData()
        {
            Leads.Clear();
            Quoted.Clear();
            Negotiating.Clear();
            Won.Clear();
            Lost.Clear();

            var projects = _context.ServiceProjects
                .Include(p => p.Customer)
                .Where(p => p.Status != ProjectStatus.Cancelled) // İptal edilenler hariç
                .ToList();

            if (projects.Count == 0)
            {
                // Dummy Data for Demonstration
                Leads.Add(new ServiceProject { Title = "Örnek Proje: Otel WiFi", TotalCost = 150000, Customer = new Customer { FullName = "Grand Hotel" } });
                Quoted.Add(new ServiceProject { Title = "Örnek: Fabrika Kamera", TotalCost = 45000, Customer = new Customer { FullName = "Sanayi A.Ş." } });
                return;
            }

            foreach (var p in projects)
            {
                switch (p.PipelineStage)
                {
                    case PipelineStage.Lead: Leads.Add(p); break;
                    case PipelineStage.Quoted: Quoted.Add(p); break;
                    case PipelineStage.Negotiating: Negotiating.Add(p); break;
                    case PipelineStage.Won: Won.Add(p); break;
                    case PipelineStage.Lost: Lost.Add(p); break;
                }
            }
        }

        #region IDropTarget Implementation

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ServiceProject && dropInfo.TargetCollection is ObservableCollection<ServiceProject>)
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo == null || dropInfo.DragInfo == null) return;

            try
            {
                // Güvenli Tip Kontrolü
                if (dropInfo.Data is ServiceProject project && dropInfo.TargetCollection is ObservableCollection<ServiceProject> targetCollection)
                {
                    var sourceCollection = dropInfo.DragInfo.SourceCollection as ObservableCollection<ServiceProject>;
                    if (sourceCollection == null) return;

                    // Eğer kaynak ve hedef aynıysa ve index değişmiyorsa işlem yapma
                    if (sourceCollection == targetCollection && sourceCollection.IndexOf(project) == dropInfo.InsertIndex)
                        return;

                    // Kaynak ve Hedef aynı ise (Reorder)
                    if (sourceCollection == targetCollection)
                    {
                         var oldIndex = sourceCollection.IndexOf(project);
                         
                         // Index sınır kontrolleri
                         if (oldIndex < 0 || oldIndex >= sourceCollection.Count) return;
                         
                         int newIndex = dropInfo.InsertIndex;
                         if (newIndex < 0) newIndex = 0;
                         if (newIndex > sourceCollection.Count) newIndex = sourceCollection.Count;
                         
                         // Move işlemi insert mantığıyla çalıştığı için index kayması olabilir, Move methodu bunu handle eder ama yine de dikkatli olalım
                         // GongSolutions genelde doğru index verir ama manual fix gerekebilir
                         if (newIndex > sourceCollection.Count - 1) newIndex = sourceCollection.Count - 1;

                         sourceCollection.Move(oldIndex, newIndex);
                    }
                    else
                    {
                        // Farklı kolona taşıma
                        
                        // Önce source'dan sil
                        sourceCollection.Remove(project);

                        // Hedefe ekle (Index kontrolü)
                        int insertIndex = dropInfo.InsertIndex;
                        if (insertIndex < 0) insertIndex = 0;
                        if (insertIndex > targetCollection.Count) insertIndex = targetCollection.Count;

                        targetCollection.Insert(insertIndex, project);

                        // Database Update logic...
                        PipelineStage newStage;

                        if (targetCollection == Leads) newStage = PipelineStage.Lead;
                        else if (targetCollection == Quoted) newStage = PipelineStage.Quoted;
                        else if (targetCollection == Negotiating) newStage = PipelineStage.Negotiating;
                        else if (targetCollection == Won) newStage = PipelineStage.Won;
                        else if (targetCollection == Lost) newStage = PipelineStage.Lost;
                        else return; // Tanımsız hedef

                        UpdateProjectStage(project, newStage);
                    }
                }
            }
            catch (Exception ex)
            {
                // Crash yerine kullanıcıya uyarı göster
                // MessageBox.Show($"Taşıma işlemi sırasında hata: {ex.Message} \n\nLütfen sayfayı yenileyiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                // Production ortamında sessiz failure veya loglama tercih edilebilir, ancak kullanıcıya crash hissettirmemek önemli.
                System.Diagnostics.Debug.WriteLine($"Drop Error: {ex}");
            }
        }

        private void UpdateProjectStage(ServiceProject project, PipelineStage newStage)
        {
            try
            {
                var dbProject = _context.ServiceProjects.Find(project.Id);
                if (dbProject != null)
                {
                    dbProject.PipelineStage = newStage;
                    
                    // Won aşamasına geçişte özel işlem: İş Emri Oluşturma Onayı
                    if (newStage == PipelineStage.Won)
                    {
                        var result = MessageBox.Show(
                            $"'{calculateDesc(project)}' satışını kazandınız! \nBu proje için otomatik iş emri oluşturulsun mu?",
                            "Satış Kazanıldı 🚀",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            CreateJobForWonProject(dbProject);
                        }
                    }

                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
                LoadData(); // Hata durumunda geri al
            }
        }

        private string calculateDesc(ServiceProject p)
        {
            return p.Title;
        }

        private void CreateJobForWonProject(ServiceProject project)
        {
            // Otomatik Proje İş Emri
            var job = new ServiceJob
            {
                CustomerId = project.CustomerId,
                ServiceProjectId = project.Id,
                ServiceJobType = ServiceJobType.Project,
                JobCategory = JobCategory.Other, // Detaya göre seçilebilir
                WorkOrderType = WorkOrderType.Installation,
                Description = $"Proje Başlangıcı: {project.Title}",
                Status = JobStatus.Pending,
                CreatedDate = DateTime.Now,
                Price = project.TotalCost // veya Bütçe
            };

            _context.ServiceJobs.Add(job);
            // Project status da Active yapılabilir
            project.Status = ProjectStatus.Active;
        }

        #endregion
    }
}
